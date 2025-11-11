# 🔧 WebPaper Input Fix - Implementation Summary

**Date:** November 11, 2025
**Issue:** WebView2 not responding to mouse clicks, keyboard input, or scroll events
**Root Cause:** `WS_EX_NOACTIVATE` flag preventing window from receiving focus

---

## 🎯 Root Cause Analysis

### The Problem
The application was successfully forwarding input events to WebView2, but the webpage wasn't responding because:

1. **`WS_EX_NOACTIVATE` flag was set** (MainWindow.xaml.cs:275)
   - This flag explicitly prevents a window from being activated
   - Windows cannot give focus to windows that cannot be activated
   - Result: `SetFocus()` was failing silently

2. **WebView2 (Chromium) requires TRUE focus to process input**
   - PostMessage() was succeeding (messages were queued)
   - But Chromium's renderer ignores input from unfocused windows (security feature)
   - Result: Events queued but ignored

3. **Window hierarchy prevented focus**
   ```
   Progman (desktop)
    └── WorkerW (behind icons)
         └── MainWindow (WS_CHILD + WS_EX_NOACTIVATE)  ← PROBLEM HERE
              └── WebView2 (Chrome_RenderWidgetHostHWND)
   ```

### Evidence from Logs
```
InputManager: Forwarding LCLICK at screen(1215,599) -> client(1215,599) to 0x001E0CF0
  ScreenToClient conversion: SUCCESS
```
Everything looked like it was working, but WebView2 wasn't responding!

---

## ✅ Changes Implemented

### 1. **NativeMethods.cs** - Added `SetForegroundWindow` API

**File:** `src/WebPaper/Native/NativeMethods.cs`

**Added:**
```csharp
/// <summary>
/// Brings the thread that created the specified window into the foreground and activates the window.
/// </summary>
[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool SetForegroundWindow(IntPtr hWnd);
```

**Why:** Needed to bring our window to foreground before it can receive focus.

---

### 2. **MainWindow.xaml.cs** - REMOVED `WS_EX_NOACTIVATE` Flag

**File:** `src/WebPaper/MainWindow.xaml.cs:273-280`

**Before:**
```csharp
// Set extended window style to prevent activation
int currentExStyle = Native.NativeMethods.GetWindowLong(_windowHandle, Native.NativeMethods.GWL_EXSTYLE);
int newExStyle = currentExStyle | (int)Native.NativeMethods.WS_EX_NOACTIVATE;
Native.NativeMethods.SetWindowLong(_windowHandle, Native.NativeMethods.GWL_EXSTYLE, newExStyle);
```

**After:**
```csharp
// CRITICAL FIX: Do NOT set WS_EX_NOACTIVATE!
// That flag prevents the window from ever receiving focus, which breaks WebView2 input.
// We need the window to be able to receive focus for input to work.
int currentExStyle = Native.NativeMethods.GetWindowLong(_windowHandle, Native.NativeMethods.GWL_EXSTYLE);
// Remove WS_EX_NOACTIVATE if it exists
int newExStyle = currentExStyle & ~(int)Native.NativeMethods.WS_EX_NOACTIVATE;
Native.NativeMethods.SetWindowLong(_windowHandle, Native.NativeMethods.GWL_EXSTYLE, newExStyle);
```

**Why:** This was THE critical fix. WS_EX_NOACTIVATE made it impossible for the window to receive focus.

**Changed in MainWindow.xaml.cs:401:**
```csharp
// BEFORE:
_inputManager.InstallHooks(webView.CoreWebView2, webViewHandle);

// AFTER:
_inputManager.InstallHooks(webView.CoreWebView2, webViewHandle, _windowHandle);
```

**Why:** InputManager needs main window handle to control focus.

---

### 3. **InputManager.cs** - Enhanced Focus Acquisition

**File:** `src/WebPaper/Core/InputManager.cs`

#### Change 3a: Added Main Window Handle Storage

**Lines 24, 33-34:**
```csharp
private IntPtr _mainWindowHandle = IntPtr.Zero; // ADDED: Store main window handle for focus control

// Focus management
private DateTime _lastFocusAttempt = DateTime.MinValue;
private const int FOCUS_RETRY_INTERVAL_MS = 100; // Don't spam focus calls
```

#### Change 3b: Updated `InstallHooks` Signature

**Line 60:**
```csharp
// BEFORE:
public void InstallHooks(CoreWebView2 webView, IntPtr webViewHandle)

// AFTER:
public void InstallHooks(CoreWebView2 webView, IntPtr webViewHandle, IntPtr mainWindowHandle)
```

**Line 70:**
```csharp
_mainWindowHandle = mainWindowHandle; // CRITICAL: We need the main window to control focus
```

#### Change 3c: Replaced SetFocus with `AcquireWebViewFocus()`

**Lines 315-322:**
```csharp
// CRITICAL FIX: WebView2 requires TRUE FOCUS to process input!
// On mouse down/up/wheel events, we must acquire real focus
if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN ||
    msg == WM_LBUTTONUP || msg == WM_RBUTTONUP || msg == WM_MBUTTONUP ||
    msg == WM_MOUSEWHEEL)
{
    AcquireWebViewFocus();
}
```

#### Change 3d: Added `AcquireWebViewFocus()` Method (MODERN 2024 APPROACH)

**Lines 361-408:**
```csharp
/// <summary>
/// Acquires TRUE focus for the WebView2 control
/// Uses MODERN best practices (2024+) - NO AttachThreadInput hack!
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
        // 3. Then SetFocus on the WebView2 child window

        // Step 1: Bring our main window to the foreground
        // Since we removed WS_EX_NOACTIVATE, this should now work properly
        bool foregroundSet = SetForegroundWindow(_mainWindowHandle);

        // Step 2: Set focus to the WebView2 child window
        // This gives it keyboard focus after the parent window is foreground
        IntPtr focusResult = SetFocus(_webViewHandle);

        // Debug logging (only on first click to avoid spam)
        static bool firstFocus = true;
        if (firstFocus)
        {
            Console.WriteLine($"InputManager: AcquireWebViewFocus() - Modern Approach (2024)");
            Console.WriteLine($"  SetForegroundWindow(_mainWindowHandle=0x{_mainWindowHandle:X8}) = {foregroundSet}");
            Console.WriteLine($"  SetFocus(_webViewHandle=0x{_webViewHandle:X8}) = 0x{focusResult:X8}");
            if (focusResult == IntPtr.Zero)
            {
                Console.WriteLine($"  WARNING: SetFocus failed! Error: {GetLastError()}");
            }
            firstFocus = false;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"InputManager: AcquireWebViewFocus failed - {ex.Message}");
    }
}
```

**Key Features:**
- ✅ Uses 2024 best practices (no deprecated `AttachThreadInput` hack)
- ✅ Throttles focus calls to once per 100ms (performance)
- ✅ Calls `SetForegroundWindow` on main window FIRST
- ✅ Then calls `SetFocus` on WebView2 child
- ✅ Debug logging on first click only (no spam)

---

## 📚 Research Findings (Web Search 2024)

### Microsoft Official Recommendations:
1. **Use `MoveFocus` API for WebView2** (CoreWebView2Controller.MoveFocus)
2. **Avoid `AttachThreadInput`** - Considered a hack as of 2024, can cause app freezes
3. **WebView2 focus issues fixed in version 134.0.3124.68**

### Windows Focus Best Practices (2024):
1. **`AttachThreadInput` no longer works reliably as of Nov 2024**
2. **Recommended:** Use `AllowSetForegroundWindow` + `SetForegroundWindow` together
3. **Avoid focus-stealing hacks** - Raymond Chen (Microsoft) strongly advises against them

### Our Approach:
Since we removed `WS_EX_NOACTIVATE`, a simple `SetForegroundWindow` + `SetFocus` should work without needing the deprecated `AttachThreadInput` hack.

---

## 🧪 Testing Instructions

### Build the Project:
```bash
cd /home/user/project-webpaper
dotnet build src/WebPaper/WebPaper.csproj -c Release
```

### Run the Application:
```bash
cd src/WebPaper/bin/Release/net8.0-windows10.0.19041.0/win-x64
./WebPaper.exe
```

### What to Test:

1. **Mouse Clicks** ✅
   - Click on links, buttons on the webpage
   - Expected: Links should navigate, buttons should activate

2. **Scrolling** ✅
   - Scroll with mouse wheel
   - Expected: Page should scroll up/down

3. **Keyboard Input** ✅
   - Click in a search box
   - Type some text
   - Expected: Text appears as you type

4. **Desktop Icons** ⚠️
   - Click on a desktop icon
   - Expected: Icon selects (NOT webpage action)
   - **NOTE:** This may still not work perfectly - see "Known Limitations" below

5. **Focus Behavior** 🔍
   - Watch the console output on first click
   - Should see: `InputManager: AcquireWebViewFocus() - Modern Approach (2024)`
   - Check if `SetForegroundWindow` and `SetFocus` both succeed

---

## 🚨 Known Limitations & Next Steps

### 1. Desktop Icon Click-Through (Still May Not Work)
**Problem:** Our window covers the desktop, so `WindowFromPoint()` returns our window even over icon areas.

**Current Mitigation:** We detect `SysListView32` (desktop icon list) and reject those clicks, but this may not be perfect.

**Possible Future Fix:**
- Implement per-pixel hit testing
- Use `WS_EX_LAYERED` with transparent regions
- Or use `WS_EX_TRANSPARENT` with dynamic toggling

### 2. Window Comes to Foreground
**Side Effect:** When you click on the wallpaper, the window briefly comes to foreground (visible for 1 frame).

**Why:** We call `SetForegroundWindow()` to give it focus, which makes it visible momentarily.

**Possible Fix:**
- Use a transparent overlay window approach (two-window architecture)
- Or accept this as a minor visual artifact

### 3. Alternative: JavaScript Simulation (Fallback)
If direct input forwarding still doesn't work perfectly, we can fallback to JavaScript simulation for clicks:

```csharp
await webView.CoreWebView2.ExecuteScriptAsync($@"
    var element = document.elementFromPoint({x}, {y});
    if (element) {{
        element.click();
    }}
");
```

**Pros:** Bypasses Windows focus issues entirely
**Cons:** High latency (50-200ms), doesn't support hover/scroll/drag well

---

## 📊 Expected Outcomes

### ✅ SHOULD NOW WORK:
- Mouse clicks on webpage content
- Keyboard input in text fields
- Mouse wheel scrolling
- Right-click context menus
- Hover effects

### ⚠️ MAY STILL HAVE ISSUES:
- Desktop icon clicks (may be blocked by our window)
- Complex drag operations
- Some keyboard shortcuts (Windows intercepts Win+X, Alt+Tab, etc.)

### ❌ BY DESIGN (LIMITATIONS):
- DRM content (Netflix, Disney+) - browser restrictions
- Some sites with X-Frame-Options - can't be embedded
- Window briefly visible when clicked - focus requirement

---

## 🎉 Why This Should Work

1. **WS_EX_NOACTIVATE removed** ✅
   - Window CAN now be activated
   - SetFocus will succeed

2. **SetForegroundWindow called** ✅
   - Main window brought to foreground
   - Parent window is now "active"

3. **SetFocus on WebView2 child** ✅
   - Child window receives keyboard focus
   - Chromium sees focused window

4. **PostMessage still sends events** ✅
   - Events are queued in message queue
   - Now WebView2 will PROCESS them (focused!)

**Result:** WebView2 receives input AND processes it! 🎊

---

## 🔍 Debugging Tips

If it still doesn't work, check the console output:

```
InputManager: AcquireWebViewFocus() - Modern Approach (2024)
  SetForegroundWindow(_mainWindowHandle=0x004B0AD6) = True
  SetFocus(_webViewHandle=0x001E0CF0) = 0x001E0CF0
```

- If `SetForegroundWindow` returns `False` → Main window can't be brought to foreground (check for other apps blocking it)
- If `SetFocus` returns `0x00000000` → Focus failed (check error code)
- If both succeed but input still doesn't work → May need to use JavaScript fallback or MoveFocus API

---

## 📝 Commit Message

When committing these changes:

```
CRITICAL FIX: Remove WS_EX_NOACTIVATE to enable WebView2 input

Root cause: WS_EX_NOACTIVATE flag prevented window from receiving focus,
which caused WebView2 to ignore all input events (clicks, keyboard, scroll).

Changes:
- Removed WS_EX_NOACTIVATE from MainWindow extended window styles
- Added SetForegroundWindow API to NativeMethods
- Enhanced InputManager.AcquireWebViewFocus() to properly acquire focus
- Uses modern 2024 best practices (NO AttachThreadInput hack)
- Added focus throttling to prevent spam (100ms interval)

Expected result: WebView2 should now respond to mouse clicks, keyboard
input, and scroll events. Desktop icons may still have click-through issues.

Fixes #[issue-number]
```

---

## 📚 References

### Microsoft Documentation:
- [WebView2 Focus Handling](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2host)
- [CoreWebView2Controller.MoveFocus](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2controller)

### Research Sources:
- Stack Overflow: WebView2 Focus Issues (2024)
- Raymond Chen's Blog: "The Old New Thing" (AttachThreadInput warnings)
- GitHub: MicrosoftEdge/WebView2Feedback #185, #4465

### Key Learnings:
- AttachThreadInput is deprecated as of 2024 (can cause freezes)
- WS_EX_NOACTIVATE prevents focus acquisition
- WebView2 requires true focus to process input (Chromium security)
- SetForegroundWindow + SetFocus is the modern approach

---

**Last Updated:** November 11, 2025
**Status:** Ready for Testing
**Next Step:** Build, run, and test on Windows 10/11
