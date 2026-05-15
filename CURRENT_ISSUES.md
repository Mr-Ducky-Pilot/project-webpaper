# 🛠️ WebPaper — Bugs Fixed & Known Limitations

**Last updated:** 2026-05-09
**Branch:** `claude/fix-app-selection-bug-DcFc4`

This document tracks the bugs that were fixed in this branch and the limitations
that remain (whether by design or because of underlying Windows API constraints).

---

## ✅ Bugs Fixed in This Branch

### 1. "Can't select / click items in foreground apps when WebPaper is on"

**Symptom:** Clicking inside another application (Chrome, Notepad, File Explorer
etc.) sometimes did nothing, or stole focus, or scrambled the desktop's icon
selection.

**Root cause:** `InputManager.MouseHookProc` unconditionally forwarded every
left/right click to `SysListView32` (the desktop icon list) regardless of where
the cursor actually was. When the user clicked on a foreground app, the synthetic
`SendMessage(SysListView32, WM_LBUTTONDOWN/UP)` still fired and corrupted shell
selection state. The `IsUserApplicationForeground()` check in front of this was
also cached for 500 ms, which meant the first click on a not-yet-focused app fell
through and was treated as a wallpaper click.

**Fix:**
- Replaced the "always forward to icons" hack with a proper hit-test:
  `WindowFromPoint` is used to classify the target as `Wallpaper` /
  `DesktopIcon` / `OtherApp`. Only `Wallpaper` clicks are forwarded to the
  WebView2; clicks on a foreground app are passed through completely
  untouched.
- Removed the 500 ms foreground-cache. `GetForegroundWindow` is cheap enough
  to call per event.
- Added a same-process descendant check on `Chrome_RenderWidgetHostHWND` so
  that a Chrome browser window in the foreground is no longer mistaken for our
  own WebView2.

### 2. Shift / Ctrl / Alt / Arrow / Tab keys did not work in the page

**Symptom:** Typing "Hello" produced "hello"; Shift+ArrowRight didn't extend
selection; Ctrl+A didn't select-all; Alt-modified shortcuts were ignored.

**Root causes (two separate bugs):**

a) The keyboard hook was *consuming* modifier keydowns (return value `1`).
   `WH_KEYBOARD_LL` returning non-zero prevents Windows from processing the
   input at all, so the kernel's global key state never registered Shift /
   Ctrl as held. Chromium reads modifier state via `GetKeyState()` when our
   `WM_KEYDOWN` arrives, sees no modifier held, and treats every keystroke as
   un-modified.

b) `ForwardKeyboardEvent` passed the **hook's `lParam`** straight into
   `SendMessage(WM_KEYDOWN, …, lParam)`. The hook's `lParam` is a *pointer to*
   `KBDLLHOOKSTRUCT`, while `WM_KEYDOWN`'s `lParam` is a packed bitfield
   (repeat count, scan code, extended-key flag, transition state). WebView2
   received garbage in those bits.

**Fix:**
- Modifier keys (Shift, Ctrl, Alt, Caps, Win) are now never consumed and never
  injected — they always fall through to `CallNextHookEx` so the kernel can
  update its key state. The page reads that state directly via `GetKeyState`.
- `ForwardKeyboardEvent` now reads the full `KBDLLHOOKSTRUCT` and constructs
  a proper `WM_KEYDOWN`/`WM_KEYUP` `lParam` from it.
- Alt-modified keys are forwarded as `WM_SYSKEYDOWN`/`WM_SYSKEYUP` (with
  `WM_SYSCHAR`) to match what `TranslateMessage` produces for a real keystroke.
- `ToUnicode` is called with the read-only flag (bit 2, value `0x4`) so it no
  longer eats dead-key state.
- `WM_CHAR` is suppressed when Ctrl is held alone, matching `TranslateMessage`.
- System hotkeys (`Alt+Tab`, `Alt+F4`, `Alt+Esc`, `Alt+Space`, `Ctrl+Esc`,
  `Win+anything`) are explicitly let through.

### 3. Multi-monitor was broken

**Symptom:** On systems with monitors arranged to the left of or above the
primary, the wallpaper was offset incorrectly, or only one monitor ever got a
wallpaper.

**Root causes:**

a) After `SetParent(window, WorkerW)` the wallpaper is a **child window**.
   `SetWindowPos` for a child takes coordinates relative to the parent's
   client area, but the existing code passed raw screen coordinates from
   `MonitorInfo.Left/Top`.

b) There was no way to put a wallpaper on every monitor at once.

**Fix:**
- The wallpaper's screen rect is now translated into WorkerW client
  coordinates with `MapWindowPoints(HWND_DESKTOP, workerW, …)` before
  `SetWindowPos`.
- Added a new `WallpaperMode` config option (`SingleMonitor` / `AllMonitors`).
  In `AllMonitors` mode, the primary `MainWindow` spawns one additional
  `MainWindow` per non-primary monitor, each parented into WorkerW with its
  own `WebView2` and `InputManager`. Each `InputManager` is scoped to its
  monitor's screen rect via `SetMonitorBounds`, so events only get forwarded
  by one instance per cursor position.

### 4. Right-click → Settings / Reload / Home / Toggle / About launched a dead second instance

**Symptom:** Clicking any item under the desktop's WebPaper context-menu
showed a "WebPaper is already running" dialog and the command was lost.

**Root cause:** `App.OnLaunched` used a single-instance `Mutex`. The second
process was correctly blocked from starting its own UI, but no IPC delivered
the command-line argument to the running primary instance.

**Fix:**
- New `Services.IpcServer` exposes a per-user named pipe (`WebPaper.IPC.v1`).
- The primary instance starts the server at the end of initialization.
- A second instance launched with one of the context-menu commands
  (`--settings`, `--reload`, `--home`, `--toggle`, `--about`) now opens the
  pipe, sends the command, and exits silently. The server hands the command
  to the existing `ExecuteCommand` dispatcher on the UI thread.

---

## 📋 Known Limitations (Unchanged)

### Trackpad two-finger scroll

**Status:** Still does not work. Windows precision-touchpad gestures don't
generate `WM_MOUSEWHEEL` messages that low-level hooks can capture; they're
delivered straight to the focused window. Since the wallpaper is a `WS_CHILD`
of WorkerW, it can't take focus to receive them.

**Workarounds:** mouse wheel, arrow keys, scrollbars, or page-specific
keyboard shortcuts (Space, Page Up/Down).

This is the same limitation Lively Wallpaper has.

### DRM video (Netflix, Disney+)

**Status:** Not supported. WebView2 does not include the Widevine CDM, so
DRM-protected video won't play. This is a Microsoft-imposed limitation of
WebView2 itself.

### Some windowed games on top of WebPaper

**Status:** If a game runs without grabbing exclusive fullscreen, mouse-hover
state can briefly leak between the wallpaper and the game. The performance
manager's auto-pause covers true exclusive fullscreen via fullscreen
detection.

---

## 🔁 Reporting New Issues

Open an issue at [`Mr-Ducky-Pilot/project-webpaper`](https://github.com/Mr-Ducky-Pilot/project-webpaper/issues)
with:
- Detailed description and steps to reproduce
- Log output from `%LocalAppData%\WebPaper\Logs\webpaper-<date>.log`
- Windows build (`winver`) and WebView2 runtime version
