using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Serilog;
using Windows.Graphics;
using WinRT.Interop;

namespace WebPaper
{
    public sealed partial class WelcomeWindow : Window
    {
        private Services.ConfigManager? _configManager;
        private string _selectedUrl = "https://www.example.com";
        public bool SetupCompleted { get; private set; } = false;

        public WelcomeWindow(Services.ConfigManager configManager)
        {
            this.InitializeComponent();
            _configManager = configManager;

            // Set window size
            SetWindowSize(800, 900);

            Log.Information("WelcomeWindow opened");
        }

        private void SetWindowSize(int width, int height)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow != null)
                {
                    appWindow.Resize(new SizeInt32 { Width = width, Height = height });

                    // Center the window
                    var displayArea = DisplayArea.Primary;
                    var workArea = displayArea.WorkArea;

                    appWindow.Move(new PointInt32
                    {
                        X = (workArea.Width - width) / 2 + workArea.X,
                        Y = (workArea.Height - height) / 2 + workArea.Y
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to set window size");
            }
        }

        private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var url = UrlTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(url))
            {
                UrlValidationText.Text = "Please enter a URL";
                UrlValidationText.Visibility = Visibility.Visible;
                _selectedUrl = "";
                return;
            }

            // Validate URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                UrlValidationText.Text = "Invalid URL format";
                UrlValidationText.Visibility = Visibility.Visible;
                _selectedUrl = "";
                return;
            }

            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                UrlValidationText.Text = "URL must start with http:// or https://";
                UrlValidationText.Visibility = Visibility.Visible;
                _selectedUrl = "";
                return;
            }

            // Valid URL
            UrlValidationText.Visibility = Visibility.Collapsed;
            _selectedUrl = url;
        }

        private void SuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                UrlTextBox.Text = url;
                _selectedUrl = url;
                Log.Information("User selected suggestion: {Url}", url);
            }
        }

        private async void GetStartedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate URL one more time
                if (string.IsNullOrWhiteSpace(_selectedUrl))
                {
                    UrlValidationText.Text = "Please enter a valid URL";
                    UrlValidationText.Visibility = Visibility.Visible;
                    return;
                }

                Log.Information("User completed setup with URL: {Url}", _selectedUrl);

                // Save the URL to config
                if (_configManager != null)
                {
                    var config = await _configManager.LoadConfigAsync();
                    config.WallpaperUrl = _selectedUrl;
                    await _configManager.SaveConfigAsync(config);
                    Log.Information("Configuration saved");
                }

                // Mark setup as completed
                SetupCompleted = true;

                // Close the welcome window
                this.Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save configuration");

                // Show error
                UrlValidationText.Text = $"Failed to save settings: {ex.Message}";
                UrlValidationText.Visibility = Visibility.Visible;
            }
        }
    }
}
