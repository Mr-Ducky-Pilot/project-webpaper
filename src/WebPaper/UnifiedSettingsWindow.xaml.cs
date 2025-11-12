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

            SetWindowSize(750, 850);
            LoadSettings();
            LoadMonitorInfo();
            LoadVersionInfo();
        }

        private void SetWindowSize(int width, int height)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow?.Resize(new SizeInt32 { Width = width, Height = height });
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
                if (config.ControlMode == ControlMode.WebPaperControl)
                {
                    WebPaperControlRadio.IsChecked = true;
                }
                else
                {
                    DesktopControlRadio.IsChecked = true;
                }

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

        private void LoadVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                VersionText.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";
            }
            catch
            {
                VersionText.Text = "Version 1.0.0";
            }
        }

        private void LoadMonitorInfo()
        {
            try
            {
                var monitors = GetMonitorInformation();
                var sb = new StringBuilder();

                sb.AppendLine($"🖥️ {monitors.Count} monitor(s) detected");
                sb.AppendLine();

                for (int i = 0; i < monitors.Count; i++)
                {
                    var monitor = monitors[i];
                    sb.AppendLine($"Monitor {i + 1}:");
                    sb.AppendLine($"  • Resolution: {monitor.Width} x {monitor.Height}");
                    sb.AppendLine($"  • Position: ({monitor.Left}, {monitor.Top})");
                    sb.AppendLine($"  • {(monitor.IsPrimary ? "✓ Primary Monitor (WebPaper loads here)" : "Secondary Monitor")}");
                    if (i < monitors.Count - 1) sb.AppendLine();
                }

                MonitorInfoText.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load monitor info");
                MonitorInfoText.Text = "Unable to load monitor information";
            }
        }

        private List<MonitorInfo> GetMonitorInformation()
        {
            var monitors = new List<MonitorInfo>();

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
                {
                    var mi = new NativeMethods.MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(mi);

                    if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                    {
                        monitors.Add(new MonitorInfo
                        {
                            Width = mi.rcMonitor.right - mi.rcMonitor.left,
                            Height = mi.rcMonitor.bottom - mi.rcMonitor.top,
                            Left = mi.rcMonitor.left,
                            Top = mi.rcMonitor.top,
                            IsPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0
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
                var newUrl = HomeUrlTextBox.Text?.Trim();

                if (string.IsNullOrWhiteSpace(newUrl))
                {
                    ShowError("Please enter a valid URL");
                    return;
                }

                if (!Uri.TryCreate(newUrl, UriKind.Absolute, out Uri? uri) ||
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    ShowError("Please enter a valid HTTP or HTTPS URL");
                    return;
                }

                if (_getConfig == null || _onConfigChanged == null) return;

                var config = _getConfig();
                config.WallpaperUrl = newUrl;
                _onConfigChanged(config);

                // Request refresh
                _onRefreshRequested?.Invoke();

                Log.Information($"Home URL updated to: {newUrl}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply URL");
                ShowError("Failed to apply URL: " + ex.Message);
            }
        }

        private void ControlModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_getConfig == null || _onConfigChanged == null) return;

                var config = _getConfig();
                config.ControlMode = WebPaperControlRadio.IsChecked == true
                    ? ControlMode.WebPaperControl
                    : ControlMode.DesktopControl;

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
                var value = (int)e.NewValue;
                BatteryValueText.Text = $"{value}%";

                if (_getConfig == null || _onConfigChanged == null) return;

                var config = _getConfig();
                config.BatteryPauseThreshold = value;
                _onConfigChanged(config);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to change battery threshold");
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
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenUrl(string url)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error opening URL");
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

        private class MonitorInfo
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int Left { get; set; }
            public int Top { get; set; }
            public bool IsPrimary { get; set; }
        }

        // P/Invoke for monitor detection
        private static class NativeMethods
        {
            public const int MONITORINFOF_PRIMARY = 0x00000001;

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public struct MONITORINFOEX
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public int dwFlags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string szDevice;
            }

            public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

            [DllImport("user32.dll")]
            public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
        }
    }
}
