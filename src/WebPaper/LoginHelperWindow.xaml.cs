using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using WebPaper.Services;
using Windows.Graphics;
using WinRT.Interop;
using CoreWebView2 = Microsoft.Web.WebView2.Core.CoreWebView2;
using CoreWebView2Environment = Microsoft.Web.WebView2.Core.CoreWebView2Environment;
using CoreWebView2NavigationCompletedEventArgs = Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs;

namespace WebPaper
{
    /// <summary>
    /// Helper window for user authentication/login
    /// </summary>
    public sealed partial class LoginHelperWindow : Window
    {
        private readonly string _loginUrl;
        private readonly CookieManager _cookieManager;
        private bool _savedSuccessfully = false;

        public bool SavedSuccessfully => _savedSuccessfully;

        public LoginHelperWindow(string loginUrl, CookieManager cookieManager)
        {
            this.InitializeComponent();

            _loginUrl = loginUrl ?? throw new ArgumentNullException(nameof(loginUrl));
            _cookieManager = cookieManager ?? throw new ArgumentNullException(nameof(cookieManager));

            // Set window size using proper WinUI 3 pattern
            SetWindowSize(1024, 768);

            // Center window
            CenterWindow();

            // Initialize
            _ = InitializeAsync();
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

        private void CenterWindow()
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow != null)
                {
                    var displayArea = DisplayArea.Primary;
                    var workArea = displayArea.WorkArea;

                    var x = (workArea.Width - 1024) / 2 + workArea.X;
                    var y = (workArea.Height - 768) / 2 + workArea.Y;

                    appWindow.Move(new PointInt32 { X = x, Y = y });
                }
            }
            catch
            {
                // Ignore centering errors
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                UrlText.Text = $"Logging in to: {_loginUrl}";

                // Initialize WebView2
                var userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WebPaper",
                    "LoginHelper"
                );

                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    null,           // browserExecutableFolder (null = use installed runtime)
                    userDataFolder, // userDataFolder
                    null            // options (null = use defaults)
                );

                await loginWebView.EnsureCoreWebView2Async(environment);

                // Configure settings
                loginWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                loginWebView.CoreWebView2.Settings.IsStatusBarEnabled = true;
                loginWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

                // Subscribe to navigation events
                loginWebView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;

                // Navigate to login URL
                loginWebView.CoreWebView2.Navigate(_loginUrl);

                Console.WriteLine($"LoginHelper: Navigated to {_loginUrl}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoginHelper ERROR: Initialization failed - {ex.Message}");
                ShowError($"Failed to load login page: {ex.Message}");
            }
        }

        private void WebView_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            // Hide loading panel
            LoadingPanel.Visibility = Visibility.Collapsed;

            if (!args.IsSuccess)
            {
                Console.WriteLine($"LoginHelper: Navigation failed - {args.WebErrorStatus}");
                ShowError($"Failed to load page: {args.WebErrorStatus}");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Saving...";
                }

                // Save cookies
                var currentUrl = loginWebView.CoreWebView2.Source;
                await _cookieManager.SaveCookiesAsync(loginWebView.CoreWebView2, currentUrl);

                _savedSuccessfully = true;

                Console.WriteLine("LoginHelper: Cookies saved successfully");

                // Close window
                this.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoginHelper ERROR: Failed to save cookies - {ex.Message}");
                ShowError($"Failed to save login: {ex.Message}");

                if (sender is Button btn)
                {
                    btn.IsEnabled = true;
                    btn.Content = "Save & Close";
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("LoginHelper: Cancelled by user");
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("LoginHelper: Closed by user");
            this.Close();
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
    }
}
