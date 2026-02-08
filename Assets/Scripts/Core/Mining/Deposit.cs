namespace MineIt.Mining
{
    public sealed class Deposit
    {
        public int DepositId { get; set; }
        public string OreTypeId { get; set; } = "iron";
        public int CenterTx { get; set; }
        public int CenterTy { get; set; }

        public int DepthMeters { get; set; }
        public int SizeTier { get; set; }          // 1..15
        public int RemainingUnits { get; set; }

        public int? ClaimedByNpcId { get; set; }
        public bool ClaimedByPlayer { get; set; }

        public bool DiscoveredByPlayer { get; set; }
    }
}
