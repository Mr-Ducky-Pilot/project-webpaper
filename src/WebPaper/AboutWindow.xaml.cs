using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.Reflection;
using Serilog;
using Windows.Graphics;
using WinRT.Interop;

namespace WebPaper
{
    public sealed partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            this.InitializeComponent();

            // Set window size (increased for new content)
            SetWindowSize(650, 800);

            LoadVersionInfo();
            LoadSystemInfo();
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

        private void LoadVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                VersionText.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";
            }
            catch
            {
                VersionText.Text = "Version 1.0.0";
            }
        }

        private void LoadSystemInfo()
        {
            try
            {
                var os = Environment.OSVersion;
                var dotnetVersion = Environment.Version;
                var architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";

                SystemInfoText.Text = $"Windows: {os.Version} ({architecture})\n" +
                                     $".NET Runtime: {dotnetVersion}\n" +
                                     $"Machine: {Environment.MachineName}";
            }
            catch
            {
                SystemInfoText.Text = "System information unavailable";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void UserGuideButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/Mr-Ducky-Pilot/project-webpaper/blob/main/USER_GUIDE.md");
        }

        private void ReportBugButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/Mr-Ducky-Pilot/project-webpaper/issues/new?template=bug_report.md");
        }

        private void FeatureRequestButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/Mr-Ducky-Pilot/project-webpaper/issues/new?template=feature_request.md");
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/Mr-Ducky-Pilot/project-webpaper");
        }

        private void LinkedInButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.linkedin.com/in/omprakash-jat/");
        }

        private void OpenUrl(string url)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error opening URL");
            }
        }
    }
}
