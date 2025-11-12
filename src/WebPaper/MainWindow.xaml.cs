using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Serilog;
using WebPaper.Core;
using Windows.Graphics;
using Windows.System;
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
        private DispatcherQueue? _dispatcherQueue;

        public MainWindow()
        {
            this.InitializeComponent();

            // CRITICAL: Capture DispatcherQueue on UI thread for thread marshaling
            // This is needed to execute WebView2 operations from background threads (like input hooks)
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

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

                LoadingStatusText.Text = "Loading configuration...";
                await LoadConfiguration();
                Log.Information("Configuration loaded successfully");
                LoadingStatusText.Text = "Setting up system tray...";
                InitializeTrayIcon();

                LoadingStatusText.Text = "Initializing cookie manager...";
                _cookieManager = new Services.CookieManager();

                LoadingStatusText.Text = "Initializing web browser...";
                await InitializeWebView2();

                LoadingStatusText.Text = "Restoring saved sessions...";
                await RestoreSavedCookies();

                LoadingStatusText.Text = "Attaching to desktop...";
                AttachToDesktop();

                LoadingStatusText.Text = "Setting up interactivity...";
                await InstallInputHooks();

                LoadingStatusText.Text = "Optimizing performance...";
                InitializePerformanceManager();

                // Check if first run and show welcome
                LoadingStatusText.Text = "Almost ready...";
                await CheckFirstRun();

                // Hide loading panel
                LoadingPanel.Visibility = Visibility.Collapsed;

                Log.Information("=== WebPaper Initialization Complete ===");
                Log.Information("Wallpaper URL: {Url}", _config?.WallpaperUrl ?? "default");
                Log.Information("Wallpaper is now fully interactive");
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
                Log.Information("Initializing WebView2...");

                // Set up WebView2 environment with custom user data folder
                var userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WebPaper",
                    "WebView2Data"
                );

                // Create environment
                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    null,           // browserExecutableFolder (null = use installed runtime)
                    userDataFolder, // userDataFolder
                    null            // options (null = use defaults)
                );

                // CRITICAL FIX: Give WebView2 control time to be fully loaded in visual tree
                // This prevents async deadlock on UI thread
                await Task.Delay(100); // Small delay to ensure control is loaded

                // Force a UI update to ensure WebView2 is in the visual tree
                var tcs = new TaskCompletionSource<bool>();
                webView.DispatcherQueue.TryEnqueue(() => tcs.SetResult(true));
                await tcs.Task;

                // Initialize WebView2
                await webView.EnsureCoreWebView2Async(environment);

                // Configure WebView2 settings
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false; // Disabled in production
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Subscribe to navigation events
                webView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
                webView.CoreWebView2.NavigationStarting += WebView_NavigationStarting;

                // Navigate to configured URL
                var url = _config?.WallpaperUrl ?? "https://blink42.com";
                Log.Information("Navigating to: {Url}", url);
                webView.CoreWebView2.Navigate(url);

                Log.Information("WebView2 initialization complete");
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
                Log.Information("Attaching window to desktop...");

                // CRITICAL: Set window styles BEFORE attaching to WorkerW
                // Get current window style
                int currentStyle = Native.NativeMethods.GetWindowLong(_windowHandle, Native.NativeMethods.GWL_STYLE);

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

                // CRITICAL FIX: Do NOT set WS_EX_NOACTIVATE!
                // That flag prevents the window from ever receiving focus, which breaks WebView2 input.
                int currentExStyle = Native.NativeMethods.GetWindowLong(_windowHandle, Native.NativeMethods.GWL_EXSTYLE);
                int newExStyle = currentExStyle & ~(int)Native.NativeMethods.WS_EX_NOACTIVATE;
                Native.NativeMethods.SetWindowLong(_windowHandle, Native.NativeMethods.GWL_EXSTYLE, newExStyle);

                // Force the window to update with new styles
                Native.NativeMethods.SetWindowPos(
                    _windowHandle,
                    IntPtr.Zero,
                    0, 0, 0, 0,
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOMOVE |
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOSIZE |
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOZORDER |
                    Native.NativeMethods.SetWindowPosFlags.SWP_FRAMECHANGED
                );

                // Create WorkerW manager and find/create WorkerW window
                _workerWManager = new WorkerWManager();
                var workerW = _workerWManager.FindOrCreateWorkerW();

                // Attach our window to desktop (sets as child of WorkerW)
                _workerWManager.AttachWindowToDesktop(_windowHandle);

                // Verify attachment
                IntPtr parent = Native.NativeMethods.GetParent(_windowHandle);

                if (parent != workerW)
                {
                    if (parent == IntPtr.Zero)
                    {
                        Log.Error("SetParent failed - attempting retry");

                        // Try again with explicit error checking
                        IntPtr result = Native.NativeMethods.SetParent(_windowHandle, workerW);
                        if (result == IntPtr.Zero)
                        {
                            uint error = Native.NativeMethods.GetLastError();
                            throw new InvalidOperationException($"SetParent failed with error code: {error}");
                        }

                        parent = Native.NativeMethods.GetParent(_windowHandle);
                    }
                    else
                    {
                        Log.Warning("Window parent mismatch detected");
                    }
                }

                // CRITICAL FIX: Set Z-order to bottom so desktop icons appear on top
                Native.NativeMethods.SetWindowPos(
                    _windowHandle,
                    Native.NativeMethods.HWND_BOTTOM,
                    0, 0, 0, 0,
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOMOVE |
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOSIZE |
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOACTIVATE
                );

                // Position window to fill PRIMARY desktop
                // Use GetSystemMetrics to get PRIMARY monitor dimensions
                int screenWidth = Native.NativeMethods.GetSystemMetrics(Native.NativeMethods.SM_CXSCREEN);
                int screenHeight = Native.NativeMethods.GetSystemMetrics(Native.NativeMethods.SM_CYSCREEN);

                // Position window at origin (0, 0) with primary monitor size
                Native.NativeMethods.SetWindowPos(
                    _windowHandle,
                    IntPtr.Zero,
                    0, 0,
                    screenWidth, screenHeight,
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOZORDER |
                    Native.NativeMethods.SetWindowPosFlags.SWP_SHOWWINDOW |
                    Native.NativeMethods.SetWindowPosFlags.SWP_NOACTIVATE
                );

                // Explicitly show the window
                Native.NativeMethods.ShowWindow(_windowHandle, Native.NativeMethods.ShowWindowCommands.SW_SHOW);

                Log.Information("Desktop attachment complete");
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

                // Install hooks (CRITICAL: Pass main window handle + DispatcherQueue for thread marshaling)
                _inputManager.InstallHooks(webView.CoreWebView2, webViewHandle, _windowHandle, _dispatcherQueue!);

                // Set control mode from configuration
                if (_config != null)
                {
                    _inputManager.ControlMode = _config.ControlMode;
                    Log.Information($"Control mode set to: {_config.ControlMode}");
                }

                Log.Information("Input hooks installed successfully");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to install input hooks - wallpaper will render but may not be interactive");
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

                // Initialize with WebView2 and DispatcherQueue (for thread marshaling)
                _performanceManager.Initialize(webView.CoreWebView2, _dispatcherQueue!);

                Log.Information("Performance manager initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize performance manager");
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
                _trayIconManager.GoToHomePageRequested += (s, e) => GoToHomePage();
                _trayIconManager.RefreshRequested += (s, e) =>
                {
                    webView.CoreWebView2?.Reload();
                    Log.Information("Page refreshed from tray icon");
                };

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

        // Keyboard shortcut handler for page refresh (Ctrl+R, F5)
        private void MainWindow_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            try
            {
                // Check for Ctrl+R or F5
                // Use WinUI3-compatible keyboard state detection
                var window = Microsoft.UI.Xaml.Window.Current ?? this;
                var coreWindow = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

                // Check if Control key is pressed (WinUI3 compatible way)
                bool isCtrlPressed = (coreWindow & Microsoft.UI.Input.VirtualKeyStates.Down) == Microsoft.UI.Input.VirtualKeyStates.Down;

                if ((e.Key == VirtualKey.R && isCtrlPressed) || e.Key == VirtualKey.F5)
                {
                    // Reload the page
                    webView.CoreWebView2?.Reload();
                    Log.Information("Page reloaded via keyboard shortcut");
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling keyboard shortcut");
            }
        }

        private void ShowUnifiedSettings()
        {
            try
            {
                if (_configManager == null)
                    return;

                // CRITICAL FIX: System tray events fire on Windows Forms thread
                // WinUI 3 windows must be created on UI thread - dispatch to DispatcherQueue
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        // Create and show unified settings window on UI thread
                        var unifiedWindow = new UnifiedSettingsWindow(
                            getConfig: () => _config ?? Models.AppConfig.CreateDefault(),
                            onConfigChanged: async (updatedConfig) =>
                            {
                                // Save configuration
                                await _configManager.SaveConfigAsync(updatedConfig);
                                _config = updatedConfig;

                                // Update input manager control mode
                                if (_inputManager != null)
                                {
                                    _inputManager.ControlMode = updatedConfig.ControlMode;
                                }

                                // Update performance manager
                                if (_performanceManager != null)
                                {
                                    _performanceManager.IsEnabled = updatedConfig.PerformanceOptimizationEnabled;
                                    _performanceManager.BatteryThreshold = updatedConfig.BatteryPauseThreshold;
                                }

                                Log.Information("Configuration updated");
                            },
                            onExitApp: () =>
                            {
                                Log.Information("Exit requested from settings window");
                                Application.Current.Exit();
                            },
                            onRefreshRequested: () =>
                            {
                                // Reload webpage with potentially new URL
                                if (webView.CoreWebView2 != null && _config != null)
                                {
                                    webView.CoreWebView2.Navigate(_config.WallpaperUrl);
                                    Log.Information($"Navigating to updated URL: {_config.WallpaperUrl}");
                                }
                            }
                        );

                        unifiedWindow.Activate();
                        Log.Information("Unified settings window opened successfully");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error creating unified settings window on UI thread");
                        _trayIconManager?.ShowNotification(
                            "Error",
                            $"Failed to open settings: {ex.Message}",
                            System.Windows.Forms.ToolTipIcon.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error dispatching unified settings window creation");
            }
        }

        private void ShowSettings()
        {
            ShowUnifiedSettings();
        }

        private void ShowAbout()
        {
            ShowUnifiedSettings();
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

        private void GoToHomePage()
        {
            try
            {
                if (_config != null && !string.IsNullOrWhiteSpace(_config.WallpaperUrl))
                {
                    Log.Information($"Navigating to home page: {_config.WallpaperUrl}");

                    // CRITICAL FIX: Properly handle UI state like RetryButton_Click
                    // This ensures error panels are hidden and loading is shown
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            // Hide error panel if visible
                            if (ErrorPanel.Visibility == Visibility.Visible)
                            {
                                ErrorPanel.Visibility = Visibility.Collapsed;
                                Log.Information("Error panel hidden before navigation");
                            }

                            // Show loading panel
                            LoadingPanel.Visibility = Visibility.Visible;

                            // Re-enable input forwarding if it was disabled
                            if (_inputManager != null && !_inputManager.IsEnabled)
                            {
                                _inputManager.IsEnabled = true;
                                Log.Information("Input forwarding re-enabled before navigation");
                            }

                            // Navigate to home page
                            if (webView?.CoreWebView2 != null)
                            {
                                webView.CoreWebView2.Navigate(_config.WallpaperUrl);
                                _trayIconManager?.ShowNotification(
                                    "Home Page",
                                    $"Navigating to {_config.WallpaperUrl}",
                                    System.Windows.Forms.ToolTipIcon.Info);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error during home page navigation on UI thread");
                        }
                    });
                }
                else
                {
                    Log.Warning("Cannot navigate to home page - URL not configured");
                    _trayIconManager?.ShowNotification(
                        "Error",
                        "Home page URL not configured",
                        System.Windows.Forms.ToolTipIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error navigating to home page");
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
                Log.Error(ex, "Error during application exit");
            }
        }

        private IntPtr GetWebViewHandle()
        {
            try
            {
                // CRITICAL FIX: WebView2's Chrome windows are CHILD WINDOWS of our main window
                // We must search child windows, not top-level windows!
                //
                // Window hierarchy:
                //   MainWindow (_windowHandle)
                //     └─ Chrome_RenderWidgetHostHWND (the WebView2 render window)
                //         └─ Chrome_WidgetWin_0
                //             └─ Chrome_WidgetWin_1

                Log.Information("Searching for WebView2 Chrome_RenderWidgetHostHWND as child of main window...");

                // Try multiple possible class names that WebView2 might use
                string[] possibleClassNames =
                {
                    "Chrome_RenderWidgetHostHWND",  // Standard name
                    "Chrome_WidgetWin_0",            // Sometimes this is the top-level one
                    "Chrome_WidgetWin_1"             // Fallback
                };

                foreach (var className in possibleClassNames)
                {
                    IntPtr handle = Native.NativeMethods.FindWindowEx(_windowHandle, IntPtr.Zero, className, null);
                    if (handle != IntPtr.Zero)
                    {
                        Log.Information($"Found WebView2 window: {className} (0x{handle:X8})");
                        return handle;
                    }
                }

                // If FindWindowEx doesn't work, enumerate all child windows
                Log.Information("FindWindowEx failed, enumerating all child windows...");
                IntPtr childHandle = IntPtr.Zero;

                Native.NativeMethods.EnumChildWindows(_windowHandle, (hwnd, lparam) =>
                {
                    var className = WorkerWManager.GetWindowClassName(hwnd);
                    Log.Information($"  Found child window: {className} (0x{hwnd:X8})");

                    // WebView2 uses Chrome_* windows
                    if (className.Contains("Chrome_"))
                    {
                        Log.Information($"  -> This looks like WebView2! Using 0x{hwnd:X8}");
                        childHandle = hwnd;
                        return false; // Stop enumeration
                    }

                    return true; // Continue enumeration
                }, IntPtr.Zero);

                if (childHandle != IntPtr.Zero)
                {
                    Log.Information($"Found WebView2 render window via enumeration: 0x{childHandle:X8}");
                    return childHandle;
                }

                Log.Warning("Could not find any Chrome_* child windows!");
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error finding WebView2 handle");
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
                Log.Error(ex, "CookieManager: Failed to restore cookies");
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
                Log.Error(ex, "CookieManager: Failed to save cookies");
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

                // CRITICAL FIX: Always hide loading panel after successful navigation
                LoadingPanel.Visibility = Visibility.Collapsed;

                // CRITICAL FIX: Hide error panel and re-enable input if error was showing
                // This handles the case where user navigates away from error (e.g., "Go to Home Page")
                if (ErrorPanel.Visibility == Visibility.Visible)
                {
                    Log.Information("Hiding error panel after successful navigation");
                    ErrorPanel.Visibility = Visibility.Collapsed;

                    // Re-enable input forwarding
                    if (_inputManager != null)
                    {
                        _inputManager.IsEnabled = true;
                        Log.Information("Input forwarding re-enabled after successful navigation");
                    }
                }
            }
            else
            {
                // Only show error for critical failures, not redirects or temporary issues
                var errorStatus = args.WebErrorStatus.ToString();
                Log.Warning($"Navigation completed with error: {errorStatus}");

                // Don't show error for common non-critical statuses
                if (errorStatus != "OperationCanceled" &&
                    errorStatus != "Unknown" &&
                    !errorStatus.Contains("Redirect"))
                {
                    ShowError($"Failed to load webpage: {errorStatus}\n\nPlease check your internet connection or try a different URL.");
                }
            }
        }

        private void ShowError(string message)
        {
            // CRITICAL FIX: Disable input forwarding when showing error dialog
            // Input hooks interfere with WinUI button clicks
            if (_inputManager != null)
            {
                _inputManager.IsEnabled = false;
                Log.Information("Input forwarding disabled for error dialog");
            }

            // Hide loading, show error
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorMessage.Text = message;

            Log.Error($"ERROR SHOWN: {message}");
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Information("Retry button clicked");
                ErrorPanel.Visibility = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;

                // Re-enable input forwarding after error dismissed
                if (_inputManager != null)
                {
                    _inputManager.IsEnabled = true;
                    Log.Information("Input forwarding re-enabled after retry");
                }

                // Reload the page
                if (webView?.CoreWebView2 != null && _config != null)
                {
                    webView.CoreWebView2.Navigate(_config.WallpaperUrl);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during retry");
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Information("Exit button clicked from error panel");
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during exit");
            }
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
                Log.Error(ex, "Error saving cookies");
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
                    Log.Error(ex, "Error disposing TrayIconManager");
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
                    Log.Error(ex, "Error disposing PerformanceManager");
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
                    Log.Error(ex, "Error disposing InputManager");
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
                    Log.Error(ex, "Error detaching from desktop");
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
                Log.Error(ex, "Error closing WebView2");
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
                Log.Error(ex, "Error during page reload");
            }
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowAbout();
        }
    }
}
