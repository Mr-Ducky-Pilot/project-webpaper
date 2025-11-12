using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using WebPaper.Models;
using CoreWebView2 = Microsoft.Web.WebView2.Core.CoreWebView2;
using CoreWebView2Cookie = Microsoft.Web.WebView2.Core.CoreWebView2Cookie;
using CoreWebView2CookieManager = Microsoft.Web.WebView2.Core.CoreWebView2CookieManager;
using CoreWebView2CookieSameSiteKind = Microsoft.Web.WebView2.Core.CoreWebView2CookieSameSiteKind;

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
                Log.Information("Saving cookies");

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

                Log.Information("Saved {CookieCount} cookies", serializableCookies.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save cookies");
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
                    Log.Information("No saved cookies found");
                    return false;
                }

                Log.Information("Loading cookies");

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
                    Log.Warning("Invalid cookie data");
                    return false;
                }

                // Check if cookies are too old (older than 30 days)
                var age = DateTime.UtcNow - container.SavedAt;
                if (age.TotalDays > 30)
                {
                    Log.Information("Cookies are {Days:F0} days old, discarding", age.TotalDays);
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
                        Log.Error(ex, "Failed to restore cookie {CookieName}", cookieData.Name);
                    }
                }

                Log.Information("Restored {RestoredCount}/{TotalCount} cookies", restoredCount, container.Cookies.Count);
                Log.Information("Cookies were saved at {SavedAt:yyyy-MM-dd HH:mm:ss} UTC", container.SavedAt);

                return restoredCount > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore cookies");
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
                    Log.Information("Cleared saved cookies");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear cookies");
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
