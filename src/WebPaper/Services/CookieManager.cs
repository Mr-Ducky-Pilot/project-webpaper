extern alias WebView2;

using WebView2::Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WebPaper.Models;

namespace WebPaper.Services
{
    /// <summary>
    /// Manages cookie persistence using Windows DPAPI encryption
    /// </summary>
    public class CookieManager
    {
        private readonly string _cookieStorePath;
        private readonly byte[] _entropy;

        public CookieManager()
        {
            // Set up secure storage location
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebPaper",
                "Cookies"
            );

            Directory.CreateDirectory(appDataPath);
            _cookieStorePath = Path.Combine(appDataPath, "cookies.dat");

            // Generate entropy for additional encryption security
            _entropy = GenerateEntropy();
        }

        /// <summary>
        /// Saves cookies from WebView2 to encrypted storage
        /// </summary>
        public async Task SaveCookiesAsync(CoreWebView2 webView, string currentUrl)
        {
            try
            {
                Console.WriteLine("CookieManager: Saving cookies...");

                var cookieManager = webView.CookieManager;
                var allCookies = await cookieManager.GetCookiesAsync(currentUrl);

                // Convert to serializable format
                var serializableCookies = new List<SerializableCookie>();

                foreach (var cookie in allCookies)
                {
                    serializableCookies.Add(new SerializableCookie
                    {
                        Name = cookie.Name,
                        Value = cookie.Value,
                        Domain = cookie.Domain,
                        Path = cookie.Path,
                        Expires = cookie.Expires,
                        IsSecure = cookie.IsSecure,
                        IsHttpOnly = cookie.IsHttpOnly,
                        SameSite = (int)cookie.SameSite,
                        IsSession = cookie.IsSession
                    });
                }

                // Create container
                var container = new CookieContainer
                {
                    Cookies = serializableCookies,
                    Url = currentUrl,
                    SavedAt = DateTime.UtcNow,
                    Version = 1
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(container, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                // Encrypt using DPAPI
                var plainBytes = Encoding.UTF8.GetBytes(json);
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser
                );

                // Save to file
                await File.WriteAllBytesAsync(_cookieStorePath, encryptedBytes);

                Console.WriteLine($"CookieManager: Saved {serializableCookies.Count} cookies");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CookieManager ERROR: Failed to save cookies - {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads cookies from encrypted storage and restores to WebView2
        /// </summary>
        public async Task<bool> RestoreCookiesAsync(CoreWebView2 webView)
        {
            try
            {
                if (!File.Exists(_cookieStorePath))
                {
                    Console.WriteLine("CookieManager: No saved cookies found");
                    return false;
                }

                Console.WriteLine("CookieManager: Loading cookies...");

                // Read encrypted file
                var encryptedBytes = await File.ReadAllBytesAsync(_cookieStorePath);

                // Decrypt using DPAPI
                var plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser
                );

                // Deserialize JSON
                var json = Encoding.UTF8.GetString(plainBytes);
                var container = JsonSerializer.Deserialize<CookieContainer>(json);

                if (container == null || container.Cookies == null)
                {
                    Console.WriteLine("CookieManager: Invalid cookie data");
                    return false;
                }

                // Check if cookies are too old (older than 30 days)
                var age = DateTime.UtcNow - container.SavedAt;
                if (age.TotalDays > 30)
                {
                    Console.WriteLine($"CookieManager: Cookies are {age.TotalDays:F0} days old, discarding");
                    File.Delete(_cookieStorePath);
                    return false;
                }

                // Restore cookies to WebView2
                var cookieManager = webView.CookieManager;
                int restoredCount = 0;

                foreach (var cookieData in container.Cookies)
                {
                    try
                    {
                        var cookie = cookieManager.CreateCookie(
                            cookieData.Name,
                            cookieData.Value,
                            cookieData.Domain,
                            cookieData.Path
                        );

                        cookie.IsSecure = cookieData.IsSecure;
                        cookie.IsHttpOnly = cookieData.IsHttpOnly;
                        cookie.SameSite = (CoreWebView2CookieSameSiteKind)cookieData.SameSite;

                        // Set expiration if not a session cookie
                        if (!cookieData.IsSession && cookieData.Expires.HasValue)
                        {
                            cookie.Expires = cookieData.Expires.Value;
                        }

                        cookieManager.AddOrUpdateCookie(cookie);
                        restoredCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"CookieManager: Failed to restore cookie {cookieData.Name} - {ex.Message}");
                    }
                }

                Console.WriteLine($"CookieManager: Restored {restoredCount}/{container.Cookies.Count} cookies");
                Console.WriteLine($"CookieManager: Cookies were saved at {container.SavedAt:yyyy-MM-dd HH:mm:ss} UTC");

                return restoredCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CookieManager ERROR: Failed to restore cookies - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes all saved cookies
        /// </summary>
        public void ClearSavedCookies()
        {
            try
            {
                if (File.Exists(_cookieStorePath))
                {
                    File.Delete(_cookieStorePath);
                    Console.WriteLine("CookieManager: Cleared saved cookies");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CookieManager ERROR: Failed to clear cookies - {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if saved cookies exist
        /// </summary>
        public bool HasSavedCookies()
        {
            return File.Exists(_cookieStorePath);
        }

        /// <summary>
        /// Gets information about saved cookies
        /// </summary>
        public async Task<(int count, DateTime savedAt, string url)?> GetCookieInfoAsync()
        {
            try
            {
                if (!File.Exists(_cookieStorePath))
                    return null;

                var encryptedBytes = await File.ReadAllBytesAsync(_cookieStorePath);
                var plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser
                );

                var json = Encoding.UTF8.GetString(plainBytes);
                var container = JsonSerializer.Deserialize<CookieContainer>(json);

                if (container == null)
                    return null;

                return (container.Cookies.Count, container.SavedAt, container.Url);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Generates entropy for DPAPI encryption
        /// </summary>
        private byte[] GenerateEntropy()
        {
            // Use machine and user specific data for entropy
            var machineId = Environment.MachineName;
            var userId = Environment.UserName;
            var entropyString = $"WebPaper-{machineId}-{userId}";

            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(entropyString));
            }
        }
    }
}
