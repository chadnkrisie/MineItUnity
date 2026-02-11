namespace MineIt.Mining
{
    public sealed class ScanResult
    {
        public int DepositId { get; set; }
        public string OreTypeId { get; set; } = "";
        public int CenterTx { get; set; }
        public int CenterTy { get; set; }

        // Intelligence fields (NEW)
        public int DistanceTiles { get; set; }
        public bool IsArtifact { get; set; }
        public bool ClaimedByPlayer { get; set; }
        public int ClaimedByNpcId { get; set; } // -1 if none
        public bool IsDepleted { get; set; }

        // Depletion ETA (seconds). -1 = unknown/not applicable.
        public double DepletionEtaSeconds { get; set; } = -1.0;

        // Tags (computed post-scan by GameSession)
        public bool TagNpc { get; set; }
        public bool TagDeep { get; set; }
        public bool TagUrgent { get; set; }
        public bool TagWarning { get; set; }
        public bool TagArtifact { get; set; }
        public bool TagDepleted { get; set; }


        public int TrueSizeTier { get; set; }
        public int EstimatedSizeTier { get; set; }
        public string EstimatedSizeClass { get; set; } = "Unknown";

        public int DepthMeters { get; set; }
        public int SignalBars { get; set; }

        /// <summary>
        /// Computes a priority score for decision-making and UI sorting.
        /// Higher score = higher urgency / value.
        /// This does NOT affect simulation; UI-only guidance.
        /// </summary>
        public double ComputePriorityScore()
        {
            // Locked (architecture v3.x):
            // ValueDensity = OreValuePerUnit / OreKgPerUnit  (credits/kg)
            // vd = clamp01(ValueDensity / 50)
            // sd = clamp01(EstimatedSizeTier / 15)
            // dd = 1 / (1 + DistanceTiles/30)
            // PriorityScore = (0.55vd + 0.45sd) * dd
            //
            // Artifacts: always above non-artifacts (handled at sort call site; still give them a boost).
            double kg = MineIt.Inventory.OreCatalog.UnitMassKg(OreTypeId);
            if (kg <= 1e-9) kg = 1.0;

            double value = MineIt.Inventory.OreCatalog.BasePricePerUnit(OreTypeId);
            double valueDensity = value / kg; // credits per kg

            double vd = Clamp01(valueDensity / 50.0);
            double sd = Clamp01(EstimatedSizeTier / 15.0);
            double dd = 1.0 / (1.0 + (DistanceTiles / 30.0));

            double score = (0.55 * vd + 0.45 * sd) * dd;

            // Mild nudge: urgent/warning sites get a little boost (does not override artifact rule).
            if (TagUrgent) score *= 1.15;
            else if (TagWarning) score *= 1.08;

            // Depleted should sink.
            if (TagDepleted) score *= 0.1;

            // Artifact gets a modest boost; final artifact precedence is enforced during sorting.
            if (IsArtifact) score *= 1.25;

            return score;
        }

        private static double Clamp01(double x)
        {
            if (x <= 0) return 0;
            if (x >= 1) return 1;
            return x;
        }

    }
}
