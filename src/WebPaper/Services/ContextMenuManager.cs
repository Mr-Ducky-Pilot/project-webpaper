using Microsoft.Win32;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace WebPaper.Services
{
    /// <summary>
    /// Manages Windows desktop context menu integration for WebPaper
    /// Adds a cascading submenu to the desktop right-click menu with WebPaper options
    /// </summary>
    public class ContextMenuManager
    {
        private const string REGISTRY_PATH = @"Directory\Background\shell\WebPaper";
        private const string COMMAND_STORE_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell";

        private readonly string _executablePath;
        private readonly string _iconPath;

        public ContextMenuManager()
        {
            // Get the current executable path
            _executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            _iconPath = _executablePath; // Use exe as icon source
        }

        /// <summary>
        /// Checks if the current process has administrator privileges
        /// </summary>
        public static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Installs WebPaper context menu to the desktop right-click menu
        /// Requires administrator privileges
        /// </summary>
        public bool InstallContextMenu()
        {
            try
            {
                if (!IsAdministrator())
                {
                    Log.Warning("Context menu installation requires administrator privileges");
                    return false;
                }

                Log.Information("Installing WebPaper desktop context menu...");

                // Create main menu entry under Desktop Background
                using (var key = Registry.ClassesRoot.CreateSubKey(REGISTRY_PATH))
                {
                    if (key == null)
                    {
                        Log.Error("Failed to create registry key for context menu");
                        return false;
                    }

                    // Set menu display name
                    key.SetValue("MUIVerb", "WebPaper");

                    // Set icon (use the exe file)
                    key.SetValue("Icon", _iconPath);

                    // Set position (Top, Bottom, or omit for default)
                    key.SetValue("Position", "Bottom");

                    // Define submenu items (cascade menu)
                    key.SetValue("SubCommands", "WebPaper.Settings;WebPaper.Reload;WebPaper.Home;WebPaper.Toggle;WebPaper.About");
                }

                // Create command store entries for each submenu item
                CreateCommandStoreEntry("WebPaper.Settings", "⚙️ Settings", "--settings");
                CreateCommandStoreEntry("WebPaper.Reload", "🔄 Reload Wallpaper", "--reload");
                CreateCommandStoreEntry("WebPaper.Home", "🏠 Go to Home Page", "--home");
                CreateCommandStoreEntry("WebPaper.Toggle", "⏯️ Toggle Wallpaper", "--toggle");
                CreateCommandStoreEntry("WebPaper.About", "ℹ️ About WebPaper", "--about");

                Log.Information("WebPaper context menu installed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to install context menu");
                return false;
            }
        }

        /// <summary>
        /// Creates a command store entry for a submenu item
        /// </summary>
        private void CreateCommandStoreEntry(string commandName, string displayName, string argument)
        {
            try
            {
                // Create entry in CommandStore
                string commandPath = Path.Combine(COMMAND_STORE_PATH, commandName);

                using (var key = Registry.LocalMachine.CreateSubKey(commandPath))
                {
                    if (key == null)
                    {
                        Log.Error($"Failed to create command store entry: {commandName}");
                        return;
                    }

                    // Set display name
                    key.SetValue("MUIVerb", displayName);

                    // Set icon (use the exe file)
                    key.SetValue("Icon", _iconPath);

                    // Create command subkey
                    using (var cmdKey = key.CreateSubKey("command"))
                    {
                        if (cmdKey != null)
                        {
                            // Set command to launch WebPaper with argument
                            cmdKey.SetValue("", $"\"{_executablePath}\" {argument}");
                        }
                    }
                }

                Log.Information($"Created command store entry: {commandName}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to create command store entry: {commandName}");
            }
        }

        /// <summary>
        /// Uninstalls WebPaper context menu from the desktop right-click menu
        /// Requires administrator privileges
        /// </summary>
        public bool UninstallContextMenu()
        {
            try
            {
                if (!IsAdministrator())
                {
                    Log.Warning("Context menu uninstallation requires administrator privileges");
                    return false;
                }

                Log.Information("Uninstalling WebPaper desktop context menu...");

                // Remove main menu entry
                try
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(REGISTRY_PATH, false);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to delete main context menu entry (may not exist)");
                }

                // Remove command store entries
                string[] commands =
                {
                    "WebPaper.Settings",
                    "WebPaper.Reload",
                    "WebPaper.Home",
                    "WebPaper.Toggle",
                    "WebPaper.About"
                };

                foreach (string command in commands)
                {
                    try
                    {
                        string commandPath = Path.Combine(COMMAND_STORE_PATH, command);
                        Registry.LocalMachine.DeleteSubKeyTree(commandPath, false);
                        Log.Information($"Removed command store entry: {command}");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"Failed to delete command store entry: {command}");
                    }
                }

                Log.Information("WebPaper context menu uninstalled successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to uninstall context menu");
                return false;
            }
        }

        /// <summary>
        /// Checks if WebPaper context menu is currently installed
        /// </summary>
        public bool IsContextMenuInstalled()
        {
            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey(REGISTRY_PATH);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restarts the application with administrator privileges
        /// Used to install/uninstall context menu
        /// </summary>
        public static bool RestartAsAdministrator(string arguments = "")
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule?.FileName ?? "",
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas" // Request admin elevation
                };

                Process.Start(processInfo);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restart as administrator");
                return false;
            }
        }
    }
}
