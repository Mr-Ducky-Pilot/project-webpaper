using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Serilog;
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

                // CRITICAL: Get PRIMARY monitor dimensions (monitor with taskbar)
                // Use GetSystemMetrics to get actual primary monitor size
                int screenWidth = Native.NativeMethods.GetSystemMetrics(Native.NativeMethods.SM_CXSCREEN);
                int screenHeight = Native.NativeMethods.GetSystemMetrics(Native.NativeMethods.SM_CYSCREEN);

                Log.Information($"Primary monitor dimensions: {screenWidth}x{screenHeight}");

                // Resize window to cover primary screen
                _appWindow.Resize(new SizeInt32
                {
                    Width = screenWidth,
                    Height = screenHeight
                });

                // Move window to origin (0, 0) of primary monitor
                _appWindow.Move(new PointInt32
                {
                    X = 0,
                    Y = 0
                });

                Log.Information($"Window positioned at (0, 0) with size {screenWidth}x{screenHeight}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to setup window");
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
                // CRITICAL: Defer initialization to allow window and controls to be fully loaded
                // This prevents WebView2 deadlock issues
                Log.Information("Waiting for window to be fully loaded...");
                await Task.Delay(250); // Give UI thread time to complete window initialization

                // Step 0: Load configuration
                Log.Information("Step 0: Loading configuration...");
                await LoadConfiguration();
                Log.Information("Step 0: Configuration loaded");

                // Step 1: Initialize system tray
                Log.Information("Step 1: Initializing system tray...");
                InitializeTrayIcon();
                Log.Information("Step 1: System tray initialized");

                // Step 2: Initialize CookieManager
                Log.Information("Step 2: Initializing CookieManager...");
                _cookieManager = new Services.CookieManager();
                Log.Information("Step 2: CookieManager initialized");

                // Step 3: Initialize WebView2
                Log.Information("Step 3: Initializing WebView2...");
                await InitializeWebView2();
                Log.Information("Step 3: WebView2 initialized");

                // Step 4: Restore saved cookies (if any)
                Log.Information("Step 4: Restoring saved cookies...");
                await RestoreSavedCookies();
                Log.Information("Step 4: Cookies restored");

                // Step 5: Attach to desktop using WorkerW
                Log.Information("Step 5: Attaching to desktop...");
                AttachToDesktop();
                Log.Information("Step 5: Attached to desktop");

                // Step 6: Install input hooks for interactivity
                Log.Information("Step 6: Installing input hooks...");
                await InstallInputHooks();
                Log.Information("Step 6: Input hooks installed");

                // Step 7: Initialize performance monitoring
                Log.Information("Step 7: Initializing performance manager...");
                InitializePerformanceManager();
                Log.Information("Step 7: Performance manager initialized");

                // Step 8: Check if first run and show welcome
                Log.Information("Step 8: Checking first run...");
                await CheckFirstRun();
                Log.Information("Step 8: First run check complete");

                // Hide loading panel
                Log.Information("Step 9: Hiding loading panel...");
                LoadingPanel.Visibility = Visibility.Collapsed;

                Log.Information("=== WebPaper Initialization Complete ===");
                Log.Information($"Wallpaper URL: {_config?.WallpaperUrl}");
                Log.Information("Wallpaper is now fully interactive!");
                Log.Information("Try clicking, typing, and scrolling on the webpage.");
                Log.Information("Right-click for options or check system tray icon.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FATAL: Initialization failed");
                ShowError($"Initialization failed: {ex.Message}");
            }
        }

        private async Task InitializeWebView2()
        {
            try
            {
                Log.Information("Step 3.1: Initializing WebView2...");

                // Set up WebView2 environment with custom user data folder
                var userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WebPaper",
                    "WebView2Data"
                );

                Log.Information($"Step 3.2: WebView2 user data folder: {userDataFolder}");

                // Create environment
                Log.Information("Step 3.3: Creating WebView2 environment...");
                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    null,           // browserExecutableFolder (null = use installed runtime)
                    userDataFolder, // userDataFolder
                    null            // options (null = use defaults)
                );

                Log.Information("Step 3.4: WebView2 environment created successfully");

                // CRITICAL FIX: Give WebView2 control time to be fully loaded in visual tree
                // This prevents async deadlock on UI thread
                Log.Information("Step 3.5a: Waiting for WebView2 control to be ready in visual tree...");
                await Task.Delay(100); // Small delay to ensure control is loaded

                // Force a UI update to ensure WebView2 is in the visual tree
                Log.Information("Step 3.5b: Forcing UI dispatcher queue to process pending operations...");
                var tcs = new TaskCompletionSource<bool>();
                webView.DispatcherQueue.TryEnqueue(() => tcs.SetResult(true));
                await tcs.Task;

                // Initialize WebView2
                Log.Information("Step 3.5c: Ensuring CoreWebView2 (this may take a moment)...");
                await webView.EnsureCoreWebView2Async(environment);

                Log.Information("Step 3.6: CoreWebView2 initialized successfully");

                // Configure WebView2 settings
                Log.Information("Step 3.7: Configuring WebView2 settings...");
                webView.CoreWebView2.Settings.AreDevToolsEnabled = true; // Enable for debugging
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Subscribe to navigation events
                Log.Information("Step 3.8: Subscribing to navigation events...");
                webView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
                webView.CoreWebView2.NavigationStarting += WebView_NavigationStarting;

                // Navigate to configured URL
                var url = _config?.WallpaperUrl ?? "https://blink42.com";
                Log.Information($"Step 3.9: Navigating to: {url}");
                webView.CoreWebView2.Navigate(url);

                Log.Information("Step 3.10: WebView2 initialization complete");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ERROR in WebView2 initialization");
                throw new InvalidOperationException("Failed to initialize WebView2. Make sure WebView2 Runtime is installed.", ex);
            }
        }

        private void AttachToDesktop()
        {
            try
            {
                Log.Information($"Step 5.1: Preparing window for desktop attachment...");
                Log.Information($"Window handle: 0x{_windowHandle:X8}");

                // CRITICAL: Set window styles BEFORE attaching to WorkerW
                // Remove caption, borders, and set as child window
                Log.Information("Step 5.2: Removing window decorations (title bar, borders, buttons)...");

                // Get current window style
                int currentStyle = Native.NativeMethods.GetWindowLong(_windowHandle, Native.NativeMethods.GWL_STYLE);
                Log.Information($"Current window style: 0x{currentStyle:X8}");

                // CRITICAL: Must set WS_CHILD BEFORE calling SetParent
                // Remove: WS_CAPTION, WS_THICKFRAME, WS_SYSMENU, WS_BORDER, WS_DLGFRAME
                // Add: WS_CHILD, WS_VISIBLE, WS_CLIPCHILDREN, WS_CLIPSIBLINGS
                int newStyle = currentStyle;

                // Remove all window frame/border styles
                newStyle &= ~(int)(Native.NativeMethods.WS_CAPTION |
                                  Native.NativeMethods.WS_THICKFRAME |
                                  Native.NativeMethods.WS_SYSMENU |
                                  Native.NativeMethods.WS_BORDER |
                                  Native.NativeMethods.WS_DLGFRAME);

                // Add child window style and clipping
                newStyle |= (int)(Native.NativeMethods.WS_CHILD |
                                 Native.NativeMethods.WS_VISIBLE |
                                 Native.NativeMethods.WS_CLIPCHILDREN |
                                 Native.NativeMethods.WS_CLIPSIBLINGS);

                Native.NativeMethods.SetWindowLong(_windowHandle, Native.NativeMethods.GWL_STYLE, newStyle);
                Log.Information($"New window style (with WS_CHILD): 0x{newStyle:X8}");

                // CRITICAL FIX: Do NOT set WS_EX_NOACTIVATE!
                // That flag prevents the window from ever receiving focus, which breaks WebView2 input.
                // We need the window to be able to receive focus for input to work.
                int currentExStyle = Native.NativeMethods.GetWindowLong(_windowHandle, Native.NativeMethods.GWL_EXSTYLE);
                // Remove WS_EX_NOACTIVATE if it exists
                int newExStyle = currentExStyle & ~(int)Native.NativeMethods.WS_EX_NOACTIVATE;
                Native.NativeMethods.SetWindowLong(_windowHandle, Native.NativeMethods.GWL_EXSTYLE, newExStyle);
                Log.Information($"Extended window style (WS_EX_NOACTIVATE removed): 0x{newExStyle:X8}");

                // Force the window to update with new styles
                Log.Information("Step 5.3: Applying window style changes...");
                Native.NativeMethods.SetWindowPos(
                    _windowHandle,
                    IntPtr.Zero,
                    0, 0, 0, 0,
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOMOVE |
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOSIZE |
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOZORDER |
                    Native.NativeMethods.SetWindowPosFlags.SWP_FRAMECHANGED
                );

                // Create WorkerW manager
                Log.Information("Step 5.4: Finding or creating WorkerW window...");
                _workerWManager = new WorkerWManager();

                // Find or create WorkerW window
                var workerW = _workerWManager.FindOrCreateWorkerW();
                Log.Information($"WorkerW handle: 0x{workerW:X8}");

                // Attach our window to desktop (sets as child of WorkerW)
                Log.Information("Step 5.5: Setting window as child of WorkerW...");
                _workerWManager.AttachWindowToDesktop(_windowHandle);

                // Verify attachment
                IntPtr parent = Native.NativeMethods.GetParent(_windowHandle);
                Log.Information($"Window parent after attachment: 0x{parent:X8}");

                if (parent == workerW)
                {
                    Log.Information("✓ Window successfully attached to WorkerW!");
                }
                else if (parent == IntPtr.Zero)
                {
                    Log.Error($"✗ CRITICAL: SetParent FAILED! Window parent is NULL (0x00000000)");
                    Log.Error($"  Expected WorkerW: 0x{workerW:X8}");
                    Log.Error($"  This means the window is not attached to desktop!");
                    Log.Error($"  Attempting to re-attach with error checking...");

                    // Try again with explicit error checking
                    IntPtr result = Native.NativeMethods.SetParent(_windowHandle, workerW);
                    if (result == IntPtr.Zero)
                    {
                        uint error = Native.NativeMethods.GetLastError();
                        throw new InvalidOperationException($"SetParent failed with error code: {error}");
                    }

                    parent = Native.NativeMethods.GetParent(_windowHandle);
                    Log.Information($"After retry, window parent: 0x{parent:X8}");
                }
                else
                {
                    Log.Warning($"! Window parent mismatch. Expected: 0x{workerW:X8}, Got: 0x{parent:X8}");
                }

                // Ensure window is positioned and sized correctly to fill PRIMARY desktop
                Log.Information("Step 5.6: Positioning window to fill PRIMARY desktop...");

                // CRITICAL: Use GetSystemMetrics to get PRIMARY monitor dimensions
                // This ensures we target the main monitor with the taskbar
                int screenWidth = Native.NativeMethods.GetSystemMetrics(Native.NativeMethods.SM_CXSCREEN);
                int screenHeight = Native.NativeMethods.GetSystemMetrics(Native.NativeMethods.SM_CYSCREEN);

                Log.Information($"Primary monitor size from GetSystemMetrics: {screenWidth}x{screenHeight}");

                // Position window at origin (0, 0) with primary monitor size
                bool setPosResult = Native.NativeMethods.SetWindowPos(
                    _windowHandle,
                    IntPtr.Zero,
                    0, 0,
                    screenWidth, screenHeight,
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOZORDER |
                    Native.NativeMethods.SetWindowPosFlags.SWP_SHOWWINDOW |
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOACTIVATE
                );

                Log.Information($"SetWindowPos result: {setPosResult}");
                Log.Information($"Window positioned at (0, 0) with size {screenWidth}x{screenHeight}");

                // Explicitly show the window
                Log.Information("Step 5.7: Explicitly showing window...");
                Native.NativeMethods.ShowWindow(_windowHandle, Native.NativeMethods.ShowWindowCommands.SW_SHOW);

                Log.Information($"Step 5.8: Desktop attachment complete!");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to attach window to desktop");
                throw new InvalidOperationException("Failed to attach window to desktop", ex);
            }
        }

        private async Task InstallInputHooks()
        {
            try
            {
                Log.Information("Installing input hooks...");

                // Create input manager
                _inputManager = new InputManager();

                // Get WebView2's HWND
                // We need to wait a moment for WebView2 to create its window
                await Task.Delay(500);

                IntPtr webViewHandle = GetWebViewHandle();

                if (webViewHandle == IntPtr.Zero)
                {
                    Log.Warning(" Could not get WebView2 handle. Input may not work correctly.");
                    Log.Information("Attempting to use fallback method...");
                    webViewHandle = _windowHandle; // Fallback to main window
                }
                else
                {
                    Log.Information($"WebView2 Handle: 0x{webViewHandle:X8}");
                }

                // Install hooks (CRITICAL: Pass main window handle for focus control)
                _inputManager.InstallHooks(webView.CoreWebView2, webViewHandle, _windowHandle);

                Log.Information("Input hooks installed successfully!");
                Console.WriteLine(_inputManager.GetDiagnostics());
            }
            catch (Exception ex)
            {
                Log.Warning($" Failed to install input hooks - {ex.Message}");
                Log.Information("Wallpaper will render but may not be interactive.");
                // Don't throw - allow app to continue without input
            }
        }

        private void InitializePerformanceManager()
        {
            try
            {
                Log.Information("Initializing performance manager...");

                // Create performance manager
                _performanceManager = new Services.PerformanceManager();

                // Subscribe to events
                _performanceManager.WallpaperPaused += OnWallpaperPaused;
                _performanceManager.WallpaperResumed += OnWallpaperResumed;

                // Initialize with WebView2
                _performanceManager.Initialize(webView.CoreWebView2);

                Log.Information("Performance manager initialized successfully!");
            }
            catch (Exception ex)
            {
                Log.Warning($" Failed to initialize performance manager - {ex.Message}");
                // Don't throw - allow app to continue without performance optimization
            }
        }

        private void OnWallpaperPaused(object? sender, string reason)
        {
            Log.Information($"Performance: Wallpaper paused - {reason}");
        }

        private void OnWallpaperResumed(object? sender, EventArgs e)
        {
            Log.Information("Performance: Wallpaper resumed");
        }

        private async Task LoadConfiguration()
        {
            try
            {
                _configManager = new Services.ConfigManager();
                _config = await _configManager.LoadConfigAsync();
                Log.Information("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                Log.Information($"ERROR loading configuration: {ex.Message}");
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

                Log.Information("System tray icon initialized");
            }
            catch (Exception ex)
            {
                Log.Warning($" Failed to initialize system tray - {ex.Message}");
                // Continue without system tray
            }
        }

        private async Task CheckFirstRun()
        {
            try
            {
                if (_config?.IsFirstRun == true && _configManager != null)
                {
                    Log.Information("First run detected - showing welcome window");

                    // Show welcome window and wait for user to complete setup
                    await ShowWelcomeWindow();

                    // Reload configuration after welcome window closes
                    await LoadConfiguration();

                    // Reload webpage with new URL
                    if (webView.CoreWebView2 != null && _config != null)
                    {
                        Log.Information("Navigating to user-selected URL: {Url}", _config.WallpaperUrl);
                        webView.CoreWebView2.Navigate(_config.WallpaperUrl);
                    }

                    // Mark first run complete
                    await _configManager.CompleteFirstRunAsync();

                    // Show welcome notification
                    _trayIconManager?.ShowNotification(
                        "Welcome to WebPaper!",
                        "Your wallpaper is now active! Right-click the tray icon for settings.",
                        System.Windows.Forms.ToolTipIcon.Info
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in first run check");
            }
        }

        private async Task ShowWelcomeWindow()
        {
            try
            {
                // Create welcome window
                var welcomeWindow = new WelcomeWindow(_configManager!);

                // Use TaskCompletionSource to wait for window to close
                var tcs = new TaskCompletionSource<bool>();
                welcomeWindow.Closed += (s, e) => tcs.SetResult(welcomeWindow.SetupCompleted);

                // Show window
                welcomeWindow.Activate();

                // Wait for window to close
                await tcs.Task;

                Log.Information("Welcome window closed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to show welcome window");
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
                Log.Information($"Error showing settings: {ex.Message}");
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
                Log.Information($"Error showing about: {ex.Message}");
            }
        }

        private async void ToggleWallpaper()
        {
            try
            {
                _wallpaperEnabled = !_wallpaperEnabled;

                if (_wallpaperEnabled)
                {
                    // Resume wallpaper
                    if (_performanceManager != null)
                    {
                        await _performanceManager.ResumeAsync();
                    }
                    _trayIconManager?.UpdateTooltip("WebPaper - Enabled");
                    _trayIconManager?.ShowNotification(
                        "WebPaper Enabled",
                        "Wallpaper is now active",
                        System.Windows.Forms.ToolTipIcon.Info);
                    Log.Information("Wallpaper enabled by user");
                }
                else
                {
                    // Pause wallpaper
                    if (_performanceManager != null)
                    {
                        await _performanceManager.PauseAsync();
                    }
                    _trayIconManager?.UpdateTooltip("WebPaper - Disabled");
                    _trayIconManager?.ShowNotification(
                        "WebPaper Disabled",
                        "Wallpaper is now paused",
                        System.Windows.Forms.ToolTipIcon.Info);
                    Log.Information("Wallpaper disabled by user");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error toggling wallpaper");
            }
        }

        private void ExitApplication()
        {
            try
            {
                Log.Information("Exiting application...");
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Log.Information($"Error exiting: {ex.Message}");
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
                    Log.Information("CookieManager: Found saved cookies, restoring...");
                    var restored = await _cookieManager.RestoreCookiesAsync(webView.CoreWebView2);

                    if (restored)
                    {
                        Log.Information("CookieManager: Cookies restored successfully!");
                        // Reload the page to use restored cookies
                        webView.CoreWebView2.Reload();
                    }
                }
                else
                {
                    Log.Information("CookieManager: No saved cookies found");
                }
            }
            catch (Exception ex)
            {
                Log.Information($"CookieManager ERROR: Failed to restore cookies - {ex.Message}");
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
                Log.Information("CookieManager: Cookies saved on shutdown");
            }
            catch (Exception ex)
            {
                Log.Information($"CookieManager ERROR: Failed to save cookies - {ex.Message}");
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
                    Log.Information("LoginHelper: CookieManager not initialized");
                    return;
                }

                // Use current URL if not specified
                var url = loginUrl ?? webView.CoreWebView2?.Source ?? "https://www.google.com";

                Log.Information($"LoginHelper: Opening login window for {url}");

                var loginWindow = new LoginHelperWindow(url, _cookieManager);
                loginWindow.Activate();

                // Wait for window to close
                // Note: In a real implementation, you'd want to handle this asynchronously
                // For now, we'll just show the window and let the user close it

            }
            catch (Exception ex)
            {
                Log.Information($"LoginHelper ERROR: {ex.Message}");
            }
        }

        private void WebView_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            // You can add URL filtering here if needed
            Log.Information($"Navigating to: {args.Uri}");
        }

        private void WebView_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                Log.Information($"Navigation completed successfully");
            }
            else
            {
                Log.Information($"Navigation failed: {args.WebErrorStatus}");
                ShowError($"Failed to load webpage: {args.WebErrorStatus}");
            }
        }

        private async void ShowError(string message)
        {
            // Hide loading, show error
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorMessage.Text = message;

            Log.Error($"ERROR: {message}");

            // Auto-dismiss error after 5 seconds
            await Task.Delay(5000);
            ErrorPanel.Visibility = Visibility.Collapsed;
            Log.Information("Error panel auto-dismissed");
        }

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Log.Information("WebPaper shutting down...");

            // Save cookies before closing
            try
            {
                await SaveCookiesAsync();
            }
            catch (Exception ex)
            {
                Log.Information($"Error saving cookies: {ex.Message}");
            }

            // Cleanup system tray
            if (_trayIconManager != null)
            {
                try
                {
                    _trayIconManager.Dispose();
                    Log.Information("System tray disposed");
                }
                catch (Exception ex)
                {
                    Log.Information($"Error disposing TrayIconManager: {ex.Message}");
                }
            }

            // Cleanup performance manager
            if (_performanceManager != null)
            {
                try
                {
                    _performanceManager.Dispose();
                    Log.Information("Performance manager disposed");
                }
                catch (Exception ex)
                {
                    Log.Information($"Error disposing PerformanceManager: {ex.Message}");
                }
            }

            // Cleanup input hooks
            if (_inputManager != null)
            {
                try
                {
                    _inputManager.Dispose();
                    Log.Information("Input hooks uninstalled");
                }
                catch (Exception ex)
                {
                    Log.Information($"Error disposing InputManager: {ex.Message}");
                }
            }

            // Cleanup WorkerW
            if (_workerWManager != null && _windowHandle != IntPtr.Zero)
            {
                try
                {
                    _workerWManager.DetachWindowFromDesktop(_windowHandle);
                    Log.Information("Detached from desktop");
                }
                catch (Exception ex)
                {
                    Log.Information($"Error detaching from desktop: {ex.Message}");
                }
            }

            // Dispose WebView2
            try
            {
                webView.Close();
                Log.Information("WebView2 closed");
            }
            catch (Exception ex)
            {
                Log.Information($"Error closing WebView2: {ex.Message}");
            }

            Log.Information("WebPaper shutdown complete");
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
                Log.Information("Wallpaper reloaded");
            }
            catch (Exception ex)
            {
                Log.Information($"Error reloading: {ex.Message}");
            }
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowAbout();
        }
    }
}
