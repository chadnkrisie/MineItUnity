namespace MineIt.Models
{
    public sealed class PlayerState
    {
        // World coordinates in tile units (smooth movement).
        public double PositionX { get; set; }
        public double PositionY { get; set; }

        public double MoveSpeedTilesPerSec { get; set; } = 6.0;

        // MVP detector (Tier 1 defaults)
        public int DetectorTier { get; set; } = 1;

        // MVP extractor (Tier 1 defaults) - NEW
        public int ExtractorTier { get; set; } = 1;

        public int ExtractorMaxDepthMeters =>
            ExtractorTier switch
            {
                1 => 100,
                2 => 200,
                3 => 350,
                4 => 500,
                5 => 650,
                _ => 100
            };

        public double ExtractorRateKgPerSec =>
            ExtractorTier switch
            {
                1 => 0.5,
                2 => 1.0,
                3 => 2.0,
                4 => 4.0,
                5 => 8.0,
                _ => 0.5
            };

        public int ExtractorRangeTiles =>
            ExtractorTier switch
            {
                1 => 3,
                2 => 3,
                3 => 4,
                4 => 4,
                5 => 5,
                _ => 3
            };

        public int DetectorMaxDepthMeters =>
            DetectorTier switch
            {
                1 => 100,
                2 => 200,
                3 => 350,
                4 => 500,
                5 => 650,
                _ => 100
            };

        public int DetectorRadiusTiles =>
            DetectorTier switch
            {
                1 => 4,
                2 => 6,
                3 => 8,
                4 => 10,
                5 => 12,
                _ => 4
            };

        // Locked cooldown table (seconds)
        public double DetectorCooldownSeconds =>
            DetectorTier switch
            {
                1 => 2.0,
                2 => 2.0,
                3 => 1.8,
                4 => 1.6,
                5 => 1.4,
                _ => 2.0
            };

        // Locked size estimate noise (tiers)
        public int DetectorSizeNoiseTiers =>
            DetectorTier switch
            {
                1 => 2,
                2 => 2,
                3 => 1,
                4 => 1,
                5 => 0,
                _ => 2
            };
    }
}
