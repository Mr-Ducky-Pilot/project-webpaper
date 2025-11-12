using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Serilog;
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
        public event EventHandler? RefreshRequested;

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

                // Create context menu (for right-click)
                var contextMenu = new ContextMenuStrip();

                // Refresh page
                var refreshItem = new ToolStripMenuItem("🔄 Refresh Page");
                refreshItem.Click += (s, e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
                contextMenu.Items.Add(refreshItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // Open Settings (same as left-click)
                var settingsItem = new ToolStripMenuItem("⚙️ Open Settings...");
                settingsItem.Click += (s, e) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
                contextMenu.Items.Add(settingsItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // Exit
                var exitItem = new ToolStripMenuItem("🚪 Exit WebPaper");
                exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
                contextMenu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = contextMenu;

                // Single left-click to show settings (replaces double-click)
                _notifyIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
                    }
                };

                Log.Information("System tray icon initialized");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize system tray icon");
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
                Log.Error(ex, "Failed to show notification");
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
                    Log.Warning("Icon not found at {IconPath}, using system icon", iconPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load custom icon");
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
                Log.Information("TrayIconManager disposed");
            }
        }
    }
}
