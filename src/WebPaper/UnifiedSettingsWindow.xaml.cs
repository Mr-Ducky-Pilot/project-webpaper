using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;
using WebPaper.Models;
using Windows.Graphics;
using WinRT.Interop;

namespace WebPaper
{
    public sealed partial class UnifiedSettingsWindow : Window
    {
        private readonly Action<AppConfig>? _onConfigChanged;
        private readonly Func<AppConfig>? _getConfig;
        private readonly Action? _onExitApp;
        private readonly Action? _onRefreshRequested;

        public UnifiedSettingsWindow(
            Func<AppConfig> getConfig,
            Action<AppConfig> onConfigChanged,
            Action onExitApp,
            Action onRefreshRequested)
        {
            this.InitializeComponent();
            _getConfig = getConfig;
            _onConfigChanged = onConfigChanged;
            _onExitApp = onExitApp;
            _onRefreshRequested = onRefreshRequested;

            // Set window size
            SetWindowSize(600, 800);

            // Load current settings
            LoadSettings();

            // Load monitor information
            LoadMonitorInformation();

            // Set version
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                VersionText.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";
            }
            catch
            {
                VersionText.Text = "Version 1.0.0";
            }
        }

        private void SetWindowSize(int width, int height)
        {
            try
            {
                var hWnd = WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow != null)
                {
                    appWindow.Resize(new SizeInt32 { Width = width, Height = height });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set window size");
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (_getConfig == null) return;

                var config = _getConfig();

                // Load home URL
                HomeUrlTextBox.Text = config.WallpaperUrl ?? "https://blink42.com";

                // Load control mode
                // Off = WebPaperControl (default), On = DesktopControl
                ControlModeToggle.IsOn = config.ControlMode == ControlMode.DesktopControl;

                // Load performance settings
                PerformanceToggle.IsOn = config.PerformanceOptimizationEnabled;
                BatterySlider.Value = config.BatteryPauseThreshold;
                BatteryValueText.Text = $"{config.BatteryPauseThreshold}%";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load settings");
            }
        }

        private void LoadMonitorInformation()
        {
            try
            {
                var monitors = GetMonitorInformation();
                var sb = new StringBuilder();

                sb.AppendLine($"{monitors.Count} monitor(s) detected");
                sb.AppendLine();

                for (int i = 0; i < monitors.Count; i++)
                {
                    var monitor = monitors[i];
                    sb.AppendLine($"Monitor {i + 1}{(monitor.IsPrimary ? " (Primary)" : "")}:");
                    sb.AppendLine($"  Resolution: {monitor.Width} x {monitor.Height}");
                    sb.AppendLine($"  Position: ({monitor.Left}, {monitor.Top})");
                    if (i < monitors.Count - 1) sb.AppendLine();
                }

                MonitorInfoText.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load monitor information");
                MonitorInfoText.Text = "Failed to retrieve monitor information";
            }
        }

        private List<MonitorInfo> GetMonitorInformation()
        {
            var monitors = new List<MonitorInfo>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
                {
                    MONITORINFO mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(mi);

                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        monitors.Add(new MonitorInfo
                        {
                            Left = mi.rcMonitor.left,
                            Top = mi.rcMonitor.top,
                            Width = mi.rcMonitor.right - mi.rcMonitor.left,
                            Height = mi.rcMonitor.bottom - mi.rcMonitor.top,
                            IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0
                        });
                    }

                    return true;
                }, IntPtr.Zero);

            return monitors;
        }

        private void ApplyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_getConfig == null || _onConfigChanged == null) return;

                var url = HomeUrlTextBox.Text?.Trim();
                if (string.IsNullOrEmpty(url))
                {
                    ShowError("Please enter a valid URL");
                    return;
                }

                // Ensure URL has protocol
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }

                var config = _getConfig();
                config.WallpaperUrl = url;
                _onConfigChanged(config);

                // Trigger refresh
                _onRefreshRequested?.Invoke();

                Log.Information($"Home URL updated to: {url}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply URL");
                ShowError("Failed to apply URL: " + ex.Message);
            }
        }

        private void ControlModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_getConfig == null || _onConfigChanged == null) return;

                var config = _getConfig();
                // Off = WebPaperControl, On = DesktopControl
                config.ControlMode = ControlModeToggle.IsOn
                    ? ControlMode.DesktopControl
                    : ControlMode.WebPaperControl;

                _onConfigChanged(config);

                Log.Information($"Control mode changed to: {config.ControlMode}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to change control mode");
            }
        }

        private void PerformanceToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_getConfig == null || _onConfigChanged == null) return;

                var config = _getConfig();
                config.PerformanceOptimizationEnabled = PerformanceToggle.IsOn;
                _onConfigChanged(config);

                Log.Information($"Performance optimization: {(PerformanceToggle.IsOn ? "Enabled" : "Disabled")}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to toggle performance");
            }
        }

        private void BatterySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            try
            {
                if (BatteryValueText == null) return; // Not initialized yet

                int value = (int)e.NewValue;
                BatteryValueText.Text = $"{value}%";

                if (_getConfig == null || _onConfigChanged == null) return;

                var config = _getConfig();
                config.BatteryPauseThreshold = value;
                _onConfigChanged(config);

                Log.Information($"Battery threshold updated to: {value}%");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update battery threshold");
            }
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/Mr-Ducky-Pilot/project-webpaper");
        }

        private void ReportBugButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/Mr-Ducky-Pilot/project-webpaper/issues/new?template=bug_report.md");
        }

        private void LinkedInButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.linkedin.com/in/omprakash-jat/");
        }

        private void ExitAppButton_Click(object sender, RoutedEventArgs e)
        {
            _onExitApp?.Invoke();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to open URL: {url}");
            }
        }

        private async void ShowError(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        // P/Invoke for monitor detection
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const int MONITORINFOF_PRIMARY = 0x00000001;

        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private class MonitorInfo
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsPrimary { get; set; }
        }
    }
}
