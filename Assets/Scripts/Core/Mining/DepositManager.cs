using System;
using System.Collections.Generic;
using MineIt.World;

namespace MineIt.Mining
{
    public sealed class DepositManager
    {
        private readonly int _seed;

        // Simple: keep discovered deposits in a dictionary by id for now
        private readonly Dictionary<int, Deposit> _allDeposits = new Dictionary<int, Deposit>();

        // ===== Artifacts (guaranteed deterministic spawns) =====
        private sealed class ArtifactSpawn
        {
            public string ArtifactId = "";
            public int DepositId;
            public int Tx;
            public int Ty;
            public int DepthMeters;
        }

        private readonly List<ArtifactSpawn> _artifactSpawns = new List<ArtifactSpawn>(6);

        // Canonical artifact IDs (stable)
        private static readonly string[] ArtifactIds =
        {
            "stellar_shard",
            "ancient_lattice",
            "void_compass",
            "quantum_fossil",
            "machine_relic",
            "echo_prism"
        };
        // ======================================================

        public DepositManager(int seed)
        {
            _seed = seed;
            BuildArtifactSpawnsDeterministic();
        }

        public IEnumerable<Deposit> GetAllDeposits() => _allDeposits.Values;

        /// <summary>
        /// Called when a chunk is first created/loaded.
        /// NOTE: townCenter is passed in to keep the signature aligned with your core architecture,
        /// even though the current town is always the center in your implementation.
        /// </summary>
        public void PopulateChunkDeposits(Chunk chunk, int townCenterTx, int townCenterTy)
        {
            // Deterministic per chunk
            int cx = chunk.Coord.Cx;
            int cy = chunk.Coord.Cy;

            int chunkSeed = ChunkSeed(cx, cy);
            var rng = new Random(chunkSeed);

            // Always materialize any precomputed artifacts that fall inside this chunk
            MaterializeArtifactsForChunk(chunk);

            // deposits per chunk (tuning constant)
            // TESTING: increase density so scans find something more often
            int count = rng.Next(3, 8); // 3..7 deposits per chunk
            for (int i = 0; i < count; i++)
            {
                int lx = rng.Next(0, Chunk.CHUNK_SIZE_TILES);
                int ly = rng.Next(0, Chunk.CHUNK_SIZE_TILES);

                int tx = cx * Chunk.CHUNK_SIZE_TILES + lx;
                int ty = cy * Chunk.CHUNK_SIZE_TILES + ly;

                int depth = rng.Next(30, 651);
                int tier = rng.Next(1, 16);
                string ore = PickOre(rng, depth);

                int units = BaseUnitsForTier(tier);

                int id = MakeDepositId(cx, cy, i);

                if (_allDeposits.ContainsKey(id))
                    continue;

                var d = new Deposit
                {
                    DepositId = id,
                    OreTypeId = ore,
                    CenterTx = tx,
                    CenterTy = ty,
                    DepthMeters = depth,
                    SizeTier = tier,
                    RemainingUnits = units,
                    DiscoveredByPlayer = false,

                    // artifacts (regular deposits are not artifacts)
                    IsArtifact = false,
                    ArtifactId = ""
                };

                AddOrReplaceDepositToChunk(chunk, d);
            }
        }

        public Deposit TryGetDepositById(int depositId)
        {
            _allDeposits.TryGetValue(depositId, out var d);
            return d;
        }

        public List<ScanResult> Scan(
            ChunkManager chunks,
            int scanCenterTx,
            int scanCenterTy,
            int radiusTiles,
            int maxDepthMeters,
            int sizeNoiseTiers,
            int detectorTier,
            Random rng)
        {
            int minTx = scanCenterTx - radiusTiles;
            int maxTx = scanCenterTx + radiusTiles;
            int minTy = scanCenterTy - radiusTiles;
            int maxTy = scanCenterTy + radiusTiles;

            var minC = ChunkManager.TileToChunk(minTx, minTy);
            var maxC = ChunkManager.TileToChunk(maxTx, maxTy);

            int r2 = radiusTiles * radiusTiles;

            var results = new List<ScanResult>();

            for (int cy = minC.Cy; cy <= maxC.Cy; cy++)
                for (int cx = minC.Cx; cx <= maxC.Cx; cx++)
                {
                    Chunk ch;
                    try { ch = chunks.GetOrLoadChunk(cx, cy); }
                    catch { continue; }

                    foreach (var d in ch.Deposits)
                    {
                        if (d.RemainingUnits <= 0) continue;
                        if (d.DepthMeters > maxDepthMeters) continue;

                        // ===== Artifact Tier Gating (Locked) =====
                        // Artifacts appear in scan results ONLY if Detector Tier == 5.
                        if (d.IsArtifact && detectorTier < 5)
                            continue;
                        // ========================================

                        int dx = d.CenterTx - scanCenterTx;
                        int dy = d.CenterTy - scanCenterTy;
                        if (dx * dx + dy * dy > r2) continue;

                        // Signal strength
                        double distTiles = System.Math.Sqrt(dx * dx + dy * dy);
                        double hd = distTiles / System.Math.Max(radiusTiles, 1);
                        double vd = (double)d.DepthMeters / System.Math.Max(maxDepthMeters, 1);

                        double sigBase = OreSignatureStrength(d.OreTypeId);
                        double sigSize = SigSizeFactor(d.SizeTier);

                        double strength = (sigBase * sigSize) / (1.0 + hd * hd + vd * vd);
                        int bars = SignalBarsFromStrength(strength);

                        // Size estimate noise (tier-based)
                        int estTier = d.SizeTier;
                        if (sizeNoiseTiers > 0)
                        {
                            int n = rng.Next(-sizeNoiseTiers, sizeNoiseTiers + 1);
                            estTier = System.Math.Clamp(d.SizeTier + n, 1, 15);
                        }

                        int distTilesInt = (int)System.Math.Round(distTiles);

                        results.Add(new ScanResult
                        {
                            DepositId = d.DepositId,
                            OreTypeId = d.OreTypeId,
                            CenterTx = d.CenterTx,
                            CenterTy = d.CenterTy,

                            DepthMeters = d.DepthMeters,

                            TrueSizeTier = d.SizeTier,
                            EstimatedSizeTier = estTier,
                            EstimatedSizeClass = SizeClassFromTier(estTier),

                            SignalBars = bars,

                            // NEW intelligence fields
                            DistanceTiles = distTilesInt,
                            IsArtifact = d.IsArtifact,
                            ClaimedByPlayer = d.ClaimedByPlayer,
                            ClaimedByNpcId = d.ClaimedByNpcId.HasValue ? d.ClaimedByNpcId.Value : -1,
                            IsDepleted = d.IsDepleted || (d.RemainingUnits <= 0)
                        });
                    }
                }

            return results;
        }

        private int ChunkSeed(int cx, int cy)
        {
            unchecked
            {
                int h = 23;
                h = h * 31 + _seed;
                h = h * 31 + cx;
                h = h * 31 + cy;
                h ^= (h << 13);
                h ^= (h >> 17);
                h ^= (h << 5);
                return h;
            }
        }

        private static int MakeDepositId(int cx, int cy, int i)
        {
            unchecked
            {
                // 8 bits for cx (0..255), 8 bits for cy (0..255), 16 bits for i (0..65535)
                // Works perfectly for a 512x512 tile world with 32x32 chunks => 16x16 chunks.
                int cxb = cx & 0xFF;
                int cyb = cy & 0xFF;
                int ii = i & 0xFFFF;

                return (cxb) | (cyb << 8) | (ii << 16);
            }
        }

        private static int BaseUnitsForTier(int tier)
        {
            // MVP mapping (your earlier table)
            return tier switch
            {
                1 => 20,
                2 => 28,
                3 => 40,
                4 => 56,
                5 => 80,
                6 => 112,
                7 => 160,
                8 => 224,
                9 => 320,
                10 => 448,
                11 => 640,
                12 => 896,
                13 => 1280,
                14 => 1792,
                15 => 2560,
                _ => 80
            };
        }

        private static string PickOre(Random rng, int depthMeters)
        {
            // MVP heuristic: deeper => rarer
            int roll = rng.Next(100);

            if (depthMeters >= 600)
                return roll < 3 ? "aether" : (roll < 10 ? "xenon" : "plat");

            if (depthMeters >= 450)
                return roll < 10 ? "gold" : (roll < 35 ? "cobalt" : "titanium");

            if (depthMeters >= 250)
                return roll < 15 ? "lithium" : (roll < 40 ? "alum" : "quartz");

            return roll < 45 ? "iron" : "copper";
        }

        private static double OreSignatureStrength(string oreId)
        {
            // MVP defaults (later: from ores.json)
            return oreId switch
            {
                // Artifacts: strong signature so they rank highly and are easy to notice once in range
                "artifact" => 2.2,

                "scrap" => 0.8,
                "iron" => 1.0,
                "copper" => 1.0,
                "quartz" => 1.1,
                "alum" => 1.05,
                "lithium" => 1.15,
                "titanium" => 1.2,
                "cobalt" => 1.25,
                "neodym" => 1.3,
                "gold" => 1.2,
                "plat" => 1.35,
                "xenon" => 1.45,
                "aether" => 1.55,
                _ => 1.0
            };
        }

        private static double SigSizeFactor(int sizeTier)
        {
            // sigSize = 1 + 0.08*log2(SizeTier+1)
            // (mild size effect)
            double v = sizeTier + 1;
            double log2 = System.Math.Log(v, 2.0);
            return 1.0 + 0.08 * log2;
        }

        private static int SignalBarsFromStrength(double s)
        {
            // Map to 1..5 (tune later)
            if (s >= 1.20) return 5;
            if (s >= 0.85) return 4;
            if (s >= 0.60) return 3;
            if (s >= 0.40) return 2;
            return 1;
        }

        private static string SizeClassFromTier(int tier)
        {
            // Simple buckets for UI (tune later)
            if (tier <= 5) return "Small";
            if (tier <= 10) return "Medium";
            return "Large";
        }

        public void AddOrReplaceDepositToChunk(Chunk chunk, Deposit d)
        {
            // Replace in manager dictionary
            _allDeposits[d.DepositId] = d;

            // Replace in chunk list (by id) — CORE-ONLY mutable access
            var list = chunk.DepositsMutable;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].DepositId == d.DepositId)
                {
                    list[i] = d;
                    return;
                }
            }

            // Not found → add
            list.Add(d);
        }

        // ===== Artifacts (guaranteed deterministic spawns) =====

        private void BuildArtifactSpawnsDeterministic()
        {
            _artifactSpawns.Clear();

            var rng = new Random(_seed ^ 0x6B9D2E17);

            const int townTx = MineIt.Simulation.GameSession.WORLD_W_TILES / 2;
            const int townTy = MineIt.Simulation.GameSession.WORLD_H_TILES / 2;

            for (int i = 0; i < ArtifactIds.Length; i++)
            {
                string artifactId = ArtifactIds[i];

                int tx, ty;

                // Guaranteed Band-4 selection
                for (; ; )
                {
                    tx = rng.Next(0, MineIt.Simulation.GameSession.WORLD_W_TILES);
                    ty = rng.Next(0, MineIt.Simulation.GameSession.WORLD_H_TILES);

                    if (ComputeRegionBand(tx, ty, townTx, townTy) == 4)
                        break;
                }

                int depth = rng.Next(450, 651);

                _artifactSpawns.Add(new ArtifactSpawn
                {
                    ArtifactId = artifactId,
                    DepositId = MakeArtifactDepositId(i, tx, ty),
                    Tx = tx,
                    Ty = ty,
                    DepthMeters = depth
                });
            }
        }

        private void MaterializeArtifactsForChunk(Chunk chunk)
        {
            if (_artifactSpawns.Count == 0) return;

            int cx = chunk.Coord.Cx;
            int cy = chunk.Coord.Cy;

            int baseTx = cx * Chunk.CHUNK_SIZE_TILES;
            int baseTy = cy * Chunk.CHUNK_SIZE_TILES;
            int endTx = baseTx + Chunk.CHUNK_SIZE_TILES - 1;
            int endTy = baseTy + Chunk.CHUNK_SIZE_TILES - 1;

            for (int i = 0; i < _artifactSpawns.Count; i++)
            {
                var a = _artifactSpawns[i];

                if (a.Tx < baseTx || a.Tx > endTx || a.Ty < baseTy || a.Ty > endTy)
                    continue;

                // Already created?
                if (_allDeposits.ContainsKey(a.DepositId))
                    continue;

                var d = new Deposit
                {
                    DepositId = a.DepositId,
                    OreTypeId = "artifact",
                    CenterTx = a.Tx,
                    CenterTy = a.Ty,
                    DepthMeters = a.DepthMeters,
                    SizeTier = 15,
                    RemainingUnits = 1,

                    DiscoveredByPlayer = false,

                    IsArtifact = true,
                    ArtifactId = a.ArtifactId
                };

                AddOrReplaceDepositToChunk(chunk, d);
            }
        }

        private static int MakeArtifactDepositId(int index0to5, int tx, int ty)
        {
            // High-bit namespace to avoid collisions with MakeDepositId()
            unchecked
            {
                int idx = index0to5 & 0x7;  // 3 bits
                int x = tx & 0x1FF;         // 9 bits (0..511)
                int y = ty & 0x1FF;         // 9 bits (0..511)

                return (0x7 << 28) | (idx << 18) | (x << 9) | y;
            }
        }

        private static int ComputeRegionBand(int tx, int ty, int townTx, int townTy)
        {
            int dx = tx - townTx;
            int dy = ty - townTy;

            // Euclidean tile distance rounded to int
            int d = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));

            if (d <= 40) return 0;
            if (d <= 120) return 1;
            if (d <= 220) return 2;
            if (d <= 320) return 3;
            return 4;
        }

        // ===== Debug/Presentation accessors for artifact spawns =====
        // Unity-side debug map overlay needs coordinates; keep it read-only.

        public int GetArtifactSpawnCount()
        {
            return (_artifactSpawns != null) ? _artifactSpawns.Count : 0;
        }

        public bool TryGetArtifactSpawn(int index,
            out string artifactId,
            out int tx,
            out int ty,
            out int depthMeters,
            out int depositId)
        {
            artifactId = "";
            tx = ty = depthMeters = depositId = 0;

            if (_artifactSpawns == null) return false;
            if ((uint)index >= (uint)_artifactSpawns.Count) return false;

            var a = _artifactSpawns[index];
            if (a == null) return false;

            artifactId = a.ArtifactId ?? "";
            tx = a.Tx;
            ty = a.Ty;
            depthMeters = a.DepthMeters;
            depositId = a.DepositId;
            return true;
        }
        // ==========================================================


        // ======================================================

        public string GetArtifactSpawnsDebugReport()
        {
            int idsLen = (ArtifactIds != null) ? ArtifactIds.Length : -1;
            int spawns = (_artifactSpawns != null) ? _artifactSpawns.Count : -1;

            var sb = new System.Text.StringBuilder(1024);

            sb.Append("Artifacts DEBUG: ArtifactIds.Length=")
              .Append(idsLen)
              .Append("  _artifactSpawns.Count=")
              .Append(spawns)
              .AppendLine();

            if (_artifactSpawns == null || _artifactSpawns.Count == 0)
            {
                sb.AppendLine("Artifacts: (none precomputed)");
                return sb.ToString();
            }

            sb.AppendLine("Artifacts (precomputed, deterministic):");

            int townTx = MineIt.Simulation.GameSession.WORLD_W_TILES / 2;
            int townTy = MineIt.Simulation.GameSession.WORLD_H_TILES / 2;

            for (int i = 0; i < _artifactSpawns.Count; i++)
            {
                var a = _artifactSpawns[i];
                if (a == null) continue;

                int dx = a.Tx - townTx;
                int dy = a.Ty - townTy;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                int cx = a.Tx / MineIt.World.Chunk.CHUNK_SIZE_TILES;
                int cy = a.Ty / MineIt.World.Chunk.CHUNK_SIZE_TILES;

                sb.Append(" ")
                  .Append(i + 1).Append(") ")
                  .Append(a.ArtifactId)
                  .Append("  id=").Append(a.DepositId)
                  .Append("  tile=(").Append(a.Tx).Append(",").Append(a.Ty).Append(')')
                  .Append("  chunk=(").Append(cx).Append(",").Append(cy).Append(')')
                  .Append("  depth=").Append(a.DepthMeters).Append("m")
                  .Append("  dist=").Append(dist.ToString("0.0"))
                  .AppendLine();
            }

            return sb.ToString();
        }

    }
}
