# 🚨 Current Issues - WebPaper Input System

**Last Updated:** November 11, 2025
**Priority:** Critical
**Status:** Under Investigation

---

## 📋 Executive Summary

WebPaper successfully renders webpages as desktop wallpaper behind icons, but **input events (clicks, scrolls, keyboard) are not reaching the WebView2 control**. The input forwarding system detects and forwards events correctly, but the WebView2 window does not respond to synthetic input when parented as a `WS_CHILD` window behind the desktop surface.

**Impact:** Wallpaper is **view-only** - cannot interact with webpage content.

---

## ❌ Critical Issue: WebView2 Input Not Working

### Problem Description

The application successfully:
- ✅ Renders webpage as wallpaper behind desktop icons
- ✅ Detects mouse clicks and keyboard events via low-level hooks
- ✅ Forwards events to WebView2 window handle with correct coordinates
- ✅ Calls `SetFocus()` on WebView2 before forwarding events

But the webpage **does not respond** to:
- ❌ Mouse clicks (left, right, middle)
- ❌ Mouse wheel scrolling
- ❌ Keyboard typing
- ❌ Hover effects

**Additionally:**
- ❌ Desktop icons cannot be clicked (wallpaper window is blocking them)

### Current Behavior

**What we see in logs:**

```
InputManager: WindowFromPoint = 'Chrome_RenderWidgetHostHWND' -> FORWARD (Our WebView2 wallpaper)
InputManager: LCLICK at (1682,441) - Forward: True
InputManager: Forwarding LCLICK at screen(1682,441) -> client(1682,441) to 0x0059084A
  ScreenToClient conversion: SUCCESS
InputManager: ~42 events/sec
```

**What happens:**
- Events are detected ✅
- Events are forwarded to correct window handle ✅
- Coordinates are converted correctly ✅
- SetFocus() is called ✅
- PostMessage() succeeds (returns true) ✅
- **But webpage does NOT respond** ❌

---

## 🔍 Root Cause Analysis

### The Fundamental Problem

**WebView2 is a `WS_CHILD` window of WorkerW, positioned behind the desktop surface.**

This creates several architectural issues:

#### 1. **Focus Cannot Be Acquired**

```csharp
// Our window hierarchy:
Progman (desktop host)
 └── WorkerW (behind desktop icons)
      └── MainWindow (WS_CHILD, our app)
           └── WebView2 (Chrome_RenderWidgetHostHWND)
```

- Windows prevents `WS_CHILD` windows behind the desktop from receiving focus
- `SetFocus()` calls may fail silently or return false focus handle
- WebView2 requires true input focus to process mouse/keyboard events

#### 2. **Input Messages Require Active Window Focus**

WebView2 (Chromium-based) expects:
- The window to have **active focus** (WM_SETFOCUS received)
- The window to be in the **foreground** window chain
- Mouse cursor position to be relative to a **focused window**

Our window is:
- Behind the desktop (`WS_CHILD` of WorkerW)
- Never receives true focus (blocked by window hierarchy)
- Not in the foreground window chain

#### 3. **PostMessage vs SendInput**

We currently use `PostMessage()` to forward input:
- ✅ Successfully posts messages to window queue
- ❌ WebView2 ignores messages from unfocused windows
- ❌ Chromium's renderer process filters unfocused input

Alternative `SendInput()` approach:
- Injects input at hardware/driver level
- **But** still requires target window to have focus
- May not work for `WS_CHILD` windows behind desktop

### Research from Lively Wallpaper

Lively Wallpaper uses:
- **RawInput API** instead of hooks
- **DirectX overlays** for some wallpaper types
- **Different window attachment** (not confirmed if WS_CHILD)
- Possible **layered windows** approach

Key insight from Lively source:
```csharp
// Lively uses RawInputDevice with ExInputSink
RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse,
    RawInputDeviceFlags.ExInputSink, hwnd);
```

This captures input even without focus, BUT still requires forwarding to a window that **can** receive input.

---

## 🧪 What We've Tried

### Attempt 1: Direct PostMessage Forwarding ❌

**Code:**
```csharp
PostMessage(_webViewHandle, WM_LBUTTONDOWN, wParam, lParam);
```

**Result:** Messages posted successfully, WebView2 doesn't respond.

**Why it failed:** WebView2 ignores input from unfocused windows.

---

### Attempt 2: ScreenToClient Coordinate Conversion ❌

**Code:**
```csharp
POINT clientPt = pt;
ScreenToClient(_webViewHandle, ref clientPt);
PostMessage(_webViewHandle, msg, wParam, MakeLParam(clientPt.X, clientPt.Y));
```

**Result:** Coordinates convert correctly, but still no response.

**Why it failed:** Coordinate conversion wasn't the issue - focus is.

---

### Attempt 3: SetFocus Before Forwarding ❌

**Code:**
```csharp
if (msg == WM_LBUTTONDOWN) {
    SetFocus(_webViewHandle);
}
PostMessage(_webViewHandle, msg, wParam, lParam);
```

**Result:** SetFocus returns a handle (possibly false success), but WebView2 still doesn't respond.

**Why it failed:** Cannot acquire true focus for `WS_CHILD` window behind desktop.

---

### Attempt 4: Accept Clicks on Chrome_RenderWidgetHostHWND ❌

**Code:**
```csharp
if (classNameStr.Contains("Chrome_RenderWidgetHostHWND")) {
    return true;  // Forward to our WebView2
}
```

**Result:** Events are forwarded, but WebView2 doesn't process them.

**Why it failed:** Detection and forwarding work, but WebView2 won't process unfocused input.

---

## 📊 Diagnostic Information

### Window Hierarchy

```
Progman (0x00010010)
 └── WorkerW (0x000202A4)
      └── WebPaper MainWindow (0x00560B2E) [WS_CHILD]
           └── WebView2 (0x0059084A) [Chrome_RenderWidgetHostHWND]
```

### Window Styles

**MainWindow after attachment:**
```
Current style:  0x34CF0000
New style:      0x76030000 (with WS_CHILD)
Extended style: 0x08000000 (WS_EX_NOACTIVATE)
```

**Style breakdown:**
- `WS_CHILD (0x40000000)` - Child window (REQUIRED for SetParent)
- `WS_VISIBLE (0x10000000)` - Visible
- `WS_CLIPCHILDREN (0x02000000)` - Don't paint over children
- `WS_CLIPSIBLINGS (0x04000000)` - Don't paint over siblings

### Input Flow Trace

```
User clicks at (1682, 441)
    ↓
Low-level mouse hook (WH_MOUSE_LL) captures event
    ↓
IsClickOnWallpaper() checks WindowFromPoint()
    → Returns: Chrome_RenderWidgetHostHWND (our WebView2) ✅
    ↓
ForwardMouseEvent() called
    → SetFocus(0x0059084A) - returns handle (possibly fake)
    → ScreenToClient() - converts coordinates ✅
    → PostMessage(0x0059084A, WM_LBUTTONDOWN, ...) - returns true ✅
    ↓
WebView2 window receives message in queue
    ↓
Chromium renderer checks focus state
    → Window is NOT focused (no WM_SETFOCUS received)
    → Window is NOT in foreground chain
    → Input is IGNORED ❌
```

---

## 🚧 Blocking Issues

### Issue 1: Desktop Icons Not Clickable

**Problem:** WindowFromPoint() returns our WebView2 for ALL desktop surface clicks, including icon areas.

**Current behavior:**
```
Click on desktop icon
    ↓
WindowFromPoint() → Chrome_RenderWidgetHostHWND (our wallpaper)
    ↓
IsClickOnWallpaper() → true (forward to wallpaper)
    ↓
Icon never receives click ❌
```

**Root cause:** Our window is positioned over the desktop, so WindowFromPoint hits us first.

**Needs:** Better hit-testing to detect icon locations and NOT forward those clicks.

---

### Issue 2: WebView2 Cannot Gain Focus

**Problem:** WS_CHILD windows behind desktop cannot receive true focus.

**Evidence:**
- SetFocus() returns a handle, but WebView2 never receives WM_SETFOCUS
- Windows blocks focus for windows behind desktop surface
- Chromium requires true focus to process input

**Potential solutions:**
1. Change window attachment method (not use WS_CHILD)
2. Use transparent overlay window approach
3. Inject input at higher level (SendInput with focus hijacking)
4. Use Direct2D/DirectX rendering instead of window-based approach

---

## 💡 Potential Solutions Under Investigation

### Solution 1: Non-Child Window Approach (HIGH PRIORITY)

**Concept:** Don't use `SetParent()` and `WS_CHILD`. Instead:

```csharp
// Remove WS_CHILD, keep as top-level window
SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

// Position behind desktop using Z-order
SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, width, height,
    SWP_NOACTIVATE);

// Set as layered window
SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
```

**Pros:**
- Window CAN receive focus (top-level window)
- Can toggle WS_EX_TRANSPARENT to allow icon clicks through
- Similar to how some overlay apps work

**Cons:**
- May appear above desktop in some scenarios
- WS_EX_TRANSPARENT makes window completely non-interactive
- Need to toggle transparency dynamically

**Status:** Not yet tested

---

### Solution 2: SendInput with Focus Hijacking (MEDIUM PRIORITY)

**Concept:** Temporarily bring window to foreground, inject input, then hide it.

```csharp
// On click detected:
SetForegroundWindow(mainWindowHandle);  // Bring to front temporarily
SetFocus(webViewHandle);                // Give WebView2 focus

// Inject input at hardware level
INPUT[] inputs = CreateMouseInput(pt.X, pt.Y, MOUSEEVENTF_LEFTDOWN);
SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

// Immediately hide again
SetWindowPos(hwnd, HWND_BOTTOM, ...);
```

**Pros:**
- WebView2 gets true focus
- Hardware-level input should work

**Cons:**
- Window will flash visible for 1 frame
- May cause visual artifacts
- Desktop icons will still be blocked
- Very hacky approach

**Status:** Not yet tested

---

### Solution 3: Transparent Overlay Pattern (HIGH PRIORITY)

**Concept:** Use two windows:

1. **Background Window:** Renders WebView2 (not interactive)
2. **Overlay Window:** Transparent, captures input, forwards to background

```csharp
// Background window (behind desktop, renders webpage)
SetParent(bgWindow, workerW);
// This stays as WS_CHILD, non-interactive

// Overlay window (on top, transparent, captures input)
SetWindowLong(overlayWindow, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
SetLayeredWindowAttributes(overlayWindow, 0, 0, LWA_ALPHA);  // Fully transparent

// Overlay captures input, programmatically controls background WebView2
// via CoreWebView2.ExecuteScriptAsync() instead of forwarding messages
```

**Pros:**
- Overlay can receive focus (top-level window)
- Can selectively allow icon clicks through (hit-testing)
- Doesn't require WebView2 to process synthetic input

**Cons:**
- Complex two-window architecture
- Need to translate clicks to JavaScript commands
- Hover effects may not work correctly

**Status:** Not yet tested

---

### Solution 4: Use Lively's RawInput Approach (MEDIUM PRIORITY)

**Concept:** Switch from hooks to RawInput API.

```csharp
RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse,
    RawInputDeviceFlags.ExInputSink, hwnd);
```

**Pros:**
- More efficient than hooks
- Can capture input even when app not focused

**Cons:**
- Still requires forwarding to a window that CAN receive input
- Doesn't solve the fundamental focus problem

**Status:** Research in progress

---

### Solution 5: Direct WebView2 Control via JavaScript (LOW PRIORITY)

**Concept:** Instead of forwarding OS input, execute JavaScript on the page.

```csharp
// On click at (x, y):
await webView.CoreWebView2.ExecuteScriptAsync($@"
    var element = document.elementFromPoint({x}, {y});
    if (element) {{
        element.click();
    }}
");
```

**Pros:**
- Bypasses Windows input focus issues
- Direct control over webpage

**Cons:**
- Very high latency (50-200ms per click)
- Doesn't work for hover effects
- Doesn't work for complex interactions (drag, right-click menus)
- Scroll events hard to simulate smoothly

**Status:** Possible fallback for click-only scenarios

---

## 📚 Research & References

### Lively Wallpaper Source Code

**Repository:** https://github.com/rocksdanister/lively

**Key files:**
- `/src/livelywpf/livelywpf/Core/InputForwarding/RawInputDX.xaml.cs`
- Input forwarding using RawInput API
- Supports multiple wallpaper types with different input approaches

**Insights:**
- Uses `RawInputDevice.RegisterDevice()` with `ExInputSink`
- Forwards input via `PostMessageW`
- Filters wallpaper types (only web, app, game receive input)
- Has complex display/monitor handling logic

**Questions to investigate:**
- How does Lively's window attachment differ from ours?
- Does Lively use WS_CHILD or a different approach?
- How does Lively handle desktop icon clicks?

---

### Microsoft WebView2 Focus Issues

**GitHub Issues:**
- [#397 - Unable to focus WebView2 when it's the only focusable control](https://github.com/MicrosoftEdge/WebView2Feedback/issues/397)
- [#4465 - WebView2 Input box in a web page does not get focus](https://github.com/MicrosoftEdge/WebView2Feedback/issues/4465)

**Workarounds mentioned:**
```csharp
// Get child window and focus it directly
public const uint GW_CHILD = 5;
var child = GetWindow(webView.Handle, GW_CHILD);
SetFocus(child);
```

**Limitation:** This works for normal WinForms/WPF windows, but our window is behind desktop.

---

### Windows API Documentation

**SetParent:**
- Makes window a child of another window
- Child windows cannot receive focus unless parent has focus
- **WorkerW never has focus** (it's a desktop worker window)

**WS_EX_TRANSPARENT:**
- Allows mouse clicks to "pass through" window
- **But** makes window completely non-interactive
- Cannot be applied selectively (all or nothing)

**WS_EX_LAYERED:**
- Allows alpha blending and transparency
- Can be combined with per-pixel alpha
- May allow transparent "holes" for icon click-through

---

## 🎯 Next Steps

### Immediate Actions

1. **Test Solution 1** (Non-Child Window Approach)
   - Remove SetParent() and WS_CHILD
   - Use SetWindowPos(HWND_BOTTOM) instead
   - Test if window can receive focus
   - Test if desktop icons are clickable

2. **Test Solution 3** (Transparent Overlay)
   - Create proof-of-concept with two windows
   - Test JavaScript-based input simulation
   - Measure latency and responsiveness

3. **Research Lively Source** (Deeper Dive)
   - Examine exact window attachment method
   - Identify how icon click-through works
   - Look for any undocumented tricks

### Long-term Strategy

**If no solution works:**
- Consider pivoting to "view-only" wallpaper mode
- Add "click to open in browser" feature
- Focus on video/animation wallpapers (auto-playing content)

**If solution found:**
- Document the working approach
- Optimize for performance and latency
- Add comprehensive input testing

---

## 📞 Help Needed

**If you have experience with:**
- Windows desktop composition and window Z-ordering
- WebView2 focus and input handling
- Low-level Windows input injection
- Wallpaper engine implementations

**Please contribute:** [GitHub Discussions](../../discussions) or [Issues](../../issues)

---

## 🔗 Related Files

- [`src/WebPaper/Core/InputManager.cs`](src/WebPaper/Core/InputManager.cs) - Input forwarding implementation
- [`src/WebPaper/MainWindow.xaml.cs`](src/WebPaper/MainWindow.xaml.cs) - Window setup and attachment
- [`src/WebPaper/Native/NativeMethods.cs`](src/WebPaper/Native/NativeMethods.cs) - Win32 API declarations

---

**Document Status:** Living document - will be updated as investigation progresses

**Last Updated:** November 11, 2025
