using System;
using System.Text;
using System.Threading;
using WebPaper.Native;
using static WebPaper.Native.NativeMethods;

namespace WebPaper.Core
{
    /// <summary>
    /// Manages the WorkerW window technique to render windows behind desktop icons
    /// </summary>
    public class WorkerWManager
    {
        private IntPtr _workerW = IntPtr.Zero;
        private bool _isInitialized = false;

        /// <summary>
        /// Gets the WorkerW window handle if found
        /// </summary>
        public IntPtr WorkerWHandle => _workerW;

        /// <summary>
        /// Gets whether the WorkerW window has been successfully found
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Finds or creates the WorkerW window that sits between desktop wallpaper and icons
        /// </summary>
        /// <returns>Handle to WorkerW window, or Progman as fallback</returns>
        public IntPtr FindOrCreateWorkerW()
        {
            try
            {
                // Step 1: Find the Progman window (Program Manager)
                IntPtr progman = FindWindow("Progman", null);
                if (progman == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Could not find Progman window. This is unexpected on Windows.");
                }

                // Step 2: Send the undocumented message 0x052C to Progman
                // This message causes Windows to spawn a WorkerW window behind the desktop icons
                SendMessageTimeout(
                    progman,
                    WM_SPAWN_WORKER,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    SendMessageTimeoutFlags.SMTO_NORMAL,
                    1000,
                    out IntPtr result);

                // Small delay to let Windows create the WorkerW window
                Thread.Sleep(100);

                // Step 3: Enumerate all windows to find the WorkerW that contains SHELLDLL_DefView
                IntPtr workerW = IntPtr.Zero;
                EnumWindows((topHandle, topParamHandle) =>
                {
                    // Look for the SHELLDLL_DefView window (this is the desktop icon container)
                    IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);

                    if (shellView != IntPtr.Zero)
                    {
                        // We found SHELLDLL_DefView, now get the next WorkerW window
                        // This WorkerW sits between the wallpaper and the desktop icons
                        workerW = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                    }

                    return true; // Continue enumeration
                }, IntPtr.Zero);

                if (workerW != IntPtr.Zero)
                {
                    _workerW = workerW;
                    _isInitialized = true;
                    return workerW;
                }
                else
                {
                    // Fallback: If WorkerW not found (can happen on Windows 11 24H2),
                    // try using Progman directly
                    _workerW = progman;
                    _isInitialized = true;
                    return progman;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to find or create WorkerW window", ex);
            }
        }

        /// <summary>
        /// Attaches the specified window to the desktop (behind icons)
        /// </summary>
        /// <param name="windowHandle">Handle to the window to attach</param>
        public void AttachWindowToDesktop(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Invalid window handle", nameof(windowHandle));
            }

            // Ensure we have a WorkerW window
            if (!_isInitialized)
            {
                FindOrCreateWorkerW();
            }

            // Set the window as a child of WorkerW
            // This makes it render behind desktop icons but above the wallpaper
            IntPtr result = SetParent(windowHandle, _workerW);

            if (result == IntPtr.Zero)
            {
                uint error = GetLastError();
                throw new InvalidOperationException($"Failed to set window parent. Error code: {error}");
            }
        }

        /// <summary>
        /// Detaches the window from the desktop, restoring it to normal window behavior
        /// </summary>
        /// <param name="windowHandle">Handle to the window to detach</param>
        public void DetachWindowFromDesktop(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Invalid window handle", nameof(windowHandle));
            }

            // Setting parent to IntPtr.Zero makes it a top-level window again
            SetParent(windowHandle, IntPtr.Zero);
        }

        /// <summary>
        /// Attempts to refresh the WorkerW window (useful if it disappears on Windows 11 24H2)
        /// </summary>
        public bool RefreshWorkerW()
        {
            _isInitialized = false;
            _workerW = IntPtr.Zero;

            try
            {
                FindOrCreateWorkerW();
                return _isInitialized;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the class name of a window
        /// </summary>
        public static string GetWindowClassName(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return string.Empty;

            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        /// <summary>
        /// Validates that the WorkerW window still exists
        /// </summary>
        public bool ValidateWorkerW()
        {
            if (!_isInitialized || _workerW == IntPtr.Zero)
                return false;

            // Check if the window is still visible and valid
            return IsWindowVisible(_workerW);
        }

        /// <summary>
        /// Debug method to enumerate all top-level windows
        /// </summary>
        public static void DebugEnumerateWindows()
        {
            Console.WriteLine("=== Enumerating Top-Level Windows ===");
            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    string className = GetWindowClassName(hWnd);
                    GetWindowRect(hWnd, out RECT rect);
                    Console.WriteLine($"Window: {hWnd:X8} | Class: {className,-30} | Rect: {rect}");
                }
                return true;
            }, IntPtr.Zero);
            Console.WriteLine("=== End Enumeration ===");
        }
    }
}
