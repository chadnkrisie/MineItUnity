namespace MineIt.Mining
{
    public sealed class ScanResult
    {
        public int DepositId { get; set; }
        public string OreTypeId { get; set; } = "";
        public int CenterTx { get; set; }
        public int CenterTy { get; set; }

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
            double score = 0.0;

            // Signal confidence (1..5)
            score += SignalBars * 10.0;

            // Size (estimated)
            score += EstimatedSizeTier * 2.0;

            // Depth penalty (shallower is better)
            score -= (DepthMeters / 50.0);

            return score;
        }

    }
}
