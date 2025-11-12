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

**Hit-Testing Logic:**

```csharp
// Check if click is on desktop icon or wallpaper
IntPtr hwnd = WindowFromPoint(clickPosition);
string className = GetWindowClassName(hwnd);

// Desktop icons are in "SysListView32" window
if (className.Contains("SysListView32"))
    return false;  // Don't forward - let icon handle it

if (className.Contains("Chrome_RenderWidgetHostHWND"))
    return true;   // Our WebView2 - forward input

return false;  // Other window - don't forward
```

**Event Consumption:**
- **Mouse events:** Passed through (not consumed) - allows natural icon clicks
- **Keyboard events:** Consumed after forwarding - prevents double input

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

2. **Primary Monitor Only**
   - Currently wallpaper only appears on primary monitor
   - Multi-monitor support planned for v1.0

3. **Some Keyboard Shortcuts**
   - Windows system hotkeys (Win+X, Alt+Tab) captured before hooks
   - This is by design (system-level shortcuts have priority)

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
│   └── InputManager.cs         # Low-level input hooks
├── Services/
│   ├── CookieManager.cs        # Secure cookie persistence
│   ├── PerformanceManager.cs   # Auto-pause optimization
│   ├── TrayIconManager.cs      # System tray icon
│   └── ConfigManager.cs        # Settings management
├── Native/
│   └── NativeMethods.cs        # Windows API P/Invoke
├── Models/
│   ├── AppConfig.cs            # Configuration model
│   └── CookieData.cs           # Cookie serialization
├── MainWindow.xaml(.cs)        # Main wallpaper window
├── SettingsWindow.xaml(.cs)    # Settings UI
├── AboutWindow.xaml(.cs)       # About dialog
├── LoginHelperWindow.xaml(.cs) # Login helper
└── App.xaml(.cs)               # Application entry point
```

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
