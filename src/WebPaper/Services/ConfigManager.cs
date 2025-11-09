using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WebPaper.Models;

namespace WebPaper.Services
{
    /// <summary>
    /// Manages application configuration persistence
    /// </summary>
    public class ConfigManager
    {
        private readonly string _configFilePath;
        private AppConfig? _currentConfig;

        public ConfigManager()
        {
            // Set up config file location in AppData
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebPaper"
            );

            Directory.CreateDirectory(appDataPath);
            _configFilePath = Path.Combine(appDataPath, "config.json");
        }

        /// <summary>
        /// Loads configuration from disk, or creates default if not exists
        /// </summary>
        public async Task<AppConfig> LoadConfigAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    Console.WriteLine("Config file not found. Creating default configuration...");
                    var defaultConfig = AppConfig.CreateDefault();
                    await SaveConfigAsync(defaultConfig);
                    _currentConfig = defaultConfig;
                    return defaultConfig;
                }

                Console.WriteLine($"Loading config from: {_configFilePath}");

                var json = await File.ReadAllTextAsync(_configFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);

                if (config == null)
                {
                    Console.WriteLine("ERROR: Failed to deserialize config. Using default.");
                    config = AppConfig.CreateDefault();
                }
                else if (!config.Validate())
                {
                    Console.WriteLine("WARNING: Config validation failed. Using default.");
                    config = AppConfig.CreateDefault();
                }

                _currentConfig = config;
                Console.WriteLine($"Config loaded successfully. URL: {config.WallpaperUrl}");

                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR loading config: {ex.Message}");
                Console.WriteLine("Using default configuration.");
                var defaultConfig = AppConfig.CreateDefault();
                _currentConfig = defaultConfig;
                return defaultConfig;
            }
        }

        /// <summary>
        /// Saves configuration to disk
        /// </summary>
        public async Task SaveConfigAsync(AppConfig config)
        {
            try
            {
                if (!config.Validate())
                {
                    throw new ArgumentException("Invalid configuration");
                }

                Console.WriteLine($"Saving config to: {_configFilePath}");

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true // Pretty print for readability
                };

                var json = JsonSerializer.Serialize(config, options);
                await File.WriteAllTextAsync(_configFilePath, json);

                _currentConfig = config;
                Console.WriteLine("Config saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR saving config: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the current configuration (cached)
        /// </summary>
        public AppConfig? GetCurrentConfig()
        {
            return _currentConfig;
        }

        /// <summary>
        /// Updates a specific config value and saves
        /// </summary>
        public async Task UpdateWallpaperUrlAsync(string url)
        {
            if (_currentConfig == null)
                await LoadConfigAsync();

            if (_currentConfig != null)
            {
                _currentConfig.WallpaperUrl = url;
                await SaveConfigAsync(_currentConfig);
            }
        }

        /// <summary>
        /// Marks first run as complete
        /// </summary>
        public async Task CompleteFirstRunAsync()
        {
            if (_currentConfig == null)
                await LoadConfigAsync();

            if (_currentConfig != null)
            {
                _currentConfig.IsFirstRun = false;
                _currentConfig.LastLaunchDate = DateTime.Now;
                await SaveConfigAsync(_currentConfig);
            }
        }

        /// <summary>
        /// Updates last launch date
        /// </summary>
        public async Task UpdateLastLaunchAsync()
        {
            if (_currentConfig == null)
                await LoadConfigAsync();

            if (_currentConfig != null)
            {
                _currentConfig.LastLaunchDate = DateTime.Now;
                await SaveConfigAsync(_currentConfig);
            }
        }

        /// <summary>
        /// Resets configuration to default
        /// </summary>
        public async Task ResetToDefaultAsync()
        {
            Console.WriteLine("Resetting configuration to default...");
            var defaultConfig = AppConfig.CreateDefault();
            await SaveConfigAsync(defaultConfig);
        }

        /// <summary>
        /// Gets the config file path
        /// </summary>
        public string GetConfigFilePath() => _configFilePath;

        /// <summary>
        /// Checks if config file exists
        /// </summary>
        public bool ConfigFileExists() => File.Exists(_configFilePath);
    }
}
