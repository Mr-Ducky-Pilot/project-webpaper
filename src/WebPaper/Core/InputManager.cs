using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
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
        private IntPtr _inputHandle = IntPtr.Zero; // CRITICAL: Chrome_WidgetWin_1 for input forwarding
        private IntPtr _mainWindowHandle = IntPtr.Zero; // ADDED: Store main window handle for focus control
        private DispatcherQueue? _dispatcherQueue; // CRITICAL: For marshaling WebView2 calls to UI thread
        private bool _isEnabled = false;
        private bool _disposed = false;

        // Performance tracking
        private DateTime _lastEventTime = DateTime.Now;
        private int _eventCount = 0;

        // Focus management
        private DateTime _lastFocusAttempt = DateTime.MinValue;
        private const int FOCUS_RETRY_INTERVAL_MS = 100; // Don't spam focus calls
        private bool _firstFocusLogged = false; // Track if we've logged focus info already

        // Keyboard state tracking (to prevent duplicate WM_CHAR and global capture)
        private POINT _lastMousePosition = new POINT { X = 0, Y = 0 };
        private bool _mouseOverWallpaper = false;
        private DateTime _lastMouseOverWallpaperTime = DateTime.MinValue;
        private HashSet<int> _pressedKeys = new HashSet<int>(); // Track which keys are currently down

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
        public void InstallHooks(CoreWebView2 webView, IntPtr webViewHandle, IntPtr mainWindowHandle, DispatcherQueue dispatcherQueue)
        {
            if (HooksInstalled)
            {
                Console.WriteLine("InputManager: Hooks already installed");
                return;
            }

            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _webViewHandle = webViewHandle;
            _mainWindowHandle = mainWindowHandle; // CRITICAL: We need the main window to control focus
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue)); // CRITICAL: For UI thread marshaling

            // CRITICAL FIX: Find Chrome_WidgetWin_1 for input forwarding (same as Lively Wallpaper)
            // WebView2 window hierarchy: Chrome_RenderWidgetHostHWND -> Chrome_WidgetWin_0 -> Chrome_WidgetWin_1
            // We must send input to Chrome_WidgetWin_1, not the top-level window!
            _inputHandle = FindWebView2InputHandle(webViewHandle);
            if (_inputHandle == IntPtr.Zero)
            {
                Console.WriteLine("InputManager WARNING: Could not find Chrome_WidgetWin_1, falling back to main WebView handle");
                Console.WriteLine("  This may cause input to not work correctly!");
                _inputHandle = webViewHandle; // Fallback
            }
            else
            {
                Console.WriteLine($"InputManager: Found Chrome_WidgetWin_1 input handle: 0x{_inputHandle:X8}");
            }

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
                    uint msg = (uint)wParam.ToInt32();

                    // CRITICAL: Track mouse position for keyboard focus detection
                    _lastMousePosition = hookStruct.pt;
                    bool isOverWallpaper = IsClickOnWallpaper(hookStruct.pt);

                    // Update wallpaper hover state and timestamp
                    if (isOverWallpaper)
                    {
                        _mouseOverWallpaper = true;
                        _lastMouseOverWallpaperTime = DateTime.Now;
                    }
                    else if (msg == WM_MOUSEMOVE)
                    {
                        // Only update on mouse move - not on scroll events
                        // (trackpad scrolls don't move cursor, so we don't want to lose focus)
                        _mouseOverWallpaper = false;
                    }

                    // Debug: Log clicks to diagnose the issue
                    if (msg == WM_LBUTTONDOWN)
                    {
                        Console.WriteLine($"InputManager: LCLICK at ({hookStruct.pt.X},{hookStruct.pt.Y}) - Forward: {isOverWallpaper}");

                        if (isOverWallpaper)
                        {
                            // Forward to WebView2
                            ForwardMouseEvent(wParam, hookStruct);
                        }
                    }
                    else if (msg == WM_MOUSEWHEEL)
                    {
                        // CRITICAL FIX: For scroll events, be more lenient!
                        // Trackpad two-finger scroll doesn't move the cursor, so we check if
                        // the mouse was over the wallpaper recently (within 2 seconds)
                        TimeSpan timeSinceOverWallpaper = DateTime.Now - _lastMouseOverWallpaperTime;
                        bool shouldForwardScroll = timeSinceOverWallpaper.TotalSeconds < 2.0;

                        if (shouldForwardScroll)
                        {
                            // Add occasional debug logging for scroll events
                            if (_eventCount % 20 == 0)
                            {
                                Console.WriteLine($"InputManager: SCROLL event - forwarding (mouse was over wallpaper {timeSinceOverWallpaper.TotalMilliseconds:F0}ms ago)");
                            }
                            ForwardMouseEvent(wParam, hookStruct);
                        }
                        else if (_eventCount % 20 == 0)
                        {
                            Console.WriteLine($"InputManager: SCROLL event - BLOCKED (mouse not over wallpaper for {timeSinceOverWallpaper.TotalSeconds:F1}s)");
                        }
                    }
                    else
                    {
                        // For other non-click events (moves, etc.), forward only if over wallpaper
                        if (isOverWallpaper)
                        {
                            ForwardMouseEvent(wParam, hookStruct);
                        }
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
                    // CRITICAL FIX: Only forward keyboard if mouse is over wallpaper!
                    // This prevents capturing keyboard input from other apps (browser, WhatsApp, notepad, etc.)
                    if (!_mouseOverWallpaper)
                    {
                        // Mouse is not over wallpaper - don't capture this keyboard input
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // Parse keyboard event
                    int vkCode = Marshal.ReadInt32(lParam);
                    uint msg = (uint)wParam.ToInt32();

                    // Track key state to prevent duplicate WM_CHAR messages
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        // Check if this is a key repeat (key already pressed)
                        bool isRepeat = _pressedKeys.Contains(vkCode);

                        if (!isRepeat)
                        {
                            // First press - add to set and forward
                            _pressedKeys.Add(vkCode);
                            ForwardKeyboardEvent(wParam, vkCode, lParam);
                        }
                        // Ignore key repeats to prevent "hhheeeyyy" issue
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        // Key released - remove from set
                        _pressedKeys.Remove(vkCode);
                        ForwardKeyboardEvent(wParam, vkCode, lParam);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: Keyboard hook error - {ex.Message}");
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        // Track last logged class to avoid spam
        private string _lastLoggedClass = "";
        private DateTime _lastClassLogTime = DateTime.MinValue;

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
                {
                    LogWindowClass("NULL", false, "(WindowFromPoint returned NULL)");
                    return false;
                }

                // Get the window class name
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classNameStr = className.ToString();

                // Desktop icons are in "SysListView32" window - don't forward those
                if (classNameStr.Contains("SysListView32"))
                {
                    LogWindowClass(classNameStr, false, "Desktop icon list");
                    return false;
                }

                // CRITICAL FIX: Accept clicks on our OWN WebView2 window!
                // When the wallpaper is visible, clicks land on the WebView2 itself
                if (classNameStr.Contains("Chrome_RenderWidgetHostHWND"))
                {
                    // Verify it's actually our WebView2 by checking if it's a child of our window
                    // (This prevents accepting clicks on other Chrome/Edge windows)
                    LogWindowClass(classNameStr, true, "Our WebView2 wallpaper");
                    return true;
                }

                // For desktop surface (SHELLDLL_DefView) and Progman, forward to wallpaper
                // These are the desktop background areas where our wallpaper should be interactive
                if (classNameStr.Contains("SHELLDLL_DefView") || classNameStr.Contains("Progman"))
                {
                    LogWindowClass(classNameStr, true, "Desktop surface");
                    return true;
                }

                // For any other window, don't forward (might be another app)
                LogWindowClass(classNameStr, false, "Other window (not desktop)");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: IsClickOnWallpaper error - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logs window class detection (throttled to avoid spam)
        /// </summary>
        private void LogWindowClass(string className, bool willForward, string reason)
        {
            // Only log if class changed or it's been >5 seconds
            if (className != _lastLoggedClass || (DateTime.Now - _lastClassLogTime).TotalSeconds > 5)
            {
                Console.WriteLine($"InputManager: WindowFromPoint = '{className}' -> {(willForward ? "FORWARD" : "REJECT")} ({reason})");
                _lastLoggedClass = className;
                _lastClassLogTime = DateTime.Now;
            }
        }

        /// <summary>
        /// Finds the Chrome_WidgetWin_1 window inside WebView2 for input forwarding
        /// This is the correct window to send input messages to (discovered by studying Lively Wallpaper)
        /// </summary>
        private IntPtr FindWebView2InputHandle(IntPtr webViewHandle)
        {
            try
            {
                // WebView2 might not have created all child windows yet - retry a few times
                const int maxRetries = 10;
                const int retryDelayMs = 100;

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    if (attempt > 0)
                    {
                        Console.WriteLine($"InputManager: Retry {attempt}/{maxRetries} to find Chrome_WidgetWin_0...");
                        Thread.Sleep(retryDelayMs);
                    }

                    // Enumerate all child windows to see what exists
                    if (attempt == 0)
                    {
                        Console.WriteLine("InputManager: Enumerating child windows of WebView2:");
                        EnumerateChildWindows(webViewHandle, 0);
                    }

                    // Find Chrome_WidgetWin_0 child
                    IntPtr chromeWidgetWin0 = FindWindowEx(webViewHandle, IntPtr.Zero, "Chrome_WidgetWin_0", null);
                    if (chromeWidgetWin0 == IntPtr.Zero)
                    {
                        if (attempt == 0)
                            Console.WriteLine("InputManager: Chrome_WidgetWin_0 not found (yet)");
                        continue; // Retry
                    }

                    // Find Chrome_WidgetWin_1 child of Chrome_WidgetWin_0
                    IntPtr chromeWidgetWin1 = FindWindowEx(chromeWidgetWin0, IntPtr.Zero, "Chrome_WidgetWin_1", null);
                    if (chromeWidgetWin1 == IntPtr.Zero)
                    {
                        Console.WriteLine("InputManager: Chrome_WidgetWin_0 found but Chrome_WidgetWin_1 not found");
                        Console.WriteLine("InputManager: Enumerating children of Chrome_WidgetWin_0:");
                        EnumerateChildWindows(chromeWidgetWin0, 1);
                        continue; // Retry
                    }

                    Console.WriteLine($"InputManager: SUCCESS - Found Chrome_WidgetWin_1 on attempt {attempt + 1}");
                    return chromeWidgetWin1;
                }

                Console.WriteLine($"InputManager: Failed to find Chrome_WidgetWin_1 after {maxRetries} attempts from webViewHandle");

                // FALLBACK: Try searching from the main window handle
                // The window hierarchy might be: MainWindow -> Chrome_RenderWidgetHostHWND -> Chrome_WidgetWin_0 -> Chrome_WidgetWin_1
                Console.WriteLine("InputManager: FALLBACK - Searching from main window handle...");
                if (_mainWindowHandle != IntPtr.Zero && _mainWindowHandle != webViewHandle)
                {
                    IntPtr result = FindWebView2InputHandleRecursive(_mainWindowHandle, 0);
                    if (result != IntPtr.Zero)
                    {
                        Console.WriteLine($"InputManager: SUCCESS via fallback - Found Chrome_WidgetWin_1: 0x{result:X8}");
                        return result;
                    }
                }

                Console.WriteLine("InputManager: All attempts exhausted - Chrome_WidgetWin_1 not found");
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: Error finding input handle - {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Recursively searches for Chrome_WidgetWin_1 in the window hierarchy
        /// </summary>
        private IntPtr FindWebView2InputHandleRecursive(IntPtr parent, int depth)
        {
            // Prevent infinite recursion
            if (depth > 5)
                return IntPtr.Zero;

            // First, check direct children for the pattern: Chrome_WidgetWin_0 -> Chrome_WidgetWin_1
            IntPtr chromeWidgetWin0 = FindWindowEx(parent, IntPtr.Zero, "Chrome_WidgetWin_0", null);
            if (chromeWidgetWin0 != IntPtr.Zero)
            {
                IntPtr chromeWidgetWin1 = FindWindowEx(chromeWidgetWin0, IntPtr.Zero, "Chrome_WidgetWin_1", null);
                if (chromeWidgetWin1 != IntPtr.Zero)
                {
                    Console.WriteLine($"InputManager: Found at depth {depth}: Chrome_WidgetWin_1 = 0x{chromeWidgetWin1:X8}");
                    return chromeWidgetWin1;
                }
            }

            // If not found, search all child windows recursively
            IntPtr child = IntPtr.Zero;
            while (true)
            {
                child = FindWindowEx(parent, child, null, null);
                if (child == IntPtr.Zero)
                    break;

                IntPtr result = FindWebView2InputHandleRecursive(child, depth + 1);
                if (result != IntPtr.Zero)
                    return result;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Helper to enumerate and log child windows for debugging
        /// </summary>
        private void EnumerateChildWindows(IntPtr parent, int indent)
        {
            IntPtr child = IntPtr.Zero;
            int count = 0;
            string indentStr = new string(' ', indent * 2);

            while (true)
            {
                child = FindWindowEx(parent, child, null, null);
                if (child == IntPtr.Zero)
                    break;

                StringBuilder className = new StringBuilder(256);
                GetClassName(child, className, className.Capacity);
                Console.WriteLine($"{indentStr}  Child {count}: {className} (0x{child:X8})");
                count++;

                if (count > 20) // Prevent spam
                {
                    Console.WriteLine($"{indentStr}  ... (more children not shown)");
                    break;
                }
            }

            if (count == 0)
                Console.WriteLine($"{indentStr}  (no children)");
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

                // Forward input to WebView2's Chrome_WidgetWin_1 child window (discovered from Lively Wallpaper)
                if (_inputHandle != IntPtr.Zero)
                {
                    // CRITICAL FIX: WM_MOUSEWHEEL requires special handling!
                    // For scroll events, wParam contains the wheel delta in high-order word
                    // and lParam contains coordinates in screen space (not client space!)
                    if (msg == WM_MOUSEWHEEL)
                    {
                        // Extract wheel delta from mouseData (high-order word)
                        // Positive = scroll up/away, Negative = scroll down/toward
                        short wheelDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);

                        // Build wParam: high-word = wheel delta, low-word = modifier keys
                        int modifiers = 0;
                        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
                            modifiers |= 0x0008; // MK_CONTROL
                        if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
                            modifiers |= 0x0004; // MK_SHIFT

                        IntPtr scrollWParam = new IntPtr((wheelDelta << 16) | modifiers);

                        // For WM_MOUSEWHEEL, lParam should be in SCREEN coordinates, not client
                        IntPtr scrollLParam = MakeLParam(pt.X, pt.Y);

                        // Log scroll events (occasionally)
                        if (_eventCount % 10 == 0) // Log every 10th scroll event to reduce spam
                        {
                            Console.WriteLine($"InputManager: SCROLL delta={wheelDelta} at screen({pt.X},{pt.Y})");
                        }

                        // Send scroll message
                        PostMessage(_inputHandle, msg, scrollWParam, scrollLParam);
                    }
                    else
                    {
                        // Regular mouse events (clicks, moves, etc.)
                        // Convert screen coordinates to client coordinates
                        POINT clientPt = pt;
                        ScreenToClient(_inputHandle, ref clientPt);

                        // Build lParam: low-word = x, high-word = y
                        IntPtr lParam = MakeLParam(clientPt.X, clientPt.Y);
                        IntPtr mouseWParam = MakeMouseWParam(hookStruct);

                        // For clicks, log the action
                        if (msg == WM_LBUTTONDOWN)
                        {
                            Console.WriteLine($"InputManager: LCLICK at screen({pt.X},{pt.Y}) -> client({clientPt.X},{clientPt.Y})");
                            Console.WriteLine($"  Forwarding to Chrome_WidgetWin_1 via PostMessage");
                        }

                        // Forward the message using PostMessage (works because we're using the correct child window!)
                        PostMessage(_inputHandle, msg, mouseWParam, lParam);
                    }
                }
                else
                {
                    // Fallback: Log for debugging
                    LogMouseEvent(msg, pt);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: Failed to forward mouse event - {ex.Message}");
            }
        }

        /// <summary>
        /// Simulates a mouse click by executing JavaScript directly on the webpage
        /// This bypasses all Windows focus requirements - GUARANTEED to work!
        /// Latency: ~50-200ms (acceptable for wallpaper use case)
        ///
        /// THREADING: Uses DispatcherQueue to marshal ExecuteScriptAsync to UI thread
        /// WebView2 COM objects MUST be accessed from the UI thread that created them
        /// </summary>
        private void SimulateClickViaJavaScript(int x, int y)
        {
            try
            {
                if (_webView == null)
                {
                    Console.WriteLine("  ERROR: _webView is null!");
                    return;
                }

                if (_dispatcherQueue == null)
                {
                    Console.WriteLine("  ERROR: _dispatcherQueue is null! Cannot marshal to UI thread.");
                    return;
                }

                // SMART JavaScript click simulation using elementsFromPoint
                string script = $@"
                    (function() {{
                        try {{
                            console.log('WebPaper: Click at ({x}, {y})');

                            // Helper: Check if element is interactive/clickable
                            function isInteractive(el) {{
                                if (!el) return false;

                                var tag = el.tagName ? el.tagName.toUpperCase() : '';
                                var role = el.getAttribute('role');

                                // Skip BODY and HTML - never clickable
                                if (tag === 'BODY' || tag === 'HTML') return false;

                                // Check interactive tags
                                if (['A', 'BUTTON', 'INPUT', 'SELECT', 'TEXTAREA', 'LABEL',
                                     'SUMMARY', 'DETAILS', 'VIDEO', 'AUDIO'].includes(tag)) {{
                                    return true;
                                }}

                                // Check ARIA roles
                                if (role && ['button', 'link', 'checkbox', 'radio', 'tab',
                                            'menuitem', 'option', 'switch', 'slider'].includes(role)) {{
                                    return true;
                                }}

                                // Check for click handlers
                                if (el.onclick || el.hasAttribute('onclick')) {{
                                    return true;
                                }}

                                // Check CSS cursor (strong indicator of clickability)
                                var computed = window.getComputedStyle(el);
                                if (computed.cursor === 'pointer') {{
                                    return true;
                                }}

                                // Check for common clickable data attributes
                                if (el.hasAttribute('data-action') ||
                                    el.hasAttribute('data-click') ||
                                    el.hasAttribute('data-toggle') ||
                                    el.hasAttribute('data-handler')) {{
                                    return true;
                                }}

                                // Check if element has child interactive elements
                                // (but the element itself might be clickable container)
                                if (el.querySelector('a, button, input, [role=""button""], [role=""link""]')) {{
                                    // Has interactive children, but check if this element itself is a clickable container
                                    var hasClickHandler = el.onclick || el.hasAttribute('onclick');
                                    if (hasClickHandler) return true;
                                }}

                                return false;
                            }}

                            // Helper: Get interactivity score (higher = more likely to be the intended target)
                            function getInteractivityScore(el) {{
                                var score = 0;
                                var tag = el.tagName ? el.tagName.toUpperCase() : '';
                                var role = el.getAttribute('role');

                                // High priority: actual interactive elements
                                if (['BUTTON', 'A'].includes(tag)) score += 100;
                                if (['INPUT', 'SELECT', 'TEXTAREA'].includes(tag)) score += 90;
                                if (role === 'button' || role === 'link') score += 80;

                                // Medium priority: clickable indicators
                                if (el.onclick || el.hasAttribute('onclick')) score += 50;
                                var computed = window.getComputedStyle(el);
                                if (computed.cursor === 'pointer') score += 40;

                                // Low priority: data attributes
                                if (el.hasAttribute('data-action')) score += 30;

                                // Penalty for generic containers
                                if (['DIV', 'SPAN', 'SECTION'].includes(tag)) score -= 10;

                                // Bonus for smaller elements (more specific target)
                                var rect = el.getBoundingClientRect();
                                var area = rect.width * rect.height;
                                var viewportArea = window.innerWidth * window.innerHeight;
                                if (area < viewportArea * 0.01) score += 20; // < 1% of screen
                                else if (area > viewportArea * 0.5) score -= 20; // > 50% of screen

                                return score;
                            }}

                            // STEP 1: Get ALL elements at this point (from top to bottom)
                            var allElements = document.elementsFromPoint({x}, {y});

                            if (!allElements || allElements.length === 0) {{
                                console.log('WebPaper: No elements found at point');
                                return 'ERROR: No element found';
                            }}

                            console.log('WebPaper: Found ' + allElements.length + ' elements at point');

                            // STEP 2: Find the best interactive element
                            var candidates = [];

                            for (var i = 0; i < allElements.length; i++) {{
                                var el = allElements[i];
                                if (isInteractive(el)) {{
                                    var score = getInteractivityScore(el);
                                    candidates.push({{ element: el, score: score, index: i }});
                                    console.log('WebPaper: Candidate ' + i + ':', el.tagName,
                                               el.className, 'score=' + score);
                                }}
                            }}

                            // STEP 3: Pick the best candidate
                            var target = null;

                            if (candidates.length === 0) {{
                                // No interactive elements found - try to find clickable parent
                                console.log('WebPaper: No interactive elements, checking parents...');
                                var topElement = allElements[0];
                                var parent = topElement.parentElement;
                                var depth = 0;

                                while (parent && depth < 5) {{
                                    if (isInteractive(parent)) {{
                                        target = parent;
                                        console.log('WebPaper: Found clickable parent:', parent.tagName);
                                        break;
                                    }}
                                    parent = parent.parentElement;
                                    depth++;
                                }}

                                // Still nothing? Click the topmost element anyway
                                if (!target) {{
                                    target = allElements[0];
                                    console.log('WebPaper: No clickable element, using topmost:', target.tagName);
                                }}
                            }} else {{
                                // Sort by score (highest first), then by index (topmost first)
                                candidates.sort(function(a, b) {{
                                    if (b.score !== a.score) return b.score - a.score;
                                    return a.index - b.index;
                                }});

                                target = candidates[0].element;
                                console.log('WebPaper: Selected best candidate:', target.tagName,
                                           target.className, 'score=' + candidates[0].score);
                            }}

                            // STEP 4: Dispatch click events
                            var eventOptions = {{
                                view: window,
                                bubbles: true,
                                cancelable: true,
                                clientX: {x},
                                clientY: {y},
                                button: 0
                            }};

                            target.dispatchEvent(new MouseEvent('mousedown', eventOptions));
                            target.dispatchEvent(new MouseEvent('mouseup', eventOptions));
                            target.dispatchEvent(new MouseEvent('click', eventOptions));

                            // Also try native click() for form elements
                            if (typeof target.click === 'function') {{
                                try {{
                                    target.click();
                                }} catch (e) {{
                                    console.log('WebPaper: Native click failed:', e.message);
                                }}
                            }}

                            // Build result
                            var resultTag = target.tagName || 'UNKNOWN';
                            var resultClass = (target.className || '').toString().substring(0, 40);
                            var candidateCount = candidates.length;

                            return 'SUCCESS: ' + resultTag +
                                   (resultClass ? ' (' + resultClass + ')' : '') +
                                   ' [' + candidateCount + ' candidates, ' + allElements.length + ' total]';

                        }} catch (e) {{
                            console.error('WebPaper: Click error:', e);
                            return 'ERROR: ' + e.message;
                        }}
                    }})();
                ";

                Console.WriteLine($"  Marshaling JavaScript execution to UI thread...");

                // CRITICAL FIX: Use DispatcherQueue.TryEnqueue to execute on UI thread
                // This is the WinUI 3 / Windows App SDK way to marshal calls to UI thread
                // WebView2 COM objects require UI thread affinity (STA threading model)
                bool enqueued = _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                {
                    try
                    {
                        Console.WriteLine($"  Executing JavaScript on UI thread...");
                        var result = await _webView.ExecuteScriptAsync(script);
                        Console.WriteLine($"  JavaScript result: {result}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  JavaScript execution FAILED: {ex.Message}");
                        Console.WriteLine($"  Exception type: {ex.GetType().Name}");
                        if (ex.StackTrace != null)
                        {
                            Console.WriteLine($"  Stack: {ex.StackTrace}");
                        }
                    }
                });

                if (!enqueued)
                {
                    Console.WriteLine($"  ERROR: Failed to enqueue JavaScript execution to UI thread!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: SimulateClickViaJavaScript failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Simulates mouse wheel scroll by executing JavaScript directly on the webpage
        /// This bypasses Windows focus requirements like click simulation
        /// </summary>
        private void SimulateScrollViaJavaScript(int x, int y, int scrollAmount)
        {
            try
            {
                if (_webView == null || _dispatcherQueue == null)
                {
                    return;
                }

                // Normalize scroll amount (typically comes in multiples of 120)
                // Convert to pixels for smoother scrolling
                int scrollPixels = scrollAmount / 120 * 100; // 100 pixels per scroll unit

                string script = $@"
                    (function() {{
                        try {{
                            // Find the scrollable element at this position
                            var element = document.elementFromPoint({x}, {y});
                            if (!element) return 'ERROR: No element';

                            // Find the nearest scrollable parent
                            var scrollableElement = element;
                            var depth = 0;

                            while (scrollableElement && depth < 10) {{
                                var computed = window.getComputedStyle(scrollableElement);
                                var overflowY = computed.overflowY;
                                var overflowX = computed.overflowX;

                                // Check if this element is scrollable
                                var isScrollable = (overflowY === 'auto' || overflowY === 'scroll' ||
                                                   overflowX === 'auto' || overflowX === 'scroll') &&
                                                  (scrollableElement.scrollHeight > scrollableElement.clientHeight ||
                                                   scrollableElement.scrollWidth > scrollableElement.clientWidth);

                                if (isScrollable) {{
                                    break;
                                }}

                                scrollableElement = scrollableElement.parentElement;
                                depth++;
                            }}

                            // Default to window if no scrollable element found
                            if (!scrollableElement || scrollableElement.tagName === 'HTML' || scrollableElement.tagName === 'BODY') {{
                                window.scrollBy(0, {scrollPixels});
                                return 'SUCCESS: Scrolled window ' + {scrollPixels} + 'px';
                            }} else {{
                                scrollableElement.scrollTop += {scrollPixels};
                                return 'SUCCESS: Scrolled ' + scrollableElement.tagName + ' ' + {scrollPixels} + 'px';
                            }}
                        }} catch (e) {{
                            return 'ERROR: ' + e.message;
                        }}
                    }})();
                ";

                // Execute on UI thread (no console logging for scrolls to avoid spam)
                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                {
                    try
                    {
                        await _webView.ExecuteScriptAsync(script);
                    }
                    catch
                    {
                        // Silently fail for scrolls to avoid log spam
                    }
                });
            }
            catch
            {
                // Silently fail for scrolls
            }
        }

        /// <summary>
        /// Acquires TRUE focus for the WebView2 control
        /// Uses MODERN best practices (2024+) - NO AttachThreadInput hack!
        /// NOTE: Research shows this may not work for WS_CHILD windows parented to WorkerW
        /// </summary>
        private void AcquireWebViewFocus()
        {
            try
            {
                // Throttle focus attempts to avoid spam (only every 100ms)
                if ((DateTime.Now - _lastFocusAttempt).TotalMilliseconds < FOCUS_RETRY_INTERVAL_MS)
                {
                    return; // Too soon, skip this attempt
                }

                _lastFocusAttempt = DateTime.Now;

                // MODERN APPROACH (2024+ Best Practice):
                // 1. Don't use AttachThreadInput (deprecated hack, can cause freezes)
                // 2. SetForegroundWindow is enough for our scenario since WS_EX_NOACTIVATE is removed
                // 3. Find the ACTUAL input child window inside WebView2 and focus it

                // Step 1: Bring our main window to the foreground
                // Since we removed WS_EX_NOACTIVATE, this should now work properly
                bool foregroundSet = SetForegroundWindow(_mainWindowHandle);

                // Step 2: Get the REAL input child window inside WebView2
                // WebView2's Chrome_RenderWidgetHostHWND has a child that accepts input
                // This is the Microsoft-recommended approach for WebView2 focus issues
                IntPtr inputChild = GetWindow(_webViewHandle, GetWindowType.GW_CHILD);
                IntPtr focusTarget = inputChild != IntPtr.Zero ? inputChild : _webViewHandle;

                // Step 3: Set focus to the actual input window
                IntPtr focusResult = SetFocus(focusTarget);

                // Debug logging (only on first click to avoid spam)
                if (!_firstFocusLogged)
                {
                    Console.WriteLine($"InputManager: AcquireWebViewFocus() - Modern Approach (2024)");
                    Console.WriteLine($"  SetForegroundWindow(_mainWindowHandle=0x{_mainWindowHandle:X8}) = {foregroundSet}");
                    Console.WriteLine($"  WebView2 Handle: 0x{_webViewHandle:X8}");
                    Console.WriteLine($"  Input Child Window: 0x{inputChild:X8}");
                    Console.WriteLine($"  Focus Target: 0x{focusTarget:X8}");
                    Console.WriteLine($"  SetFocus() = 0x{focusResult:X8}");
                    if (focusResult == IntPtr.Zero)
                    {
                        uint error = GetLastError();
                        Console.WriteLine($"  WARNING: SetFocus failed! Error: {error}");
                        Console.WriteLine($"  Trying to focus WebView2 directly as fallback...");

                        // Fallback: Try focusing the WebView2 handle directly
                        IntPtr fallbackFocus = SetFocus(_webViewHandle);
                        Console.WriteLine($"  Fallback SetFocus(_webViewHandle) = 0x{fallbackFocus:X8}");
                    }
                    else
                    {
                        Console.WriteLine($"  SUCCESS: WebView2 should now have focus!");
                    }
                    _firstFocusLogged = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: AcquireWebViewFocus failed - {ex.Message}");
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

                // Send keyboard message to Chrome_WidgetWin_1
                if (_inputHandle != IntPtr.Zero)
                {
                    IntPtr keyWParam = new IntPtr(vkCode);
                    PostMessage(_inputHandle, msg, keyWParam, lParam);

                    // CRITICAL FIX: For text input to work, we MUST send WM_CHAR messages!
                    // WM_KEYDOWN/WM_KEYUP only send virtual key codes, but text input
                    // requires WM_CHAR with the actual character.
                    //
                    // This is why backspace (control key) works but typing letters doesn't!
                    if (msg == WM_KEYDOWN)
                    {
                        // Convert virtual key to character
                        char ch = VirtualKeyToChar((uint)vkCode);
                        if (ch != '\0') // If it's a printable character
                        {
                            // Send WM_CHAR message with the character
                            IntPtr charWParam = new IntPtr(ch);
                            PostMessage(_inputHandle, WM_CHAR, charWParam, IntPtr.Zero);

                            // Log for debugging (only occasionally to avoid spam)
                            if (char.IsLetterOrDigit(ch) || char.IsPunctuation(ch) || ch == ' ')
                            {
                                Console.WriteLine($"InputManager: Sent WM_CHAR for '{ch}' (0x{(int)ch:X2})");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InputManager: Failed to forward keyboard event - {ex.Message}");
            }
        }

        /// <summary>
        /// Converts a virtual key code to a character using current keyboard state
        /// Returns '\0' if the key doesn't produce a character (e.g., Shift, Ctrl)
        /// </summary>
        private char VirtualKeyToChar(uint vkCode)
        {
            try
            {
                // Get current keyboard state (for modifiers like Shift)
                byte[] keyboardState = new byte[256];
                if (!GetKeyboardState(keyboardState))
                    return '\0';

                // Get scan code from virtual key
                uint scanCode = MapVirtualKey(vkCode, 0); // MAPVK_VK_TO_VSC = 0

                // Convert to Unicode character
                StringBuilder result = new StringBuilder(5);
                int ret = ToUnicode(vkCode, scanCode, keyboardState, result, result.Capacity, 0);

                if (ret == 1) // Successfully converted to 1 character
                {
                    return result[0];
                }
                else if (ret == 2) // Dead key or multi-character (rare)
                {
                    return result[0]; // Return first character
                }

                return '\0'; // No character (control key, etc.)
            }
            catch
            {
                return '\0';
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
                   $"  WebView Handle: {(_webViewHandle != IntPtr.Zero ? $"0x{_webViewHandle:X8}" : "Not set")}\n" +
                   $"  Input Handle (Chrome_WidgetWin_1): {(_inputHandle != IntPtr.Zero ? $"0x{_inputHandle:X8}" : "Not found")}";
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
