using System;

namespace WebPaper.Models
{
    /// <summary>
    /// Application configuration model
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Version of the configuration file format
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// URL to display as wallpaper
        /// </summary>
        public string WallpaperUrl { get; set; } = "https://www.example.com";

        /// <summary>
        /// Whether to start WebPaper when Windows starts
        /// </summary>
        public bool AutoStartEnabled { get; set; } = false;

        /// <summary>
        /// Whether to minimize to system tray instead of taskbar
        /// </summary>
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>
        /// Whether performance optimization is enabled
        /// </summary>
        public bool PerformanceOptimizationEnabled { get; set; } = true;

        /// <summary>
        /// Battery percentage threshold for auto-pause (0-100)
        /// </summary>
        public int BatteryPauseThreshold { get; set; } = 20;

        /// <summary>
        /// Whether to show notification when wallpaper is paused/resumed
        /// </summary>
        public bool ShowPauseNotifications { get; set; } = false;

        /// <summary>
        /// Whether this is the first time running WebPaper
        /// </summary>
        public bool IsFirstRun { get; set; } = true;

        /// <summary>
        /// Last time the application was launched
        /// </summary>
        public DateTime? LastLaunchDate { get; set; }

        /// <summary>
        /// Creates a default configuration
        /// </summary>
        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                WallpaperUrl = "https://www.example.com",
                AutoStartEnabled = false,
                MinimizeToTray = true,
                PerformanceOptimizationEnabled = true,
                BatteryPauseThreshold = 20,
                ShowPauseNotifications = false,
                IsFirstRun = true,
                LastLaunchDate = null
            };
        }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public bool Validate()
        {
            // Validate URL
            if (string.IsNullOrWhiteSpace(WallpaperUrl))
                return false;

            if (!Uri.TryCreate(WallpaperUrl, UriKind.Absolute, out Uri? uri))
                return false;

            if (uri.Scheme != "http" && uri.Scheme != "https")
                return false;

            // Validate battery threshold
            if (BatteryPauseThreshold < 0 || BatteryPauseThreshold > 100)
                return false;

            return true;
        }
    }
}
