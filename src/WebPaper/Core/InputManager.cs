using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WebPaper.Models;
using WebPaper.Native;
using static WebPaper.Native.NativeMethods;
using CoreWebView2 = Microsoft.Web.WebView2.Core.CoreWebView2;

namespace WebPaper.Core
{
    /// <summary>
    /// Manages low-level input hooks and forwards events to WebView2
    /// </summary>
    public class InputManager : IDisposable
    {
        private IntPtr _mouseHookId = IntPtr.Zero;
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private HookProc? _mouseHookCallback;
        private HookProc? _keyboardHookCallback;
        private CoreWebView2? _webView;
        private IntPtr _webViewHandle = IntPtr.Zero;
        private bool _isEnabled = false;
        private bool _disposed = false;

        // Performance tracking
        private DateTime _lastEventTime = DateTime.Now;
        private int _eventCount = 0;

        /// <summary>
        /// Gets or sets whether input forwarding is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    Console.WriteLine($"InputManager: Forwarding {(_isEnabled ? "ENABLED" : "DISABLED")}");
                }
            }
        }

        /// <summary>
        /// Gets whether hooks are currently installed
        /// </summary>
        public bool HooksInstalled => _mouseHookId != IntPtr.Zero || _keyboardHookId != IntPtr.Zero;

        /// <summary>
        /// Installs low-level mouse and keyboard hooks
        /// </summary>
        public void InstallHooks(CoreWebView2 webView, IntPtr webViewHandle)
        {
            if (HooksInstalled)
            {
                Console.WriteLine("InputManager: Hooks already installed");
                return;
            }

            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _webViewHandle = webViewHandle;

            try
            {
                // Keep references to prevent garbage collection
                _mouseHookCallback = MouseHookProc;
                _keyboardHookCallback = KeyboardHookProc;

                // Install mouse hook
                _mouseHookId = SetWindowsHookEx(
                    HookType.WH_MOUSE_LL,
                    _mouseHookCallback,
                    IntPtr.Zero,
                    0
                );

                if (_mouseHookId == IntPtr.Zero)
                {
                    uint error = GetLastError();
                    throw new InvalidOperationException($"Failed to install mouse hook. Error: {error}");
                }

                // Install keyboard hook
                _keyboardHookId = SetWindowsHookEx(
                    HookType.WH_KEYBOARD_LL,
                    _keyboardHookCallback,
                    IntPtr.Zero,
                    0
                );

                if (_keyboardHookId == IntPtr.Zero)
                {
                    uint error = GetLastError();
                    UnhookWindowsHookEx(_mouseHookId);
                    _mouseHookId = IntPtr.Zero;
                    throw new InvalidOperationException($"Failed to install keyboard hook. Error: {error}");
                }

                _isEnabled = true;
                Console.WriteLine("InputManager: Hooks installed successfully");
                Console.WriteLine($"  Mouse Hook: 0x{_mouseHookId:X8}");
                Console.WriteLine($"  Keyboard Hook: 0x{_keyboardHookId:X8}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager ERROR: Failed to install hooks - {ex.Message}");
                UninstallHooks();
                throw;
            }
        }

        /// <summary>
        /// Removes installed hooks
        /// </summary>
        public void UninstallHooks()
        {
            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }

            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }

            _isEnabled = false;
            Console.WriteLine("InputManager: Hooks uninstalled");
        }

        /// <summary>
        /// Low-level mouse hook callback
        /// </summary>
        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                // CRITICAL: Process quickly to avoid timeout (must complete in <200ms)
                if (nCode >= HC_ACTION && _isEnabled && _webView != null)
                {
                    // Parse mouse event
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                    // Check if this event is for the wallpaper (not desktop icons)
                    if (IsClickOnWallpaper(hookStruct.pt))
                    {
                        // Forward to WebView2
                        ForwardMouseEvent(wParam, hookStruct);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never throw from hook - would cause hook to be removed
                Console.WriteLine($"InputManager: Mouse hook error - {ex.Message}");
            }

            // ALWAYS call next hook
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Low-level keyboard hook callback
        /// </summary>
        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                // CRITICAL: Process quickly to avoid timeout
                if (nCode >= HC_ACTION && _isEnabled && _webView != null)
                {
                    // Only forward keyboard if wallpaper has focus
                    // (You could add focus detection here)

                    // Parse keyboard event
                    int vkCode = Marshal.ReadInt32(lParam);

                    // Forward to WebView2
                    ForwardKeyboardEvent(wParam, vkCode, lParam);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: Keyboard hook error - {ex.Message}");
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Checks if a click is on the wallpaper (not on desktop icons)
        /// </summary>
        private bool IsClickOnWallpaper(POINT pt)
        {
            try
            {
                // Get the window at this point
                IntPtr hwnd = WindowFromPoint(pt);

                if (hwnd == IntPtr.Zero)
                    return false;

                // Get the window class name
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classNameStr = className.ToString();

                // Desktop icons are in "SysListView32" window
                if (classNameStr.Contains("SysListView32"))
                {
                    // Click is on a desktop icon - don't forward
                    return false;
                }

                // Check if it's the SHELLDLL_DefView (desktop window)
                if (classNameStr.Contains("SHELLDLL_DefView"))
                {
                    // Click is on desktop, but might still be on an icon
                    // More sophisticated hit-testing could be done here
                    return false;
                }

                // Assume click is on wallpaper
                return true;
            }
            catch
            {
                // On error, assume wallpaper (safer to forward)
                return true;
            }
        }

        /// <summary>
        /// Forwards mouse event to WebView2
        /// </summary>
        private void ForwardMouseEvent(IntPtr wParam, MSLLHOOKSTRUCT hookStruct)
        {
            try
            {
                // Determine event type
                uint msg = (uint)wParam.ToInt32();
                POINT pt = hookStruct.pt;

                // Track performance
                _eventCount++;
                if ((DateTime.Now - _lastEventTime).TotalSeconds >= 5)
                {
                    Console.WriteLine($"InputManager: ~{_eventCount / 5} events/sec");
                    _eventCount = 0;
                    _lastEventTime = DateTime.Now;
                }

                // Convert to client coordinates (relative to WebView)
                // Note: For wallpaper, screen coordinates = client coordinates usually
                // But you might need to adjust based on window position

                // Send message to WebView's window handle
                if (_webViewHandle != IntPtr.Zero)
                {
                    // Create wParam for mouse messages (includes key states)
                    IntPtr mouseWParam = MakeMouseWParam(hookStruct);
                    IntPtr mouseLParam = MakeLParam(pt.X, pt.Y);

                    // Post the message (non-blocking)
                    PostMessage(_webViewHandle, msg, mouseWParam, mouseLParam);
                }
                else
                {
                    // Fallback: Use WebView2's CoreWebView2 API
                    // This is less direct but more reliable
                    LogMouseEvent(msg, pt);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: Failed to forward mouse event - {ex.Message}");
            }
        }

        /// <summary>
        /// Forwards keyboard event to WebView2
        /// </summary>
        private void ForwardKeyboardEvent(IntPtr wParam, int vkCode, IntPtr lParam)
        {
            try
            {
                uint msg = (uint)wParam.ToInt32();

                // Send keyboard message to WebView
                if (_webViewHandle != IntPtr.Zero)
                {
                    IntPtr keyWParam = new IntPtr(vkCode);
                    PostMessage(_webViewHandle, msg, keyWParam, lParam);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: Failed to forward keyboard event - {ex.Message}");
            }
        }

        /// <summary>
        /// Creates wParam for mouse messages (includes modifier keys)
        /// </summary>
        private IntPtr MakeMouseWParam(MSLLHOOKSTRUCT hookStruct)
        {
            int wParam = 0;

            // Check modifier keys
            if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
                wParam |= 0x0008; // MK_CONTROL

            if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
                wParam |= 0x0004; // MK_SHIFT

            // Check mouse buttons (for move events)
            if ((GetAsyncKeyState(0x01) & 0x8000) != 0) // Left button
                wParam |= 0x0001; // MK_LBUTTON

            if ((GetAsyncKeyState(0x02) & 0x8000) != 0) // Right button
                wParam |= 0x0002; // MK_RBUTTON

            if ((GetAsyncKeyState(0x04) & 0x8000) != 0) // Middle button
                wParam |= 0x0010; // MK_MBUTTON

            return new IntPtr(wParam);
        }

        /// <summary>
        /// Packs x and y coordinates into lParam
        /// </summary>
        private IntPtr MakeLParam(int x, int y)
        {
            return new IntPtr((y << 16) | (x & 0xFFFF));
        }

        /// <summary>
        /// Logs mouse event for debugging
        /// </summary>
        private void LogMouseEvent(uint msg, POINT pt)
        {
            string eventName = msg switch
            {
                WM_MOUSEMOVE => "MOVE",
                WM_LBUTTONDOWN => "LDOWN",
                WM_LBUTTONUP => "LUP",
                WM_RBUTTONDOWN => "RDOWN",
                WM_RBUTTONUP => "RUP",
                WM_MOUSEWHEEL => "WHEEL",
                _ => $"0x{msg:X4}"
            };

            // Only log non-move events to avoid spam
            if (msg != WM_MOUSEMOVE)
            {
                Console.WriteLine($"Mouse {eventName} at ({pt.X}, {pt.Y})");
            }
        }

        /// <summary>
        /// Gets diagnostic information
        /// </summary>
        public string GetDiagnostics()
        {
            return $"InputManager Status:\n" +
                   $"  Enabled: {_isEnabled}\n" +
                   $"  Mouse Hook: {(_mouseHookId != IntPtr.Zero ? $"0x{_mouseHookId:X8}" : "Not installed")}\n" +
                   $"  Keyboard Hook: {(_keyboardHookId != IntPtr.Zero ? $"0x{_keyboardHookId:X8}" : "Not installed")}\n" +
                   $"  WebView Handle: {(_webViewHandle != IntPtr.Zero ? $"0x{_webViewHandle:X8}" : "Not set")}";
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                UninstallHooks();
                _webView = null;
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        ~InputManager()
        {
            Dispose();
        }
    }
}
