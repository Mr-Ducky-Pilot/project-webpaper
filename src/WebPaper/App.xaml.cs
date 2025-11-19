using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading;
using Serilog;
using CoreWebView2Environment = Microsoft.Web.WebView2.Core.CoreWebView2Environment;

namespace WebPaper
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? m_window;
        private static Mutex? _singleInstanceMutex;
        private const string MUTEX_NAME = "WebPaper_SingleInstance_Mutex";

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            // Allocate console for debugging
            InitializeConsoleAndLogging();

            this.InitializeComponent();

            // Handle unhandled exceptions
            this.UnhandledException += App_UnhandledException;

            Log.Information("=== WebPaper Application Starting ===");
            Log.Information("Version: 1.0.0");
            Log.Information("OS: {OS}", Environment.OSVersion);
        }

        private void InitializeConsoleAndLogging()
        {
            try
            {
                // Initialize Serilog file logging (production mode - no console)
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WebPaper",
                    "Logs",
                    "webpaper.log"
                );

                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information() // Changed from Debug to Information for production
                    .WriteTo.File(logPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                Log.Information("Logging initialized successfully. Log file: {LogPath}", logPath);
            }
            catch (Exception ex)
            {
                // If logging fails, try to show error via MessageBox
                try
                {
                    Native.NativeMethods.MessageBox(
                        IntPtr.Zero,
                        $"Failed to initialize logging: {ex.Message}\n\nThe application will continue but errors will not be logged.",
                        "WebPaper - Logging Error",
                        Native.NativeMethods.MB_OK | Native.NativeMethods.MB_ICONWARNING
                    );
                }
                catch
                {
                    // If even that fails, just continue
                }
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                Log.Information("OnLaunched called");

                // Handle command-line arguments from desktop context menu
                string[] commandArgs = Environment.GetCommandLineArgs();

                // Check for special commands that don't need single-instance check
                bool isSpecialCommand = false;
                if (commandArgs.Length > 1)
                {
                    string argument = commandArgs[1].ToLower();
                    isSpecialCommand = argument == "--install-context-menu" ||
                                      argument == "--uninstall-context-menu";
                }

                // Single instance check (skip for special installation commands)
                if (!isSpecialCommand)
                {
                    bool createdNew;
                    _singleInstanceMutex = new Mutex(true, MUTEX_NAME, out createdNew);

                    if (!createdNew)
                    {
                        // Another instance is already running
                        Log.Information("Another instance of WebPaper is already running. Exiting.");
                        Native.NativeMethods.MessageBox(
                            IntPtr.Zero,
                            "WebPaper is already running!\n\nCheck your system tray for the WebPaper icon.",
                            "WebPaper",
                            Native.NativeMethods.MB_OK | Native.NativeMethods.MB_ICONINFORMATION
                        );
                        Current.Exit();
                        return;
                    }
                }
                if (commandArgs.Length > 1)
                {
                    string argument = commandArgs[1].ToLower();
                    Log.Information($"Processing command-line argument: {argument}");

                    // Handle special installation commands
                    if (argument == "--install-context-menu")
                    {
                        HandleContextMenuInstallation();
                        Current.Exit();
                        return;
                    }
                    else if (argument == "--uninstall-context-menu")
                    {
                        HandleContextMenuUninstallation();
                        Current.Exit();
                        return;
                    }
                }

                // Check if WebView2 Runtime is available
                EnsureWebView2Runtime();

                // Create and activate the main window
                Log.Information("Creating MainWindow");
                m_window = new MainWindow(commandArgs.Length > 1 ? commandArgs[1] : null);
                m_window.Activate();

                Log.Information("WebPaper started successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "FATAL: Application launch failed");
                ShowFatalError($"WebPaper failed to start:\n\n{ex.Message}\n\nPlease check the log file at:\n%LocalAppData%\\WebPaper\\Logs");
                Current.Exit();
            }
        }

        private void EnsureWebView2Runtime()
        {
            try
            {
                Log.Information("Checking for WebView2 Runtime");

                // Try to get WebView2 version to verify it's installed
                string? version = CoreWebView2Environment.GetAvailableBrowserVersionString();

                if (string.IsNullOrEmpty(version))
                {
                    Log.Error("WebView2 Runtime not found");
                    throw new InvalidOperationException("WebView2 Runtime not found");
                }

                Log.Information("WebView2 Runtime detected: {Version}", version);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to detect WebView2 Runtime");
                throw new InvalidOperationException(
                    "WebView2 Runtime is required but not installed.\n\n" +
                    "Please download and install it from:\n" +
                    "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                    ex
                );
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "UNHANDLED EXCEPTION: {Message}", e.Message);

            // Mark as handled to prevent app crash (for debugging)
            e.Handled = true;

            // Show error dialog
            ShowFatalError($"An unexpected error occurred:\n\n{e.Message}\n\nPlease check the log file at:\n%LocalAppData%\\WebPaper\\Logs");
        }

        private void ShowFatalError(string message)
        {
            try
            {
                Log.Error("Showing fatal error dialog: {Message}", message);

                // Show MessageBox with error
                Native.NativeMethods.MessageBox(
                    IntPtr.Zero,
                    message,
                    "WebPaper - Fatal Error",
                    Native.NativeMethods.MB_OK | Native.NativeMethods.MB_ICONERROR
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to show error dialog");
                // If we can't even show the error, just log it
            }
        }

        /// <summary>
        /// Handles context menu installation request
        /// </summary>
        private void HandleContextMenuInstallation()
        {
            try
            {
                var contextMenuManager = new Services.ContextMenuManager();
                if (contextMenuManager.InstallContextMenu())
                {
                    Log.Information("Desktop context menu installed successfully");
                    Native.NativeMethods.MessageBox(
                        IntPtr.Zero,
                        "WebPaper has been added to your desktop right-click menu!\n\nRight-click on your desktop to see the WebPaper menu.",
                        "WebPaper - Context Menu Installed",
                        Native.NativeMethods.MB_OK | Native.NativeMethods.MB_ICONINFORMATION
                    );
                }
                else
                {
                    Log.Error("Failed to install desktop context menu");
                    Native.NativeMethods.MessageBox(
                        IntPtr.Zero,
                        "Failed to install desktop context menu.\n\nPlease make sure you run as Administrator.",
                        "WebPaper - Installation Failed",
                        Native.NativeMethods.MB_OK | Native.NativeMethods.MB_ICONERROR
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error installing context menu");
            }
        }

        /// <summary>
        /// Handles context menu uninstallation request
        /// </summary>
        private void HandleContextMenuUninstallation()
        {
            try
            {
                var contextMenuManager = new Services.ContextMenuManager();
                if (contextMenuManager.UninstallContextMenu())
                {
                    Log.Information("Desktop context menu uninstalled successfully");
                    Native.NativeMethods.MessageBox(
                        IntPtr.Zero,
                        "WebPaper has been removed from your desktop right-click menu.",
                        "WebPaper - Context Menu Uninstalled",
                        Native.NativeMethods.MB_OK | Native.NativeMethods.MB_ICONINFORMATION
                    );
                }
                else
                {
                    Log.Error("Failed to uninstall desktop context menu");
                    Native.NativeMethods.MessageBox(
                        IntPtr.Zero,
                        "Failed to uninstall desktop context menu.\n\nPlease make sure you run as Administrator.",
                        "WebPaper - Uninstallation Failed",
                        Native.NativeMethods.MB_OK | Native.NativeMethods.MB_ICONERROR
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error uninstalling context menu");
            }
        }
    }
}
