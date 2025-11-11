using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using System;
using System.Threading.Tasks;
using WebPaper.Models;
using WebPaper.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace WebPaper
{
    public sealed partial class SettingsWindow : Window
    {
        private readonly ConfigManager _configManager;
        private readonly CookieManager _cookieManager;
        private AppConfig _config;
        private bool _hasUnsavedChanges = false;

        public bool SettingsSaved { get; private set; } = false;

        public SettingsWindow(ConfigManager configManager, CookieManager cookieManager)
        {
            this.InitializeComponent();
            _configManager = configManager;
            _cookieManager = cookieManager;
            _config = _configManager.GetCurrentConfig() ?? AppConfig.CreateDefault();

            // Set window size
            SetWindowSize(600, 700);

            LoadSettings();
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
            catch
            {
                // Fallback - size will be default
            }
        }

        private async void LoadSettings()
        {
            try
            {
                // Load current config
                _config = await _configManager.LoadConfigAsync();

                // Populate UI
                UrlTextBox.Text = _config.WallpaperUrl;
                PerformanceToggle.IsOn = _config.PerformanceOptimizationEnabled;
                BatterySlider.Value = _config.BatteryPauseThreshold;
                NotificationsToggle.IsOn = _config.ShowPauseNotifications;
                AutoStartToggle.IsOn = _config.AutoStartEnabled;
                TrayToggle.IsOn = _config.MinimizeToTray;

                // Update performance settings visibility
                PerformanceSettings.Visibility = _config.PerformanceOptimizationEnabled
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                // Update cookie info
                await UpdateCookieInfo();

                // Show config file path
                ConfigPathText.Text = _configManager.GetConfigFilePath();

                StatusText.Text = "Settings loaded";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading settings: {ex.Message}";
                Console.WriteLine($"SettingsWindow: Error loading settings - {ex.Message}");
            }
        }

        private async Task UpdateCookieInfo()
        {
            var info = await _cookieManager.GetCookieInfoAsync();
            if (info.HasValue)
            {
                CookieInfoText.Text = $"{info.Value.count} cookies saved (last saved: {info.Value.savedAt:yyyy-MM-dd HH:mm})";
            }
            else
            {
                CookieInfoText.Text = "No saved cookies";
            }
        }

        private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _hasUnsavedChanges = true;

            // Validate URL
            var url = UrlTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                StatusText.Text = "URL cannot be empty";
                return;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                if (uri.Scheme == "http" || uri.Scheme == "https")
                {
                    StatusText.Text = "URL is valid";
                }
                else
                {
                    StatusText.Text = "URL must use http:// or https://";
                }
            }
            else
            {
                StatusText.Text = "Invalid URL format";
            }
        }

        private void PerformanceToggle_Toggled(object sender, RoutedEventArgs e)
        {
            _hasUnsavedChanges = true;
            PerformanceSettings.Visibility = PerformanceToggle.IsOn
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BatterySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            _hasUnsavedChanges = true;
            BatteryValueText.Text = $"{(int)e.NewValue}%";
        }

        private void NotificationsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            _hasUnsavedChanges = true;
        }

        private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
        {
            _hasUnsavedChanges = true;
        }

        private void TrayToggle_Toggled(object sender, RoutedEventArgs e)
        {
            _hasUnsavedChanges = true;
        }

        private void SuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                UrlTextBox.Text = url;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = UrlTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    StatusText.Text = "Please enter a URL first";
                    return;
                }

                // Open login helper window
                var loginWindow = new LoginHelperWindow(url, _cookieManager);
                loginWindow.Activate();

                StatusText.Text = "Login helper opened";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error opening login helper: {ex.Message}";
            }
        }

        private void TestUrlButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                StatusText.Text = "Please enter a URL first";
                return;
            }

            try
            {
                // Open URL in default browser for testing
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusText.Text = "URL opened in browser";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error opening URL: {ex.Message}";
            }
        }

        private async void ClearCookiesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Confirm with user
                var dialog = new ContentDialog
                {
                    Title = "Clear Cookies",
                    Content = "This will delete all saved cookies and you will need to log in again. Continue?",
                    PrimaryButtonText = "Clear",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    _cookieManager.ClearSavedCookies();
                    await UpdateCookieInfo();
                    StatusText.Text = "Cookies cleared";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error clearing cookies: {ex.Message}";
            }
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Confirm with user
                var dialog = new ContentDialog
                {
                    Title = "Reset Settings",
                    Content = "This will reset all settings to default values. Continue?",
                    PrimaryButtonText = "Reset",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await _configManager.ResetToDefaultAsync();
                    LoadSettings();
                    StatusText.Text = "Settings reset to default";
                    _hasUnsavedChanges = false;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error resetting settings: {ex.Message}";
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Build updated config from UI
                var newConfig = new AppConfig
                {
                    WallpaperUrl = UrlTextBox.Text?.Trim() ?? "https://blink42.com",
                    PerformanceOptimizationEnabled = PerformanceToggle.IsOn,
                    BatteryPauseThreshold = (int)BatterySlider.Value,
                    ShowPauseNotifications = NotificationsToggle.IsOn,
                    AutoStartEnabled = AutoStartToggle.IsOn,
                    MinimizeToTray = TrayToggle.IsOn,
                    IsFirstRun = _config.IsFirstRun,
                    LastLaunchDate = _config.LastLaunchDate
                };

                // Validate
                if (!newConfig.Validate())
                {
                    StatusText.Text = "Invalid configuration. Please check your settings.";
                    return;
                }

                // Save
                await _configManager.SaveConfigAsync(newConfig);

                StatusText.Text = "Settings saved successfully";
                SettingsSaved = true;
                _hasUnsavedChanges = false;

                // Close window after short delay
                await Task.Delay(500);
                this.Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error saving settings: {ex.Message}";
                Console.WriteLine($"SettingsWindow: Error saving - {ex.Message}");
            }
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var dialog = new ContentDialog
                {
                    Title = "Unsaved Changes",
                    Content = "You have unsaved changes. Discard them?",
                    PrimaryButtonText = "Discard",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            SettingsSaved = false;
            this.Close();
        }
    }
}
