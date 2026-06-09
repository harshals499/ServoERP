using System;

namespace HVAC_Pro_Desktop.Services
{
    public static class SimulationClock
    {
        public const int MaxSimulatedDays = 60;
        public const int RealSecondsPerSimulatedDay = 50;

        private static DateTime _baseDate = DateTime.Today;
        private static int _simulatedDay;

        public static DateTime Now
        {
            get { return _baseDate.Date.AddDays(_simulatedDay).Add(DateTime.Now.TimeOfDay); }
        }

        public static DateTime Today
        {
            get { return _baseDate.Date.AddDays(_simulatedDay); }
        }

        public static int SimulatedDay
        {
            get { return _simulatedDay; }
        }

        public static void Configure(DateTime baseDate, int simulatedDay)
        {
            _baseDate = baseDate == default(DateTime) ? DateTime.Today : baseDate.Date;
            _simulatedDay = Math.Max(0, Math.Min(MaxSimulatedDays, simulatedDay));
        }

        public static DateTime AdvanceOneDay()
        {
            _simulatedDay = Math.Min(MaxSimulatedDays, _simulatedDay + 1);
            return Today;
        }
    }
}
