using Microsoft.UI.Xaml;
using System;

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
            this.InitializeComponent();

            // Handle unhandled exceptions
            this.UnhandledException += App_UnhandledException;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                // Check if WebView2 Runtime is available
                EnsureWebView2Runtime();

                // Create and activate the main window
                m_window = new MainWindow();
                m_window.Activate();

                Console.WriteLine("WebPaper started successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL: Application launch failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);

                // Show error dialog
                ShowFatalError(ex.Message);
            }
        }

        private void EnsureWebView2Runtime()
        {
            try
            {
                // Try to get WebView2 version to verify it's installed
                string? version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();

                if (string.IsNullOrEmpty(version))
                {
                    throw new InvalidOperationException("WebView2 Runtime not found");
                }

                Console.WriteLine($"WebView2 Runtime detected: {version}");
            }
            catch (Exception)
            {
                throw new InvalidOperationException(
                    "WebView2 Runtime is required but not installed.\n\n" +
                    "Please download and install it from:\n" +
                    "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
                );
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"UNHANDLED EXCEPTION: {e.Message}");
            Console.WriteLine(e.Exception?.StackTrace);

            // Mark as handled to prevent app crash (for debugging)
            e.Handled = true;

            // In production, you might want to show an error dialog
            ShowFatalError($"An unexpected error occurred: {e.Message}");
        }

        private void ShowFatalError(string message)
        {
            // For now, just write to console
            // In a real app, you'd show a dialog with instructions
            Console.WriteLine($"\n{'='.ToString() repeat 60}");
            Console.WriteLine("FATAL ERROR");
            Console.WriteLine($"{'='.ToString() repeat 60}");
            Console.WriteLine(message);
            Console.WriteLine($"{'='.ToString() repeat 60}\n");

            // TODO: Show actual dialog when running on Windows
            // Use ContentDialog or MessageDialog
        }
    }
}
