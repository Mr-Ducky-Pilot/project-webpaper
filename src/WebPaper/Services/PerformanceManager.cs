using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WebPaper.Native;
using static WebPaper.Native.NativeMethods;
using CoreWebView2 = Microsoft.Web.WebView2.Core.CoreWebView2;

namespace WebPaper.Services
{
    /// <summary>
    /// Manages performance optimization including fullscreen detection and auto-pause
    /// </summary>
    public class PerformanceManager : IDisposable
    {
        private CoreWebView2? _webView;
        private Timer? _monitoringTimer;
        private bool _isPaused = false;
        private bool _disposed = false;

        // Performance metrics
        private DateTime _lastCheck = DateTime.Now;
        private int _pauseCount = 0;
        private int _resumeCount = 0;
        private TimeSpan _totalPausedTime = TimeSpan.Zero;
        private DateTime? _pausedAt = null;

        // Configuration
        private readonly int _checkIntervalMs = 2000; // Check every 2 seconds
        private readonly bool _enableBatteryOptimization = true;

        public bool IsPaused => _isPaused;
        public int PauseCount => _pauseCount;
        public int ResumeCount => _resumeCount;
        public TimeSpan TotalPausedTime => _totalPausedTime + (_pausedAt.HasValue ? (DateTime.Now - _pausedAt.Value) : TimeSpan.Zero);

        /// <summary>
        /// Event raised when wallpaper is paused
        /// </summary>
        public event EventHandler<string>? WallpaperPaused;

        /// <summary>
        /// Event raised when wallpaper is resumed
        /// </summary>
        public event EventHandler? WallpaperResumed;

        public void Initialize(CoreWebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));

            // Start monitoring
            _monitoringTimer = new Timer(
                MonitoringCallback,
                null,
                TimeSpan.FromSeconds(2), // Initial delay
                TimeSpan.FromMilliseconds(_checkIntervalMs)
            );

            Console.WriteLine("PerformanceManager: Initialized and monitoring started");
        }

        private async void MonitoringCallback(object? state)
        {
            try
            {
                await CheckAndAdjustPerformanceAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PerformanceManager ERROR: Monitoring failed - {ex.Message}");
            }
        }

        private async Task CheckAndAdjustPerformanceAsync()
        {
            if (_webView == null || _disposed)
                return;

            // Check for fullscreen applications
            bool shouldPause = ShouldPauseRendering();

            if (shouldPause && !_isPaused)
            {
                await PauseRenderingAsync("Fullscreen app detected");
            }
            else if (!shouldPause && _isPaused)
            {
                await ResumeRenderingAsync();
            }
        }

        /// <summary>
        /// Determines if rendering should be paused
        /// </summary>
        private bool ShouldPauseRendering()
        {
            try
            {
                // Check for fullscreen application
                if (IsFullscreenAppRunning())
                {
                    return true;
                }

                // Check battery status if optimization enabled
                if (_enableBatteryOptimization && IsOnBattery() && GetBatteryPercentage() < 20)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false; // Don't pause on errors
            }
        }

        /// <summary>
        /// Checks if a fullscreen application is currently running
        /// </summary>
        private bool IsFullscreenAppRunning()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return false;

                // Get window rectangle
                if (!GetWindowRect(foregroundWindow, out RECT rect))
                    return false;

                // Get the monitor the window is on
                var primaryWidth = GetSystemMetrics(SM_CXSCREEN);
                var primaryHeight = GetSystemMetrics(SM_CYSCREEN);

                // Check if window covers the entire screen
                bool isFullscreen =
                    rect.Left <= 0 &&
                    rect.Top <= 0 &&
                    rect.Width >= primaryWidth &&
                    rect.Height >= primaryHeight;

                // Additional check: exclude desktop and taskbar
                if (isFullscreen)
                {
                    var className = WorkerWManager.GetWindowClassName(foregroundWindow);

                    // Don't consider these as fullscreen
                    if (className.Contains("Progman") ||
                        className.Contains("WorkerW") ||
                        className.Contains("Shell_TrayWnd"))
                    {
                        return false;
                    }
                }

                return isFullscreen;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if system is running on battery
        /// </summary>
        private bool IsOnBattery()
        {
            try
            {
                var status = System.Windows.Forms.SystemInformation.PowerStatus;
                return status.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets current battery percentage
        /// </summary>
        private int GetBatteryPercentage()
        {
            try
            {
                var status = System.Windows.Forms.SystemInformation.PowerStatus;
                return (int)(status.BatteryLifePercent * 100);
            }
            catch
            {
                return 100; // Assume fully charged on error
            }
        }

        /// <summary>
        /// Pauses rendering to save resources
        /// </summary>
        private async Task PauseRenderingAsync(string reason)
        {
            if (_webView == null || _isPaused)
                return;

            try
            {
                // Pause media playback
                await _webView.ExecuteScriptAsync(@"
                    (function() {
                        document.querySelectorAll('video').forEach(v => v.pause());
                        document.querySelectorAll('audio').forEach(a => a.pause());
                    })();
                ");

                _isPaused = true;
                _pauseCount++;
                _pausedAt = DateTime.Now;

                Console.WriteLine($"PerformanceManager: Paused rendering - {reason}");
                WallpaperPaused?.Invoke(this, reason);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PerformanceManager ERROR: Failed to pause - {ex.Message}");
            }
        }

        /// <summary>
        /// Resumes rendering
        /// </summary>
        private async Task ResumeRenderingAsync()
        {
            if (_webView == null || !_isPaused)
                return;

            try
            {
                // Optionally resume media (user may not want this)
                // await _webView.ExecuteScriptAsync(@"
                //     (function() {
                //         document.querySelectorAll('video').forEach(v => v.play());
                //     })();
                // ");

                _isPaused = false;
                _resumeCount++;

                if (_pausedAt.HasValue)
                {
                    _totalPausedTime += DateTime.Now - _pausedAt.Value;
                    _pausedAt = null;
                }

                Console.WriteLine("PerformanceManager: Resumed rendering");
                WallpaperResumed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PerformanceManager ERROR: Failed to resume - {ex.Message}");
            }
        }

        /// <summary>
        /// Gets performance diagnostics
        /// </summary>
        public string GetDiagnostics()
        {
            var uptime = DateTime.Now - _lastCheck;
            var pausedPercent = TotalPausedTime.TotalSeconds / uptime.TotalSeconds * 100;

            return $"PerformanceManager Diagnostics:\n" +
                   $"  Status: {(_isPaused ? "PAUSED" : "ACTIVE")}\n" +
                   $"  Pause Count: {_pauseCount}\n" +
                   $"  Resume Count: {_resumeCount}\n" +
                   $"  Total Paused Time: {TotalPausedTime:hh\\:mm\\:ss}\n" +
                   $"  Paused Percentage: {pausedPercent:F1}%\n" +
                   $"  Battery Status: {(IsOnBattery() ? $"On Battery ({GetBatteryPercentage()}%)" : "Plugged In")}\n" +
                   $"  Fullscreen App: {(IsFullscreenAppRunning() ? "Yes" : "No")}";
        }

        /// <summary>
        /// Gets the width of the primary screen
        /// </summary>
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        public void Dispose()
        {
            if (!_disposed)
            {
                _monitoringTimer?.Dispose();
                _monitoringTimer = null;
                _webView = null;
                _disposed = true;

                Console.WriteLine("PerformanceManager: Disposed");
            }
        }
    }
}
