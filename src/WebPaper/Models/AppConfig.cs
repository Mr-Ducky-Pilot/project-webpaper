using System;

namespace WebPaper.Models
{
    /// <summary>
    /// How the wallpaper is laid out across the user's monitors.
    /// </summary>
    public enum WallpaperMode
    {
        /// <summary>One wallpaper window on the monitor selected by <see cref="AppConfig.PreferredMonitorIndex"/>.</summary>
        SingleMonitor = 0,
        /// <summary>One wallpaper window per monitor (Lively-style "per display"). Each monitor renders the same URL independently.</summary>
        AllMonitors = 1,
    }

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
        public string WallpaperUrl { get; set; } = "https://blink42.com";

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
        /// Preferred monitor index for wallpaper display (0 = Primary, 1+ = Secondary monitors).
        /// Only used when <see cref="Mode"/> is <see cref="WallpaperMode.SingleMonitor"/>.
        /// </summary>
        public int PreferredMonitorIndex { get; set; } = 0;

        /// <summary>
        /// How the wallpaper is laid out across monitors.
        /// </summary>
        public WallpaperMode Mode { get; set; } = WallpaperMode.SingleMonitor;

        /// <summary>
        /// Creates a default configuration
        /// </summary>
        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                WallpaperUrl = "https://blink42.com",
                AutoStartEnabled = false,
                MinimizeToTray = true,
                PerformanceOptimizationEnabled = true,
                BatteryPauseThreshold = 20,
                ShowPauseNotifications = false,
                IsFirstRun = true,
                LastLaunchDate = null,
                PreferredMonitorIndex = 0 // Default to primary monitor
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
