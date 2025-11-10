using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using WebPaper.Core;
using Windows.Graphics;
using WinRT.Interop;
using CoreWebView2 = Microsoft.Web.WebView2.Core.CoreWebView2;
using CoreWebView2Environment = Microsoft.Web.WebView2.Core.CoreWebView2Environment;
using CoreWebView2NavigationCompletedEventArgs = Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs;
using CoreWebView2NavigationStartingEventArgs = Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs;

namespace WebPaper
{
    /// <summary>
    /// The main wallpaper window that renders behind desktop icons
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private WorkerWManager? _workerWManager;
        private InputManager? _inputManager;
        private Services.CookieManager? _cookieManager;
        private Services.PerformanceManager? _performanceManager;
        private Services.ConfigManager? _configManager;
        private Services.TrayIconManager? _trayIconManager;
        private Models.AppConfig? _config;
        private IntPtr _windowHandle;
        private AppWindow? _appWindow;
        private bool _isInitialized = false;
        private bool _wallpaperEnabled = true;

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
                // Step 0: Load configuration
                await LoadConfiguration();

                // Step 1: Initialize system tray
                InitializeTrayIcon();

                // Step 2: Initialize CookieManager
                _cookieManager = new Services.CookieManager();

                // Step 3: Initialize WebView2
                await InitializeWebView2();

                // Step 4: Restore saved cookies (if any)
                await RestoreSavedCookies();

                // Step 5: Attach to desktop using WorkerW
                AttachToDesktop();

                // Step 6: Install input hooks for interactivity
                await InstallInputHooks();

                // Step 7: Initialize performance monitoring
                InitializePerformanceManager();

                // Step 8: Check if first run and show welcome
                await CheckFirstRun();

                // Hide loading panel
                LoadingPanel.Visibility = Visibility.Collapsed;

                Console.WriteLine("=== WebPaper Initialization Complete ===");
                Console.WriteLine($"Wallpaper URL: {_config?.WallpaperUrl}");
                Console.WriteLine("Wallpaper is now fully interactive!");
                Console.WriteLine("Try clicking, typing, and scrolling on the webpage.");
                Console.WriteLine("Right-click for options or check system tray icon.");
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
                    browserExecutableFolder: null,
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

                // Navigate to configured URL
                var url = _config?.WallpaperUrl ?? "https://www.example.com";
                Console.WriteLine($"Navigating to: {url}");
                webView.CoreWebView2.Navigate(url);
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

        private void InitializePerformanceManager()
        {
            try
            {
                Console.WriteLine("Initializing performance manager...");

                // Create performance manager
                _performanceManager = new Services.PerformanceManager();

                // Subscribe to events
                _performanceManager.WallpaperPaused += OnWallpaperPaused;
                _performanceManager.WallpaperResumed += OnWallpaperResumed;

                // Initialize with WebView2
                _performanceManager.Initialize(webView.CoreWebView2);

                Console.WriteLine("Performance manager initialized successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Failed to initialize performance manager - {ex.Message}");
                // Don't throw - allow app to continue without performance optimization
            }
        }

        private void OnWallpaperPaused(object? sender, string reason)
        {
            Console.WriteLine($"Performance: Wallpaper paused - {reason}");
        }

        private void OnWallpaperResumed(object? sender, EventArgs e)
        {
            Console.WriteLine("Performance: Wallpaper resumed");
        }

        private async Task LoadConfiguration()
        {
            try
            {
                _configManager = new Services.ConfigManager();
                _config = await _configManager.LoadConfigAsync();
                Console.WriteLine("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR loading configuration: {ex.Message}");
                _config = Models.AppConfig.CreateDefault();
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _trayIconManager = new Services.TrayIconManager();
                _trayIconManager.Initialize();

                // Wire up events
                _trayIconManager.ShowSettingsRequested += (s, e) => ShowSettings();
                _trayIconManager.ShowAboutRequested += (s, e) => ShowAbout();
                _trayIconManager.ExitRequested += (s, e) => ExitApplication();
                _trayIconManager.ToggleWallpaperRequested += (s, e) => ToggleWallpaper();

                Console.WriteLine("System tray icon initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Failed to initialize system tray - {ex.Message}");
                // Continue without system tray
            }
        }

        private async Task CheckFirstRun()
        {
            try
            {
                if (_config?.IsFirstRun == true && _configManager != null)
                {
                    Console.WriteLine("First run detected - showing welcome message");

                    // Show welcome notification
                    _trayIconManager?.ShowNotification(
                        "Welcome to WebPaper!",
                        "Right-click the tray icon or desktop to access settings.",
                        System.Windows.Forms.ToolTipIcon.Info
                    );

                    // Mark first run complete
                    await _configManager.CompleteFirstRunAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in first run check: {ex.Message}");
            }
        }

        private void ShowSettings()
        {
            try
            {
                if (_configManager == null || _cookieManager == null)
                    return;

                // Create and show settings window
                var settingsWindow = new SettingsWindow(_configManager, _cookieManager);
                settingsWindow.Activate();

                // If settings were saved, reload configuration
                settingsWindow.Closed += async (s, e) =>
                {
                    if (settingsWindow.SettingsSaved)
                    {
                        await LoadConfiguration();
                        // Reload the page with new URL
                        if (webView.CoreWebView2 != null && _config != null)
                        {
                            webView.CoreWebView2.Navigate(_config.WallpaperUrl);
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing settings: {ex.Message}");
            }
        }

        private void ShowAbout()
        {
            try
            {
                var aboutWindow = new AboutWindow();
                aboutWindow.Activate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing about: {ex.Message}");
            }
        }

        private void ToggleWallpaper()
        {
            try
            {
                _wallpaperEnabled = !_wallpaperEnabled;

                if (_wallpaperEnabled)
                {
                    // Resume wallpaper
                    if (_performanceManager != null)
                    {
                        // TODO: Resume if paused
                    }
                    _trayIconManager?.UpdateTooltip("WebPaper - Enabled");
                    Console.WriteLine("Wallpaper enabled");
                }
                else
                {
                    // Pause wallpaper
                    if (_performanceManager != null)
                    {
                        // TODO: Pause
                    }
                    _trayIconManager?.UpdateTooltip("WebPaper - Disabled");
                    Console.WriteLine("Wallpaper disabled");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling wallpaper: {ex.Message}");
            }
        }

        private void ExitApplication()
        {
            try
            {
                Console.WriteLine("Exiting application...");
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exiting: {ex.Message}");
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

        private async Task RestoreSavedCookies()
        {
            try
            {
                if (_cookieManager == null)
                    return;

                if (_cookieManager.HasSavedCookies())
                {
                    Console.WriteLine("CookieManager: Found saved cookies, restoring...");
                    var restored = await _cookieManager.RestoreCookiesAsync(webView.CoreWebView2);

                    if (restored)
                    {
                        Console.WriteLine("CookieManager: Cookies restored successfully!");
                        // Reload the page to use restored cookies
                        webView.CoreWebView2.Reload();
                    }
                }
                else
                {
                    Console.WriteLine("CookieManager: No saved cookies found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CookieManager ERROR: Failed to restore cookies - {ex.Message}");
                // Don't throw - continue without cookies
            }
        }

        private async Task SaveCookiesAsync()
        {
            try
            {
                if (_cookieManager == null || webView.CoreWebView2 == null)
                    return;

                var currentUrl = webView.CoreWebView2.Source;
                await _cookieManager.SaveCookiesAsync(webView.CoreWebView2, currentUrl);
                Console.WriteLine("CookieManager: Cookies saved on shutdown");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CookieManager ERROR: Failed to save cookies - {ex.Message}");
            }
        }

        /// <summary>
        /// Opens a login helper window for authentication
        /// </summary>
        public async Task OpenLoginHelperAsync(string? loginUrl = null)
        {
            await Task.CompletedTask; // Suppress async warning

            try
            {
                if (_cookieManager == null)
                {
                    Console.WriteLine("LoginHelper: CookieManager not initialized");
                    return;
                }

                // Use current URL if not specified
                var url = loginUrl ?? webView.CoreWebView2?.Source ?? "https://www.google.com";

                Console.WriteLine($"LoginHelper: Opening login window for {url}");

                var loginWindow = new LoginHelperWindow(url, _cookieManager);
                loginWindow.Activate();

                // Wait for window to close
                // Note: In a real implementation, you'd want to handle this asynchronously
                // For now, we'll just show the window and let the user close it

            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoginHelper ERROR: {ex.Message}");
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

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Console.WriteLine("WebPaper shutting down...");

            // Save cookies before closing
            try
            {
                await SaveCookiesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving cookies: {ex.Message}");
            }

            // Cleanup system tray
            if (_trayIconManager != null)
            {
                try
                {
                    _trayIconManager.Dispose();
                    Console.WriteLine("System tray disposed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing TrayIconManager: {ex.Message}");
                }
            }

            // Cleanup performance manager
            if (_performanceManager != null)
            {
                try
                {
                    _performanceManager.Dispose();
                    Console.WriteLine("Performance manager disposed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing PerformanceManager: {ex.Message}");
                }
            }

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

        // Context menu event handlers
        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowSettings();
        }

        private void ReloadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                webView.CoreWebView2?.Reload();
                Console.WriteLine("Wallpaper reloaded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reloading: {ex.Message}");
            }
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowAbout();
        }
    }
}
