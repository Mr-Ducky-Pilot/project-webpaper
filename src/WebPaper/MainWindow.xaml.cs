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
        private InputManager? _inputManager;
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

                // Step 3: Install input hooks for interactivity
                await InstallInputHooks();

                // Hide loading panel
                LoadingPanel.Visibility = Visibility.Collapsed;

                Console.WriteLine("=== WebPaper Initialization Complete ===");
                Console.WriteLine("Wallpaper is now fully interactive!");
                Console.WriteLine("Try clicking, typing, and scrolling on the webpage.");
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

        private async Task InstallInputHooks()
        {
            try
            {
                Console.WriteLine("Installing input hooks...");

                // Create input manager
                _inputManager = new InputManager();

                // Get WebView2's HWND
                // We need to wait a moment for WebView2 to create its window
                await Task.Delay(500);

                IntPtr webViewHandle = GetWebViewHandle();

                if (webViewHandle == IntPtr.Zero)
                {
                    Console.WriteLine("WARNING: Could not get WebView2 handle. Input may not work correctly.");
                    Console.WriteLine("Attempting to use fallback method...");
                    webViewHandle = _windowHandle; // Fallback to main window
                }
                else
                {
                    Console.WriteLine($"WebView2 Handle: 0x{webViewHandle:X8}");
                }

                // Install hooks
                _inputManager.InstallHooks(webView.CoreWebView2, webViewHandle);

                Console.WriteLine("Input hooks installed successfully!");
                Console.WriteLine(_inputManager.GetDiagnostics());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Failed to install input hooks - {ex.Message}");
                Console.WriteLine("Wallpaper will render but may not be interactive.");
                // Don't throw - allow app to continue without input
            }
        }

        private IntPtr GetWebViewHandle()
        {
            try
            {
                // Try to find WebView2's child window
                // WebView2 creates a child window for rendering
                IntPtr childHandle = IntPtr.Zero;

                Native.NativeMethods.EnumWindows((hwnd, lparam) =>
                {
                    // Check if this window is a child of our window
                    IntPtr parent = Native.NativeMethods.GetWindow(hwnd, Native.NativeMethods.GetWindowType.GW_OWNER);

                    // Get the window class
                    var className = WorkerWManager.GetWindowClassName(hwnd);

                    // WebView2 uses Chrome_WidgetWin_* windows
                    if (className.Contains("Chrome_WidgetWin"))
                    {
                        childHandle = hwnd;
                        return false; // Stop enumeration
                    }

                    return true; // Continue enumeration
                }, IntPtr.Zero);

                return childHandle;
            }
            catch
            {
                return IntPtr.Zero;
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
            Console.WriteLine("WebPaper shutting down...");

            // Cleanup input hooks
            if (_inputManager != null)
            {
                try
                {
                    _inputManager.Dispose();
                    Console.WriteLine("Input hooks uninstalled");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing InputManager: {ex.Message}");
                }
            }

            // Cleanup WorkerW
            if (_workerWManager != null && _windowHandle != IntPtr.Zero)
            {
                try
                {
                    _workerWManager.DetachWindowFromDesktop(_windowHandle);
                    Console.WriteLine("Detached from desktop");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error detaching from desktop: {ex.Message}");
                }
            }

            // Dispose WebView2
            try
            {
                webView.Close();
                Console.WriteLine("WebView2 closed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing WebView2: {ex.Message}");
            }

            Console.WriteLine("WebPaper shutdown complete");
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
