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

                // Load performance settings
                PerformanceToggle.IsOn = config.PerformanceOptimizationEnabled;
                BatterySlider.Value = config.BatteryPauseThreshold;
                BatteryValueText.Text = $"{config.BatteryPauseThreshold}%";

                // Check context menu installation status
                UpdateContextMenuStatus();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load settings");
            }
        }

        /// <summary>
        /// Updates the context menu installation status display
        /// </summary>
        private void UpdateContextMenuStatus()
        {
            try
            {
                var contextMenuManager = new Services.ContextMenuManager();
                bool isInstalled = contextMenuManager.IsContextMenuInstalled();

                ContextMenuStatusText.Text = isInstalled
                    ? "Status: Installed ✓"
                    : "Status: Not installed";

                InstallContextMenuButton.IsEnabled = !isInstalled;
                UninstallContextMenuButton.IsEnabled = isInstalled;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check context menu status");
                ContextMenuStatusText.Text = "Status: Unknown";
            }
        }

        private void LoadMonitorInformation()
        {
            try
            {
                // Use new MonitorManager service
                var monitorManager = new Services.MonitorManager();
                var monitors = monitorManager.GetAllMonitors();
                var sb = new StringBuilder();

                sb.AppendLine($"{monitors.Count} monitor(s) detected");
                sb.AppendLine();

                for (int i = 0; i < monitors.Count; i++)
                {
                    var monitor = monitors[i];
                    sb.AppendLine($"Monitor {i + 1}{(monitor.IsPrimary ? " (Primary)" : "")}:");
                    sb.AppendLine($"  Resolution: {monitor.Width} x {monitor.Height}");
                    sb.AppendLine($"  Position: ({monitor.Left}, {monitor.Top})");
                    sb.AppendLine($"  Device: {monitor.DeviceName}");
                    if (i < monitors.Count - 1) sb.AppendLine();
                }

                MonitorInfoText.Text = sb.ToString();

                // Populate monitor selection ComboBox
                LoadMonitorSelection(monitors);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load monitor information");
                MonitorInfoText.Text = "Failed to retrieve monitor information";
            }
        }

        private void LoadMonitorSelection(List<Services.MonitorManager.MonitorInfo> monitors)
        {
            try
            {
                if (MonitorSelectionComboBox == null) return;

                MonitorSelectionComboBox.Items.Clear();

                foreach (var monitor in monitors)
                {
                    string displayName = monitor.IsPrimary
                        ? $"Monitor {monitor.Index + 1} (Primary) - {monitor.Width}x{monitor.Height}"
                        : $"Monitor {monitor.Index + 1} - {monitor.Width}x{monitor.Height}";

                    MonitorSelectionComboBox.Items.Add(displayName);
                }

                // Select current monitor from config
                if (_getConfig != null)
                {
                    var config = _getConfig();
                    int selectedIndex = config.PreferredMonitorIndex;

                    // Validate index is within range
                    if (selectedIndex >= 0 && selectedIndex < monitors.Count)
                    {
                        MonitorSelectionComboBox.SelectedIndex = selectedIndex;
                    }
                    else
                    {
                        MonitorSelectionComboBox.SelectedIndex = 0; // Default to primary
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load monitor selection");
            }
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

        /// <summary>
        /// Handles monitor selection change
        /// </summary>
        private void MonitorSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_getConfig == null || _onConfigChanged == null) return;
                if (MonitorSelectionComboBox.SelectedIndex < 0) return;

                var config = _getConfig();
                config.PreferredMonitorIndex = MonitorSelectionComboBox.SelectedIndex;
                _onConfigChanged(config);

                Log.Information($"Monitor selection changed to index: {MonitorSelectionComboBox.SelectedIndex}");

                // Show restart button and note
                RestartAppButton.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                MonitorChangeNote.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update monitor selection");
            }
        }

        /// <summary>
        /// Handles restart app button click - restarts WebPaper
        /// </summary>
        private void RestartAppButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Information("User requested app restart for monitor change");

                // Get the current executable path
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

                if (!string.IsNullOrEmpty(exePath))
                {
                    // Start a new instance with --restart flag (bypasses single-instance check)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "--restart",
                        UseShellExecute = true
                    });

                    // Exit current instance
                    Microsoft.UI.Xaml.Application.Current.Exit();
                }
                else
                {
                    Log.Error("Could not determine executable path for restart");
                    ShowError("Could not restart WebPaper. Please restart manually.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restart app");
                ShowError($"Failed to restart: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles install context menu button click
        /// </summary>
        private void InstallContextMenuButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if already running as administrator
                if (!Services.ContextMenuManager.IsAdministrator())
                {
                    // Show info and restart as admin
                    var result = Native.NativeMethods.MessageBox(
                        IntPtr.Zero,
                        "Installing the desktop context menu requires administrator privileges.\n\nWebPaper will restart with administrator rights. Continue?",
                        "WebPaper - Administrator Required",
                        Native.NativeMethods.MB_YESNO | Native.NativeMethods.MB_ICONQUESTION
                    );

                    if (result == Native.NativeMethods.IDYES)
                    {
                        // Restart as administrator with install flag
                        if (Services.ContextMenuManager.RestartAsAdministrator("--install-context-menu"))
                        {
                            // Close current window
                            this.Close();
                        }
                    }
                }
                else
                {
                    // Already admin, install directly
                    var contextMenuManager = new Services.ContextMenuManager();
                    if (contextMenuManager.InstallContextMenu())
                    {
                        UpdateContextMenuStatus();
                        Native.NativeMethods.MessageBox(
                            IntPtr.Zero,
                            "WebPaper has been added to your desktop right-click menu!",
                            "WebPaper - Context Menu Installed",
                            Native.NativeMethods.MB_OK | Native.NativeMethods.MB_ICONINFORMATION
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to install context menu");
                ShowError("Failed to install context menu: " + ex.Message);
            }
        }

        /// <summary>
        /// Handles uninstall context menu button click
        /// </summary>
        private void UninstallContextMenuButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if already running as administrator
                if (!Services.ContextMenuManager.IsAdministrator())
                {
                    // Show info and restart as admin
                    var result = Native.NativeMethods.MessageBox(
                        IntPtr.Zero,
                        "Uninstalling the desktop context menu requires administrator privileges.\n\nWebPaper will restart with administrator rights. Continue?",
                        "WebPaper - Administrator Required",
                        Native.NativeMethods.MB_YESNO | Native.NativeMethods.MB_ICONQUESTION
                    );

                    if (result == Native.NativeMethods.IDYES)
                    {
                        // Restart as administrator with uninstall flag
                        if (Services.ContextMenuManager.RestartAsAdministrator("--uninstall-context-menu"))
                        {
                            // Close current window
                            this.Close();
                        }
                    }
                }
                else
                {
                    // Already admin, uninstall directly
                    var contextMenuManager = new Services.ContextMenuManager();
                    if (contextMenuManager.UninstallContextMenu())
                    {
                        UpdateContextMenuStatus();
                        Native.NativeMethods.MessageBox(
                            IntPtr.Zero,
                            "WebPaper has been removed from your desktop right-click menu.",
                            "WebPaper - Context Menu Uninstalled",
                            Native.NativeMethods.MB_OK | Native.NativeMethods.MB_ICONINFORMATION
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to uninstall context menu");
                ShowError("Failed to uninstall context menu: " + ex.Message);
            }
        }
    }
}
