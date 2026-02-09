namespace MineIt.Inventory
{
    /// <summary>
    /// MVP in-code ore metadata (later replaced by ores.json).
    /// Keep deterministic and stable.
    /// </summary>
    public static class OreCatalog
    {
        public static double UnitMassKg(string oreId)
        {
            // These are reasonable placeholders; tune later or move to JSON.
            return oreId switch
            {
                "scrap" => 1.0,
                "iron" => 1.2,
                "copper" => 1.0,
                "quartz" => 0.8,
                "alum" => 0.9,
                "lithium" => 0.6,
                "titanium" => 1.1,
                "cobalt" => 1.0,
                "neodym" => 0.7,
                "gold" => 1.5,
                "plat" => 1.6,
                "xenon" => 0.9,
                "aether" => 0.5,
                "artifact" => 0.9,
                _ => 1.0
            };
        }

        public static double ExtractionDifficulty(string oreId)
        {
            // Placeholder difficulty curve; later comes from data.
            return oreId switch
            {
                "scrap" => 1.0,
                "iron" => 1.1,
                "copper" => 1.0,
                "quartz" => 1.2,
                "alum" => 1.0,
                "lithium" => 1.3,
                "titanium" => 1.4,
                "cobalt" => 1.5,
                "neodym" => 1.6,
                "gold" => 1.4,
                "plat" => 1.6,
                "xenon" => 1.8,
                "aether" => 2.0,
                "artifact" => 2.0,
                _ => 1.0
            };
        }

        public static int BasePricePerUnit(string oreId)
        {
            return oreId switch
            {
                "scrap" => 1,
                "iron" => 4,
                "copper" => 5,
                "quartz" => 6,
                "alum" => 7,
                "lithium" => 12,
                "titanium" => 15,
                "cobalt" => 18,
                "neodym" => 25,
                "gold" => 35,
                "plat" => 50,
                "xenon" => 80,
                "aether" => 120,
                "artifact" => 0,
                _ => 1
            };
        }

    }
}
