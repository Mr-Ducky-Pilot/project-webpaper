# 🚨 Known Issues - WebPaper

**Last Updated:** November 11, 2025
**Status:** Mostly functional with one known limitation

---

## ✅ Working Features

WebPaper successfully:

- ✅ Renders webpages as desktop wallpaper behind icons
- ✅ Mouse clicks (left, right, middle) work perfectly
- ✅ Keyboard typing works (text input, shortcuts, etc.)
- ✅ Mouse wheel scrolling works
- ✅ Arrow key scrolling works
- ✅ Scrollbar interactions work
- ✅ Desktop icons remain clickable
- ✅ Context-aware input (only captures when mouse over wallpaper)

---

## ⚠️ Known Limitation: Trackpad Two-Finger Scroll

### Issue Description

**Trackpad two-finger scrolling does not work** on the wallpaper webpage.

**Affected:** Laptop/notebook precision touchpads
**Workarounds:** Use mouse wheel, arrow keys, or webpage scrollbars

### Root Cause

Windows Precision Touchpad two-finger scroll gestures **do not generate `WM_MOUSEWHEEL` messages** that can be captured by low-level hooks (`WH_MOUSE_LL`). Instead, they:

1. Send messages directly to the focused window (bypassing system hooks)
2. Use different message types (`WM_GESTURE`, `WM_VSCROLL`, or low-delta `WM_MOUSEWHEEL`)
3. Require the target window to have focus

Since WebPaper's window is a `WS_CHILD` of WorkerW (positioned behind the desktop), it cannot receive proper focus, and thus cannot receive direct touchpad scroll messages.

### Technical Details

From Windows API research:

> "Touchpad scroll events are sent only to the target window, so they cannot be captured through system-wide hooks."

**Evidence from logs:**

- Zero `WM_MOUSEWHEEL` messages logged when using trackpad scroll
- Mouse wheel scroll generates messages that are successfully captured
- This is a fundamental Windows API limitation, not a bug in WebPaper

### Why Not Fixed?

Fixing this would require:

**Option A: Raw Input API**

- Register for HID touchpad device (`RegisterRawInputDevices`)
- Parse raw HID reports for scroll gestures
- Complex implementation with marginal benefit

**Option B: Window Focus Manipulation**

- Temporarily bring window to foreground on scroll
- Would cause visual flashing/artifacts
- Unreliable and poor user experience

**Option C: Give up desktop wallpaper positioning**

- Makes the app not a wallpaper anymore
- Defeats the entire purpose of WebPaper

### Workarounds for Users

**Recommended methods:**

1. **Use mouse wheel** - Works perfectly ✅
2. **Use arrow keys** (↑↓) - Works perfectly ✅
3. **Click and drag scrollbars** - Works perfectly ✅
4. **Use external mouse** - Mouse wheel works ✅

**Alternative:**

- If the website has keyboard shortcuts (Space, Page Up/Down), use those

### Comparison with Other Apps

**Lively Wallpaper:** Also does not support trackpad two-finger scroll (same Windows limitation)
**Wallpaper Engine:** Unknown (uses DirectX rendering, different architecture)

---

## 📞 Reporting Issues

If you encounter issues not listed here:

1. Check the [GitHub Issues](../../issues) page
2. Create a new issue with:
   - Detailed description of the problem
   - Steps to reproduce
   - Log file contents (found in `%LocalAppData%\WebPaper\Logs\`)
   - Your Windows version and WebView2 version

---

**Document Status:** Living document - updated as issues are discovered/resolved

**Last Updated:** November 11, 2025
