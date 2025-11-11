using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Application = Microsoft.UI.Xaml.Application;

namespace WebPaper.Services
{
    /// <summary>
    /// Manages the system tray icon and context menu
    /// </summary>
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private bool _disposed = false;

        public event EventHandler? ShowSettingsRequested;
        public event EventHandler? ShowAboutRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler? ToggleWallpaperRequested;
        public event EventHandler? GoToHomePageRequested;

        public void Initialize()
        {
            try
            {
                // Create notify icon with custom icon
                _notifyIcon = new NotifyIcon
                {
                    Icon = LoadCustomIcon(),
                    Text = "WebPaper - Interactive Wallpaper",
                    Visible = true
                };

                // Create context menu
                var contextMenu = new ContextMenuStrip();

                // Wallpaper control
                var toggleItem = new ToolStripMenuItem("Enable Wallpaper")
                {
                    CheckOnClick = true,
                    Checked = true
                };
                toggleItem.Click += (s, e) =>
                {
                    ToggleWallpaperRequested?.Invoke(this, EventArgs.Empty);
                    toggleItem.Text = toggleItem.Checked ? "Disable Wallpaper" : "Enable Wallpaper";
                };
                contextMenu.Items.Add(toggleItem);

                // Go to Home Page
                var homePageItem = new ToolStripMenuItem("🏠 Go to Home Page");
                homePageItem.Click += (s, e) => GoToHomePageRequested?.Invoke(this, EventArgs.Empty);
                contextMenu.Items.Add(homePageItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // Settings
                var settingsItem = new ToolStripMenuItem("⚙️ Settings...");
                settingsItem.Click += (s, e) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
                contextMenu.Items.Add(settingsItem);

                // About
                var aboutItem = new ToolStripMenuItem("ℹ️ About WebPaper");
                aboutItem.Click += (s, e) => ShowAboutRequested?.Invoke(this, EventArgs.Empty);
                contextMenu.Items.Add(aboutItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // Exit
                var exitItem = new ToolStripMenuItem("🚪 Exit");
                exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
                contextMenu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = contextMenu;

                // Double-click to show settings
                _notifyIcon.DoubleClick += (s, e) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty);

                Console.WriteLine("TrayIconManager: System tray icon initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TrayIconManager ERROR: Failed to initialize - {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a notification balloon
        /// </summary>
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TrayIconManager: Failed to show notification - {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the tooltip text
        /// </summary>
        public void UpdateTooltip(string text)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            }
        }

        /// <summary>
        /// Shows the tray icon
        /// </summary>
        public void Show()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
            }
        }

        /// <summary>
        /// Hides the tray icon
        /// </summary>
        public void Hide()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }

        /// <summary>
        /// Loads custom icon from Assets or falls back to system icon
        /// </summary>
        private Icon LoadCustomIcon()
        {
            try
            {
                // Try to load from Assets folder (relative to executable)
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var iconPath = Path.Combine(appDir, "Assets", "app.ico");

                if (File.Exists(iconPath))
                {
                    // Load .ico file directly (supports multiple sizes)
                    return new Icon(iconPath, new Size(16, 16));
                }
                else
                {
                    Console.WriteLine($"TrayIconManager: Icon not found at {iconPath}, using system icon");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TrayIconManager: Failed to load custom icon - {ex.Message}");
            }

            // Fallback to system application icon
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _notifyIcon?.Dispose();
                _notifyIcon = null;
                _disposed = true;
                Console.WriteLine("TrayIconManager: Disposed");
            }
        }
    }
}
