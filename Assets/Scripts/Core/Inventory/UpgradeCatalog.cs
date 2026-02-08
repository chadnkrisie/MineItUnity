namespace MineIt.Inventory
{
    /// <summary>
    /// MVP upgrade pricing / capacities (later move into JSON or economy model).
    /// </summary>
    public static class UpgradeCatalog
    {
        public static int DetectorPriceForTier(int tier)
        {
            return tier switch
            {
                1 => 200,
                2 => 500,
                3 => 1200,
                4 => 2500,
                5 => 5000,
                _ => 0
            };
        }

        public static int ExtractorPriceForTier(int tier)
        {
            return tier switch
            {
                1 => 200,
                2 => 600,
                3 => 1500,
                4 => 3200,
                5 => 7000,
                _ => 0
            };
        }

        public static int BackpackPriceForTier(int tier)
        {
            // MVP tuning values (easy to adjust)
            return tier switch
            {
                1 => 0,
                2 => 300,
                3 => 800,
                4 => 1500,
                5 => 2500,
                _ => 0
            };
        }

        public static double BackpackCapacityKgForTier(int tier)
        {
            // Locked from your architecture doc
            return tier switch
            {
                1 => 50.0,
                2 => 100.0,
                3 => 200.0,
                4 => 350.0,
                5 => 500.0,
                _ => 50.0
            };
        }
    }
}
