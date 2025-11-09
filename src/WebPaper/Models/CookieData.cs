using System;
using System.Collections.Generic;

namespace WebPaper.Models
{
    /// <summary>
    /// Represents a serializable cookie for persistence
    /// </summary>
    public class SerializableCookie
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Path { get; set; } = "/";
        public double? Expires { get; set; } // Unix timestamp
        public bool IsSecure { get; set; }
        public bool IsHttpOnly { get; set; }
        public int SameSite { get; set; } // 0 = None, 1 = Lax, 2 = Strict
        public bool IsSession { get; set; }

        public override string ToString()
        {
            return $"{Name}={Value} (Domain: {Domain}, Session: {IsSession})";
        }
    }

    /// <summary>
    /// Container for cookie collection with metadata
    /// </summary>
    public class CookieContainer
    {
        public List<SerializableCookie> Cookies { get; set; } = new List<SerializableCookie>();
        public DateTime SavedAt { get; set; }
        public string Url { get; set; } = string.Empty;
        public int Version { get; set; } = 1;

        public CookieContainer()
        {
            SavedAt = DateTime.UtcNow;
        }
    }
}
