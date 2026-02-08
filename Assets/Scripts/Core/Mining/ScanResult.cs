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
    }
}
