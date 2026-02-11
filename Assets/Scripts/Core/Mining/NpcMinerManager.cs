using System;
using System.Collections.Generic;
using MineIt.Inventory;
using MineIt.World;

namespace MineIt.Mining
{
    /// <summary>
    /// Phase 1 NPC miners (headless simulation):
    /// - No movement / no pathfinding
    /// - Claim + extract deposits over time
    /// - Deterministic based on seed and internal RNG
    /// - Operates only on deposits that exist in DepositManager (i.e., populated chunks)
    /// </summary>
    public sealed class NpcMinerManager
    {
        public sealed class NpcMiner
        {
            public int NpcId;
            public int Tier; // 1..5 (simple skill/perf scalar)

            // Current target deposit id (0 = none)
            public int TargetDepositId;

            // Time until next decision attempt (seconds)
            public double DecisionCooldownRemainingSeconds;

            // Deterministic per-NPC stagger
            public double DecisionCooldownMinSeconds;
            public double DecisionCooldownMaxSeconds;

            // Extract carry (kg) to avoid fractional loss like player extractor
            public double ExtractKgCarry;
        }

        private readonly int _seed;
        private readonly Random _rng;
        private readonly List<NpcMiner> _npcs = new List<NpcMiner>();

        public IReadOnlyList<NpcMiner> Npcs => _npcs;

        public int NpcCount => _npcs.Count;

        /// <summary>
        /// Called by GameSession afterglow scheduler:
        /// Attempts to claim the deposit for the specified NPC and immediately start extraction.
        /// Returns true only if the claim succeeded.
        /// </summary>
        public bool TryClaimAndStartExtraction(int npcId, int depositId, DepositManager deposits)
        {
            if (deposits == null) return false;
            if (depositId == 0) return false;

            // Find NPC
            NpcMiner npc = null;
            for (int i = 0; i < _npcs.Count; i++)
            {
                if (_npcs[i].NpcId == npcId)
                {
                    npc = _npcs[i];
                    break;
                }
            }
            if (npc == null) return false;

            var d = deposits.TryGetDepositById(depositId);
            if (d == null) return false;

            // Eligibility (locked rules)
            if (d.RemainingUnits <= 0) return false;
            if (d.IsArtifact) return false;           // NPC never claim artifacts
            if (d.ClaimedByPlayer) return false;
            if (d.ClaimedByNpcId.HasValue) return false;

            // Claim
            d.ClaimedByNpcId = npc.NpcId;

            // Start extraction
            npc.TargetDepositId = depositId;
            npc.ExtractKgCarry = 0.0;

            return true;
        }


        public NpcMinerManager(int seed)
        {
            _seed = seed;
            _rng = new Random(seed ^ 0x19C4A33D);
        }

        public void InitializeMvpNpcSet(int count)
        {
            _npcs.Clear();

            count = Math.Max(0, count);

            for (int i = 0; i < count; i++)
            {
                // Deterministic tier assignment (simple)
                int tier = 1 + (i % 5);

                // Stagger decisions so they don't all spike same tick
                double min = 3.0;
                double max = 6.0;

                var npc = new NpcMiner
                {
                    NpcId = i + 1, // 1..N
                    Tier = tier,
                    TargetDepositId = 0,
                    DecisionCooldownMinSeconds = min,
                    DecisionCooldownMaxSeconds = max,
                    DecisionCooldownRemainingSeconds = NextDecisionDelay(min, max),
                    ExtractKgCarry = 0.0
                };

                _npcs.Add(npc);
            }
        }

        public void Tick(double dtSeconds, DepositManager deposits)
        {
            if (dtSeconds <= 0) return;
            if (deposits == null) return;
            if (_npcs.Count == 0) return;

            for (int i = 0; i < _npcs.Count; i++)
            {
                var npc = _npcs[i];

                // Afterglow system assigns targets externally.
                // If the NPC has a target, it extracts; otherwise it idles.
                if (npc.TargetDepositId != 0)
                    TickExtraction(dtSeconds, deposits, npc);
            }
        }

        private void TickDecision(double dtSeconds, DepositManager deposits, NpcMiner npc)
        {
            npc.DecisionCooldownRemainingSeconds -= dtSeconds;
            if (npc.DecisionCooldownRemainingSeconds > 0)
                return;

            npc.DecisionCooldownRemainingSeconds = NextDecisionDelay(npc.DecisionCooldownMinSeconds, npc.DecisionCooldownMaxSeconds);

            // Attempt to claim a deposit.
            // Phase 1 rule: choose "best" available among known deposits:
            // - unclaimed
            // - has remaining units
            // - not player-claimed
            // Choose by a simple score: (rarer/deeper slightly preferred + larger preferred)
            int bestId = 0;
            double bestScore = double.NegativeInfinity;

            foreach (var d in deposits.GetAllDeposits())
            {
                if (d == null) continue;
                if (d.RemainingUnits <= 0) continue;
                if (d.IsArtifact) continue; // locked fairness rule: NPCs never claim artifact deposits

                if (d.ClaimedByPlayer) continue;
                if (d.ClaimedByNpcId.HasValue) continue;

                // Prefer larger and deeper slightly (creates pressure on valuable deposits)
                double score = 0.0;
                score += d.SizeTier * 1.0;
                score += (d.DepthMeters / 100.0) * 0.25;

                // Small deterministic jitter so ties break without bias to iteration order
                score += (_rng.NextDouble() * 0.05);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = d.DepositId;
                }
            }

            if (bestId == 0)
                return;

            var target = deposits.TryGetDepositById(bestId);
            if (target == null) return;

            // Re-check eligibility and claim
            if (target.RemainingUnits <= 0) return;
            if (target.ClaimedByPlayer) return;
            if (target.ClaimedByNpcId.HasValue) return;

            target.ClaimedByNpcId = npc.NpcId;
            // DEBUG: force discovery so NPC activity is visible on map
            //target.DiscoveredByPlayer = true;

            npc.TargetDepositId = bestId;
            npc.ExtractKgCarry = 0.0;

        }

        private void TickExtraction(double dtSeconds, DepositManager deposits, NpcMiner npc)
        {
            var d = deposits.TryGetDepositById(npc.TargetDepositId);
            if (d == null)
            {
                npc.TargetDepositId = 0;
                npc.ExtractKgCarry = 0.0;
                return;
            }

            // Safety: NPCs should never extract artifacts
            if (d.IsArtifact)
            {
                npc.TargetDepositId = 0;
                npc.ExtractKgCarry = 0.0;
                return;
            }

            // If deposit is depleted or ownership changed, release target
            if (d.RemainingUnits <= 0)
            {
                d.RemainingUnits = 0;
                d.IsDepleted = true;
                npc.TargetDepositId = 0;
                npc.ExtractKgCarry = 0.0;
                return;
            }

            if (d.ClaimedByPlayer)
            {
                // Player owns it now (future-proof: should not happen in Phase 1)
                npc.TargetDepositId = 0;
                npc.ExtractKgCarry = 0.0;
                return;
            }

            if (!d.ClaimedByNpcId.HasValue || d.ClaimedByNpcId.Value != npc.NpcId)
            {
                // Lost ownership
                npc.TargetDepositId = 0;
                npc.ExtractKgCarry = 0.0;
                return;
            }

            // Extract rate: slower than player, scaled by tier, affected by ore difficulty
            double baseRateKgPerSec = 0.25 + (npc.Tier - 1) * 0.20; // Tier1=0.25, Tier5=1.05
            double diff = OreCatalog.ExtractionDifficulty(d.OreTypeId);
            if (diff < 1e-6) diff = 1.0;

            double rateKgPerSec = baseRateKgPerSec / diff;
            if (rateKgPerSec < 1e-6) rateKgPerSec = 1e-6;

            double kgThisTick = rateKgPerSec * dtSeconds;

            double unitKg = OreCatalog.UnitMassKg(d.OreTypeId);
            if (unitKg < 1e-6) unitKg = 1.0;

            npc.ExtractKgCarry += kgThisTick;

            int unitsPotential = (int)Math.Floor(npc.ExtractKgCarry / unitKg);
            if (unitsPotential <= 0)
                return;

            int unitsByDeposit = Math.Min(unitsPotential, d.RemainingUnits);
            if (unitsByDeposit <= 0)
                return;

            d.RemainingUnits -= unitsByDeposit;

            npc.ExtractKgCarry -= unitsByDeposit * unitKg;
            if (npc.ExtractKgCarry < 0) npc.ExtractKgCarry = 0;

            if (d.RemainingUnits <= 0)
            {
                d.RemainingUnits = 0;
                d.IsDepleted = true;

                npc.TargetDepositId = 0;
                npc.ExtractKgCarry = 0.0;
            }
        }

        private double NextDecisionDelay(double min, double max)
        {
            if (max <= min) return Math.Max(0.1, min);
            return min + _rng.NextDouble() * (max - min);
        }

        public double GetNpcExtractionRateKgPerSec(NpcMiner npc, string oreTypeId)
        {
            if (npc == null) return 0.0;

            double baseRateKgPerSec = 0.25 + (npc.Tier - 1) * 0.20;
            double diff = Inventory.OreCatalog.ExtractionDifficulty(oreTypeId);
            if (diff < 1e-6) diff = 1.0;

            return baseRateKgPerSec / diff;
        }

        // ===== Save/Load support =====

        public void LoadFromSave(List<MineIt.Save.NpcMinerSaveData> list)
        {
            if (list == null) return;

            _npcs.Clear();

            for (int i = 0; i < list.Count; i++)
            {
                var sd = list[i];
                if (sd == null) continue;

                var npc = new NpcMiner
                {
                    NpcId = sd.NpcId,
                    Tier = sd.Tier,
                    TargetDepositId = sd.TargetDepositId,
                    DecisionCooldownRemainingSeconds = sd.DecisionCooldownRemainingSeconds,
                    DecisionCooldownMinSeconds = sd.DecisionCooldownMinSeconds,
                    DecisionCooldownMaxSeconds = sd.DecisionCooldownMaxSeconds,
                    ExtractKgCarry = sd.ExtractKgCarry
                };

                _npcs.Add(npc);
            }
        }

        public void SaveTo(List<MineIt.Save.NpcMinerSaveData> outList)
        {
            if (outList == null) return;

            outList.Clear();

            for (int i = 0; i < _npcs.Count; i++)
            {
                var npc = _npcs[i];
                outList.Add(new MineIt.Save.NpcMinerSaveData
                {
                    NpcId = npc.NpcId,
                    Tier = npc.Tier,
                    TargetDepositId = npc.TargetDepositId,
                    DecisionCooldownRemainingSeconds = npc.DecisionCooldownRemainingSeconds,
                    DecisionCooldownMinSeconds = npc.DecisionCooldownMinSeconds,
                    DecisionCooldownMaxSeconds = npc.DecisionCooldownMaxSeconds,
                    ExtractKgCarry = npc.ExtractKgCarry
                });
            }
        }
    }
}
