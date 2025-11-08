using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using WebPaper.Core;
using Windows.Graphics;
using WinRT.Interop;

namespace WebPaper
{
    /// <summary>
    /// The main wallpaper window that renders behind desktop icons
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private WorkerWManager? _workerWManager;
        private IntPtr _windowHandle;
        private AppWindow? _appWindow;
        private bool _isInitialized = false;

        public MainWindow()
        {
            this.InitializeComponent();

            // Initialize window handle and AppWindow
            _windowHandle = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            // Set up window for wallpaper mode
            SetupWindow();

            // Subscribe to window events
            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;
        }

        private void SetupWindow()
        {
            if (_appWindow == null) return;

            try
            {
                // Remove title bar for a clean wallpaper look
                _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                // Get primary display dimensions
                var displayArea = DisplayArea.Primary;
                var workArea = displayArea.WorkArea;

                // Resize window to cover entire screen
                _appWindow.Resize(new SizeInt32
                {
                    Width = workArea.Width,
                    Height = workArea.Height
                });

                // Move window to top-left corner
                _appWindow.Move(new PointInt32
                {
                    X = workArea.X,
                    Y = workArea.Y
                });
            }
            catch (Exception ex)
            {
                ShowError($"Failed to setup window: {ex.Message}");
            }
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Only initialize once
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                // Step 1: Initialize WebView2
                await InitializeWebView2();

                // Step 2: Attach to desktop using WorkerW
                AttachToDesktop();

                // Hide loading panel
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ShowError($"Initialization failed: {ex.Message}");
            }
        }

        private async Task InitializeWebView2()
        {
            try
            {
                // Set up WebView2 environment with custom user data folder
                var userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WebPaper",
                    "WebView2Data"
                );

                // Create environment
                var environment = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder
                );

                // Initialize WebView2
                await webView.EnsureCoreWebView2Async(environment);

                // Configure WebView2 settings
                webView.CoreWebView2.Settings.AreDevToolsEnabled = true; // Enable for debugging
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Subscribe to navigation events
                webView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
                webView.CoreWebView2.NavigationStarting += WebView_NavigationStarting;

                // Navigate to initial URL (you can change this)
                webView.CoreWebView2.Navigate("https://www.youtube.com");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize WebView2. Make sure WebView2 Runtime is installed.", ex);
            }
        }

        private void AttachToDesktop()
        {
            try
            {
                // Create WorkerW manager
                _workerWManager = new WorkerWManager();

                // Find or create WorkerW window
                var workerW = _workerWManager.FindOrCreateWorkerW();

                // Attach our window to desktop
                _workerWManager.AttachWindowToDesktop(_windowHandle);

                Console.WriteLine($"Successfully attached to desktop. WorkerW: 0x{workerW:X8}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to attach window to desktop", ex);
            }
        }

        private void WebView_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            // You can add URL filtering here if needed
            Console.WriteLine($"Navigating to: {args.Uri}");
        }

        private void WebView_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                Console.WriteLine($"Navigation completed successfully");
            }
            else
            {
                Console.WriteLine($"Navigation failed: {args.WebErrorStatus}");
                ShowError($"Failed to load webpage: {args.WebErrorStatus}");
            }
        }

        private void ShowError(string message)
        {
            // Hide loading, show error
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorMessage.Text = message;

            Console.WriteLine($"ERROR: {message}");
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            // Cleanup
            if (_workerWManager != null && _windowHandle != IntPtr.Zero)
            {
                try
                {
                    _workerWManager.DetachWindowFromDesktop(_windowHandle);
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }

            // Dispose WebView2
            webView.Close();
        }

        /// <summary>
        /// Changes the URL being displayed
        /// </summary>
        public void NavigateToUrl(string url)
        {
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.Navigate(url);
            }
        }
    }
}
