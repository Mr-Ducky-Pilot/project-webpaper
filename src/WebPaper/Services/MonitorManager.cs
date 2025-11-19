using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WebPaper.Services
{
    /// <summary>
    /// Manages multi-monitor detection and positioning
    /// Handles primary/secondary monitor selection and window placement
    /// </summary>
    public class MonitorManager
    {
        #region P/Invoke Declarations

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        private const uint MONITORINFOF_PRIMARY = 0x00000001;

        #endregion

        /// <summary>
        /// Represents information about a display monitor
        /// </summary>
        public class MonitorInfo
        {
            public int Index { get; set; }
            public string DeviceName { get; set; } = "";
            public int Left { get; set; }
            public int Top { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsPrimary { get; set; }
            public int WorkAreaLeft { get; set; }
            public int WorkAreaTop { get; set; }
            public int WorkAreaWidth { get; set; }
            public int WorkAreaHeight { get; set; }

            public override string ToString()
            {
                string type = IsPrimary ? "Primary" : "Secondary";
                return $"{type} Monitor {Index + 1}: {Width}x{Height} at ({Left}, {Top}) - {DeviceName}";
            }
        }

        /// <summary>
        /// Gets information about all connected monitors
        /// </summary>
        public List<MonitorInfo> GetAllMonitors()
        {
            var monitors = new List<MonitorInfo>();
            int monitorIndex = 0;

            bool MonitorEnum(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
            {
                MONITORINFOEX mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(mi);

                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    var monitor = new MonitorInfo
                    {
                        Index = monitorIndex++,
                        DeviceName = mi.szDevice,
                        Left = mi.rcMonitor.Left,
                        Top = mi.rcMonitor.Top,
                        Width = mi.rcMonitor.Width,
                        Height = mi.rcMonitor.Height,
                        IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                        WorkAreaLeft = mi.rcWork.Left,
                        WorkAreaTop = mi.rcWork.Top,
                        WorkAreaWidth = mi.rcWork.Width,
                        WorkAreaHeight = mi.rcWork.Height
                    };

                    monitors.Add(monitor);
                    Log.Information("Detected monitor: {Monitor}", monitor.ToString());
                }

                return true; // Continue enumeration
            }

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnum, IntPtr.Zero);

            // Sort monitors - primary first, then by left position
            monitors.Sort((a, b) =>
            {
                if (a.IsPrimary != b.IsPrimary)
                    return b.IsPrimary.CompareTo(a.IsPrimary); // Primary first
                return a.Left.CompareTo(b.Left); // Then by position
            });

            // Reassign indices after sorting
            for (int i = 0; i < monitors.Count; i++)
            {
                monitors[i].Index = i;
            }

            return monitors;
        }

        /// <summary>
        /// Gets the primary monitor information
        /// </summary>
        public MonitorInfo? GetPrimaryMonitor()
        {
            var monitors = GetAllMonitors();
            var primary = monitors.Find(m => m.IsPrimary);

            if (primary == null && monitors.Count > 0)
            {
                // Fallback: if no primary flag detected, assume first monitor at (0,0) is primary
                primary = monitors.Find(m => m.Left == 0 && m.Top == 0);
                if (primary != null)
                {
                    Log.Warning("Primary flag not detected, using monitor at (0,0) as primary");
                }
                else
                {
                    // Last resort: use first monitor
                    primary = monitors[0];
                    Log.Warning("No monitor at (0,0), using first detected monitor as primary");
                }
            }

            Log.Information($"GetPrimaryMonitor returning: {primary}");
            return primary;
        }

        /// <summary>
        /// Gets monitor by index (0-based)
        /// Index 0 is always primary after sorting
        /// </summary>
        public MonitorInfo? GetMonitorByIndex(int index)
        {
            var monitors = GetAllMonitors();

            if (index < 0 || index >= monitors.Count)
            {
                Log.Warning($"Monitor index {index} out of range (0-{monitors.Count - 1}), using primary");
                return GetPrimaryMonitor();
            }

            var monitor = monitors[index];
            Log.Information($"GetMonitorByIndex({index}) returning: {monitor}");
            return monitor;
        }

        /// <summary>
        /// Gets monitor by device name
        /// </summary>
        public MonitorInfo? GetMonitorByDeviceName(string deviceName)
        {
            var monitors = GetAllMonitors();
            return monitors.Find(m => m.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the total monitor count
        /// </summary>
        public int GetMonitorCount()
        {
            return GetAllMonitors().Count;
        }

        /// <summary>
        /// Creates a human-readable description of all monitors
        /// </summary>
        public string GetMonitorSummary()
        {
            var monitors = GetAllMonitors();
            var sb = new StringBuilder();

            if (monitors.Count == 0)
            {
                sb.AppendLine("No monitors detected");
            }
            else if (monitors.Count == 1)
            {
                var monitor = monitors[0];
                sb.AppendLine($"Single monitor detected:");
                sb.AppendLine($"  Resolution: {monitor.Width} x {monitor.Height}");
                sb.AppendLine($"  Device: {monitor.DeviceName}");
            }
            else
            {
                sb.AppendLine($"{monitors.Count} monitors detected:");
                foreach (var monitor in monitors)
                {
                    sb.AppendLine($"  {monitor}");
                }
            }

            return sb.ToString();
        }
    }
}
