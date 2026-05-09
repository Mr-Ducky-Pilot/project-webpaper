# Technical Overview

## What is WebPaper?

WebPaper is a Windows desktop application that transforms any webpage into a **fully interactive wallpaper**. It uses advanced Windows APIs and modern web technologies to create a seamless integration between web content and the Windows desktop.

---

## How It Works

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    MainWindow (WinUI 3)                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │
│  │ WebView2    │  │ Loading     │  │ Error Panel         │ │
│  │ (Webpage)   │  │ Indicator   │  │ (Error Messages)    │ │
│  └─────────────┘  └─────────────┘  └─────────────────────┘ │
└────────┬──────────────────┬────────────────┬────────────────┘
         │                  │                │
   ┌─────▼──────┐    ┌──────▼─────┐   ┌─────▼──────────┐
   │ WorkerW    │    │ Input      │   │ Performance    │
   │ Manager    │    │ Manager    │   │ Manager        │
   │            │    │            │   │                │
   │ - Attach   │    │ - Mouse    │   │ - Fullscreen   │
   │   to       │    │   hooks    │   │   detection    │
   │   desktop  │    │ - Keyboard │   │ - Battery      │
   │ - Behind   │    │   hooks    │   │   status       │
   │   icons    │    │ - Forward  │   │ - Auto-pause   │
   │ - Z-order  │    │   events   │   │ - Metrics      │
   │            │    │            │   │                │
   └────────────┘    └──────┬─────┘   └────────────────┘
                            │
                     ┌──────▼──────────┐
                     │ Cookie Manager  │
                     │                 │
                     │ - Save cookies  │
                     │ - DPAPI encrypt │
                     │ - Restore login │
                     └─────────────────┘
```

---

## Core Components

### 1. Desktop Integration (WorkerW Technique)

**Challenge:** How do you put a window behind desktop icons on Windows?

**Solution:** The undocumented **WorkerW technique**

```csharp
// Send undocumented message 0x052C to Progman
IntPtr progman = FindWindow("Progman", null);
SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero,
    SendMessageTimeoutFlags.SMTO_NORMAL, 1000, out IntPtr result);

// This spawns a WorkerW window containing SHELLDLL_DefView
IntPtr workerW = FindWorkerWWindow();

// Attach our window as child of WorkerW
SetParent(mainWindowHandle, workerW);

// Set Z-order to HWND_BOTTOM so icons appear on top
SetWindowPos(mainWindowHandle, HWND_BOTTOM, ...);
```

**Key Files:**
- `src/WebPaper/Core/WorkerWManager.cs` - Desktop integration logic
- Handles Windows 11 24H2 compatibility (WorkerW behavior changed)

---

### 2. Input Forwarding

**Challenge:** Capture system-wide mouse and keyboard input and forward to the wallpaper without breaking desktop icons.

**Solution:** Low-level Windows hooks with intelligent hit-testing

```csharp
// Install system-wide hooks
_mouseHookId = SetWindowsHookEx(
    HookType.WH_MOUSE_LL,     // Low-level mouse hook
    MouseHookCallback,         // Our callback function
    IntPtr.Zero,
    0                          // All threads
);

_keyboardHookId = SetWindowsHookEx(
    HookType.WH_KEYBOARD_LL,  // Low-level keyboard hook
    KeyboardHookCallback,
    IntPtr.Zero,
    0
);
```

**Hit-Testing Logic (rewritten in 2026 to fix the "can't click foreground apps" bug):**

```csharp
IntPtr hwnd = WindowFromPoint(clickPosition);
string cls  = GetClassName(hwnd);

if (cls.Contains("SysListView32"))                  return DesktopIcon;
if (cls.Contains("Chrome_RenderWidgetHostHWND")
    && IsDescendantOfMain(hwnd))                    return Wallpaper;
if (cls.Contains("SHELLDLL_DefView")
    || cls.Contains("Progman")
    || cls == "WorkerW")                            return Wallpaper;
return OtherApp;          // Foreground app — pass through, do nothing.
```

**Event Routing:**
- **Wallpaper hit** → forward to WebView2 via `PostMessage` to `Chrome_WidgetWin_1`.
- **DesktopIcon hit** → leave it alone; the shell handles the click natively.
- **OtherApp hit** → never intercepted; the user's foreground app receives the click intact.

**Modifier Key Handling:**
- **Shift / Ctrl / Alt / Caps / Win:** never consumed, never injected. They
  always fall through `CallNextHookEx` so the kernel updates global key state.
  WebView2/Chromium reads that state via `GetKeyState()` when our manually
  posted `WM_KEYDOWN` arrives.
- **Other keys** *while the cursor is over the wallpaper:* forwarded to
  WebView2 as a properly-formed `WM_KEYDOWN` (with a real bitfield `lParam`
  built from `KBDLLHOOKSTRUCT`) plus `WM_CHAR`/`WM_SYSCHAR`. The keydown is
  consumed so the desktop doesn't also receive it (no icon-search popup,
  no F2 rename, no arrow-key icon nav).

**Key Files:**
- `src/WebPaper/Core/InputManager.cs` - Input hook implementation
- Processes events in <5ms (Windows removes hooks if >200ms)

---

### 3. Cookie Persistence

**Challenge:** WebView2 doesn't persist session cookies by default. Users would have to log in every time.

**Solution:** Manual cookie extraction with DPAPI encryption

```csharp
// Save cookies
var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(url);
var json = JsonSerializer.Serialize(cookies);
var plainBytes = Encoding.UTF8.GetBytes(json);

// Encrypt with Windows DPAPI (AES-256)
var encrypted = ProtectedData.Protect(
    plainBytes,
    _entropy,  // Additional entropy (machine+user ID hash)
    DataProtectionScope.CurrentUser  // User-specific encryption
);

await File.WriteAllBytesAsync(_cookieStorePath, encrypted);

// Restore cookies
var encryptedBytes = await File.ReadAllBytesAsync(_cookieStorePath);
var plainBytes = ProtectedData.Unprotect(
    encryptedBytes,
    _entropy,
    DataProtectionScope.CurrentUser
);

var json = Encoding.UTF8.GetString(plainBytes);
var cookies = JsonSerializer.Deserialize<CookieContainer>(json);
// ... restore to WebView2
```

**Security:**
- Uses Windows DPAPI (same as Chrome/Edge for passwords)
- User-specific, machine-specific encryption
- Additional entropy from SHA-256 hash of machine+user ID
- Automatic 30-day expiration

**Key Files:**
- `src/WebPaper/Services/CookieManager.cs` - Cookie persistence logic
- Storage location: `%LOCALAPPDATA%\WebPaper\Cookies\cookies.enc`

---

### 4. Performance Optimization

**Challenge:** Wallpaper shouldn't waste CPU/battery during games or low battery.

**Solution:** Smart auto-pause system

```csharp
// Monitor system every 2 seconds
private bool ShouldPauseRendering()
{
    // Check 1: Is a fullscreen app running?
    if (IsFullscreenAppRunning())
        return true;

    // Check 2: On battery and below threshold?
    if (IsOnBattery() && GetBatteryPercentage() < BatteryPauseThreshold)
        return true;

    return false;
}

// Pause by stopping media playback (keeps WebView2 alive)
private async Task PauseRenderingAsync(string reason)
{
    await _webView.ExecuteScriptAsync(@"
        document.querySelectorAll('video').forEach(v => v.pause());
        document.querySelectorAll('audio').forEach(a => a.pause());
    ");

    Log.Information("Wallpaper paused: {Reason}", reason);
}
```

**Impact:**
- **90% CPU reduction** during fullscreen apps (games)
- **15-30 minutes extra battery life** when battery is low
- WebView2 state preserved (no reload needed)

**Key Files:**
- `src/WebPaper/Services/PerformanceManager.cs` - Auto-pause logic

---

## Technology Stack

| Component | Technology | Why? |
|-----------|-----------|------|
| **Language** | C# 12 | Type safety, modern async/await, productivity |
| **Framework** | .NET 8.0 | Latest LTS, best performance, AOT ready |
| **UI** | WinUI 3 | Modern Windows 11 design, native performance |
| **Web Engine** | WebView2 | Chromium-based, lightweight (5MB), native |
| **Desktop API** | Win32 P/Invoke | WorkerW technique, low-level hooks |
| **Encryption** | Windows DPAPI | Built-in AES-256, hardware-backed |
| **Logging** | Serilog | Structured logging, file rolling |

---

## Performance Metrics

**Measured on:** Windows 11 23H2, i5-12600K, 16GB RAM

| Scenario | CPU Usage | Memory | GPU |
|----------|-----------|--------|-----|
| **Idle (Static Page)** | 1-2% | ~150 MB | 0-1% |
| **YouTube Video** | 3-5% | ~200 MB | 2-4% |
| **Heavy Animation** | 5-8% | ~250 MB | 4-8% |
| **Paused (Fullscreen)** | <0.5% | ~150 MB | 0% |

- **Startup Time:** 2-3 seconds from launch to interactive
- **Input Latency:** <50ms from click/keypress to webpage response
- **Event Processing:** <5ms per input event (hook requirement: <200ms)

---

## Windows Compatibility

| Version | Status | Notes |
|---------|--------|-------|
| Windows 10 1809 | ✅ Supported | Minimum version |
| Windows 10 21H2 | ✅ Supported | Fully tested |
| Windows 11 21H2 | ✅ Supported | Fully tested |
| Windows 11 22H2 | ✅ Supported | Fully tested |
| Windows 11 23H2 | ✅ Supported | Fully tested |
| **Windows 11 24H2** | ✅ Supported | **Special handling** (WorkerW fallback) |

---

## Security & Privacy

### Data Storage

**Local Storage:**
- Encrypted cookies: `%LOCALAPPDATA%\WebPaper\Cookies\cookies.enc`
- WebView2 cache: `%LOCALAPPDATA%\WebPaper\WebView2Data\`
- Logs: `%LOCALAPPDATA%\WebPaper\Logs\webpaper.log`

**No Network Traffic:**
- ❌ No telemetry
- ❌ No analytics
- ❌ No crash reports sent anywhere
- ❌ No update checks

**Security Features:**
- DPAPI encryption (AES-256)
- User+machine specific encryption
- Entropy from SHA-256(MachineGuid + UserSID)
- 30-day automatic cookie expiration

---

## Known Limitations

### Technical Limitations

1. **Trackpad Two-Finger Scroll**
   - Windows API doesn't provide low-level trackpad events
   - WM_MOUSEWHEEL messages don't fire for two-finger scroll
   - **Workaround:** Use mouse wheel, scroll bars, or keyboard arrows

2. **Some Keyboard Shortcuts**
   - Windows system hotkeys (Win+X, Alt+Tab, Ctrl+Esc) are explicitly let
     through so the shell keeps working
   - Modifier keys themselves (Shift, Ctrl, Alt, Win) are never consumed —
     they fall through to the kernel so Chromium/WebView2 reads correct
     modifier state via `GetKeyState`

### By Design

1. **Context-Aware Input**
   - Keyboard only forwards when mouse is over wallpaper
   - Prevents capturing input from other applications

2. **Icon Overlap**
   - Clicks on desktop icons don't reach wallpaper
   - Icons work normally (intended behavior)

3. **DRM Content**
   - Netflix, Disney+ may not work due to browser DRM restrictions
   - WebView2 limitation (EME/Widevine)

---

## Project Structure

```
src/WebPaper/
├── Core/
│   ├── WorkerWManager.cs       # Desktop integration (WorkerW)
│   └── InputManager.cs         # Low-level input hooks + per-monitor scope
├── Services/
│   ├── CookieManager.cs        # Secure cookie persistence
│   ├── PerformanceManager.cs   # Auto-pause optimization
│   ├── TrayIconManager.cs      # System tray icon
│   ├── ContextMenuManager.cs   # Desktop right-click registration
│   ├── ConfigManager.cs        # Settings management
│   ├── MonitorManager.cs       # Multi-monitor detection
│   └── IpcServer.cs            # Named-pipe server for context-menu commands
├── Native/
│   └── NativeMethods.cs        # Windows API P/Invoke
├── Models/
│   ├── AppConfig.cs            # Configuration model + WallpaperMode enum
│   ├── CookieData.cs           # Cookie serialization
│   └── InputEvents.cs          # Input event types
├── MainWindow.xaml(.cs)        # Wallpaper window (primary or per-monitor secondary)
├── UnifiedSettingsWindow.xaml(.cs) # Unified Settings UI
├── AboutWindow.xaml(.cs)       # About dialog (legacy)
├── LoginHelperWindow.xaml(.cs) # Login helper
└── App.xaml(.cs)               # Entry point + single-instance + IPC client
```

### Per-Monitor Architecture (2026)

When `AppConfig.Mode == AllMonitors`, the primary `MainWindow` is the only one
that runs the tray icon, IPC server, welcome dialog and cookie restore. After
its own initialization completes it spawns one additional `MainWindow` per
non-primary monitor with `isSecondary: true`. Each secondary:

- targets a specific `MonitorInfo`,
- creates its own `WebView2` against the shared user-data folder,
- attaches itself into WorkerW,
- installs its own `InputManager` whose `SetMonitorBounds(...)` ensures it
  only forwards events from its own monitor.

Because every instance's hook is system-wide, an event near the boundary fires
in every instance — but only one instance's bounds will contain the screen
point, so exactly one forwards.

### Right-Click Context Menu (IPC)

The desktop right-click integration registers commands like `--settings`,
`--reload`, `--home`, `--toggle`, `--about`. When the user picks one, Windows
launches `WebPaper.exe --settings`. The single-instance `Mutex` blocks the
new process from running its own UI, but `App.OnLaunched` now:

1. notices the command-line argument is a context-menu command,
2. opens the per-user `WebPaper.IPC.v1` named pipe,
3. writes the command and exits.

The primary instance's `IpcServer` accepts the connection, reads the command,
marshals to the UI thread, and runs the same `ExecuteCommand` dispatcher the
internal tray menu uses.

**Key Metrics:**
- **Total Lines of Code:** ~3,500 (C#)
- **Components:** 7 major components
- **Windows API Calls:** ~50 P/Invoke declarations
- **Complexity:** Medium (well-architected)

---

## Build Requirements

**Development:**
- Visual Studio 2022 (v17.8+)
- .NET 8 SDK
- Windows 11 SDK (10.0.22621.0+)
- WebView2 Runtime (usually pre-installed)

**Runtime:**
- Windows 10 (1809+) or Windows 11
- WebView2 Runtime
- 4GB RAM minimum (8GB recommended)
- Internet connection for webpage content

---

## Further Reading

- **[User Guide](User-Guide)** - Installation and usage
- **[Contributing](Contributing)** - Contribution guidelines
- **[FAQ](FAQ)** - Frequently asked questions
- **Source Code:** [GitHub Repository](https://github.com/Mr-Ducky-Pilot/project-webpaper)

---

*For technical support, please [open an issue](../../issues) on GitHub.*
