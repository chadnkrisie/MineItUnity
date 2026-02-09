namespace MineIt.Mining
{
    public sealed class Deposit
    {
        public int DepositId { get; internal set; }
        public string OreTypeId { get; internal set; } = "iron";
        public int CenterTx { get; internal set; }
        public int CenterTy { get; internal set; }

        public int DepthMeters { get; internal set; }
        public int SizeTier { get; internal set; }          // 1..15
        public int RemainingUnits { get; internal set; }

        public int? ClaimedByNpcId { get; internal set; }
        public bool ClaimedByPlayer { get; internal set; }

        public bool DiscoveredByPlayer { get; internal set; }

        /// <summary>
        /// Estimates time (in seconds) until this deposit is depleted,
        /// given an effective extraction rate (kg/sec).
        /// Returns double.PositiveInfinity if rate is zero or deposit already empty.
        /// </summary>
        public double EstimateSecondsToDeplete(double extractionRateKgPerSec)
        {
            if (RemainingUnits <= 0) return 0.0;
            if (extractionRateKgPerSec <= 1e-9) return double.PositiveInfinity;

            double unitKg = Inventory.OreCatalog.UnitMassKg(OreTypeId);
            if (unitKg <= 1e-9) unitKg = 1.0;

            double remainingKg = RemainingUnits * unitKg;
            return remainingKg / extractionRateKgPerSec;
        }

    }
}
