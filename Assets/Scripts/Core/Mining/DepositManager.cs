using System;
using System.Collections.Generic;
using MineIt.World;

namespace MineIt.Mining
{
    public sealed class DepositManager
    {
        private readonly int _seed;

        // Simple: keep discovered deposits in a dictionary by id for now
        private readonly Dictionary<int, Deposit> _allDeposits = new();

        public DepositManager(int seed) => _seed = seed;

        public IEnumerable<Deposit> GetAllDeposits() => _allDeposits.Values;

        // Called when a chunk is first created/loaded.
        public void PopulateChunkDeposits(Chunk chunk)
        {
            // Deterministic per chunk
            int cx = chunk.Coord.Cx;
            int cy = chunk.Coord.Cy;

            int chunkSeed = ChunkSeed(cx, cy);
            var rng = new Random(chunkSeed);

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
                    DiscoveredByPlayer = false
                };

                AddOrReplaceDepositToChunk(chunk, d);
            }
        }

        public Deposit? TryGetDepositById(int depositId)
        {
            return _allDeposits.TryGetValue(depositId, out var d) ? d : null;
        }

        public List<ScanResult> Scan(
            ChunkManager chunks,
            int scanCenterTx,
            int scanCenterTy,
            int radiusTiles,
            int maxDepthMeters,
            int sizeNoiseTiers,
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

                        int dx = d.CenterTx - scanCenterTx;
                        int dy = d.CenterTy - scanCenterTy;
                        if (dx * dx + dy * dy > r2) continue;

                        // Signal strength (per your architecture)
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
                            SignalBars = bars
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
            // Matches your tier table roughly (MVP: exact mapping not required yet)
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

            // Replace in chunk list (by id)
            for (int i = 0; i < chunk.Deposits.Count; i++)
            {
                if (chunk.Deposits[i].DepositId == d.DepositId)
                {
                    chunk.Deposits[i] = d;
                    return;
                }
            }

            chunk.Deposits.Add(d);
        }

    }
}
