namespace MineIt.Simulation
{
    public sealed class GameClock
    {
        // Real-time seconds accumulated by simulation.
        public double TotalRealSeconds { get; private set; }

        // Locked: 1 full in-game day = 24 minutes real time.
        // 24 minutes = 1440 real seconds.
        public const double RealSecondsPerInGameDay = 24.0 * 60.0;

        // In-game time = 24 hours => 1440 minutes.
        public double DayFraction01
        {
            get
            {
                double t = TotalRealSeconds % RealSecondsPerInGameDay;
                return t / RealSecondsPerInGameDay; // 0..1
            }
        }

        public double InGameHours => DayFraction01 * 24.0;

        // Simple night definition: night is [20:00..24:00) U [0:00..6:00)
        public bool IsNight
        {
            get
            {
                double h = InGameHours;
                return (h >= 20.0) || (h < 6.0);
            }
        }

        // Darkness strength 0..1 with ramps at dusk/dawn.
        // Dusk: 18->20 ramps up. Dawn: 6->8 ramps down.
        public double Darkness01
        {
            get
            {
                double h = InGameHours;

                if (h >= 20.0 || h < 6.0) return 1.0;

                if (h >= 18.0 && h < 20.0)
                {
                    // 18..20 => 0..1
                    return (h - 18.0) / 2.0;
                }

                if (h >= 6.0 && h < 8.0)
                {
                    // 6..8 => 1..0
                    return 1.0 - ((h - 6.0) / 2.0);
                }

                return 0.0;
            }
        }

        public void Advance(double dtSeconds)
        {
            if (dtSeconds <= 0) return;
            TotalRealSeconds += dtSeconds;
        }
    }
}
