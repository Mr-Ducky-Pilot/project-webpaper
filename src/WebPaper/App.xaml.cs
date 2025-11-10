using Microsoft.UI.Xaml;
using System;
using System.IO;
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
                // Allocate console for debugging (comment out for production)
                Native.NativeMethods.AllocConsole();
                Console.WriteLine("Console allocated for debugging");

                // Initialize Serilog file logging
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WebPaper",
                    "Logs",
                    "webpaper.log"
                );

                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .WriteTo.File(logPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                Console.WriteLine($"Log file: {logPath}");
            }
            catch (Exception ex)
            {
                // If logging fails, continue anyway
                Console.WriteLine($"Warning: Failed to initialize logging: {ex.Message}");
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

                // Check if WebView2 Runtime is available
                EnsureWebView2Runtime();

                // Create and activate the main window
                Log.Information("Creating MainWindow");
                m_window = new MainWindow();
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
    }
}
