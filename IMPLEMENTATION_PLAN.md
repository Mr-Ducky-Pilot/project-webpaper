# Project WebPaper - Windows Implementation Plan
**Created:** November 8, 2025
**Target Platform:** Windows 11 (Windows 10 compatibility as secondary goal)
**Project Goal:** Lightweight, fully interactive webpage wallpaper renderer

---

## Executive Summary

Based on comprehensive research of existing solutions (Lively Wallpaper), latest Windows 11 24H2 compatibility data, and current best practices, this document outlines the implementation strategy for Project WebPaper.

**Key Decision:** Build a lightweight, focused solution from scratch rather than forking Lively Wallpaper, with the following justifications:
- Lively is feature-rich but heavy (supports videos, games, shaders, etc.)
- We need laser focus on web content with optimal performance
- Simpler codebase for easier maintenance and iteration
- Full control over authentication/cookie management
- Can study Lively's approach without inheriting unnecessary complexity

**Timeline Estimate:** 8-10 weeks for MVP, 12-14 weeks for production-ready v1.0

---

## 1. Technology Stack (FINALIZED)

### Core Framework
- **Language:** C# 12 (.NET 8)
- **UI Framework:** WinUI 3 (Windows App SDK 1.5+)
- **Build System:** MSBuild / Visual Studio 2022

### Web Rendering Engine: **WebView2** (chosen over CefSharp)

**Rationale:**
1. **Native Integration:** Built into Windows, automatic updates via OS
2. **Smaller Distribution:** ~5MB runtime vs 100-150MB for CEF
3. **Better Support:** Official Microsoft support, actively developed
4. **Performance:** No noticeable difference vs CefSharp in benchmarks
5. **Security:** Runs in separate process with sandboxing
6. **Future-Proof:** Microsoft's official web embedding solution

**Trade-offs Accepted:**
- No offscreen rendering (acceptable for our use case)
- Cookie persistence requires manual handling (solvable, see Section 5)
- Windows-only (aligned with current scope)

### Supporting Libraries
```xml
<PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2651.64" />
<PackageReference Include="Microsoft.Windows.SDK.Contracts" Version="10.0.22621.2" />
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.240802000" />
<PackageReference Include="CommunityToolkit.WinUI.UI" Version="8.1.240916" />
```

### Installer & Distribution
- **Primary:** MSIX packaging (Microsoft Store ready)
- **Alternative:** WiX Toolset v4 for traditional installer
- **Auto-updates:** Built into MSIX deployment

---

## 2. Architecture Design

```
┌─────────────────────────────────────────────────────────┐
│              Main Application (WinUI 3)                 │
│  ┌─────────────────────────────────────────────────┐  │
│  │  System Tray UI                                  │  │
│  │  - Quick enable/disable                          │  │
│  │  - URL management                                │  │
│  │  - Settings                                       │  │
│  └─────────────────────────────────────────────────┘  │
└─────────────────┬───────────────────────────────────────┘
                  │
    ┌─────────────┴─────────────┐
    │                           │
┌───▼──────────────┐  ┌────────▼─────────────┐
│ WallpaperWindow  │  │  InputManager        │
│ Manager          │  │  - Mouse hooks       │
│                  │  │  - Keyboard hooks    │
│ - WorkerW setup  │  │  - Focus detection   │
│ - Window mgmt    │  │  - Icon hit-testing  │
│ - Multi-monitor  │  │  - Event forwarding  │
└───┬──────────────┘  └─────────┬────────────┘
    │                           │
    └─────────────┬─────────────┘
                  │
         ┌────────▼──────────────┐
         │  WebView2Renderer     │
         │  - Page rendering     │
         │  - Navigation mgmt    │
         │  - Cookie persistence │
         │  - JS execution       │
         │  - Performance opts   │
         └───────────────────────┘
              │
    ┌─────────┴─────────┐
    │                   │
┌───▼────────┐  ┌──────▼────────┐
│ CookieStore│  │ ConfigManager │
│ - Persist   │  │ - User prefs  │
│ - Restore   │  │ - URLs        │
│ - Sync     │  │ - Profiles    │
└────────────┘  └───────────────┘
```

### Module Breakdown

#### 2.1 WallpaperWindowManager
**Responsibilities:**
- Enumerate and manage displays
- Implement WorkerW technique for each monitor
- Handle display configuration changes
- Manage window lifecycle

**Key Classes:**
```csharp
public class WallpaperWindowManager
{
    private Dictionary<string, WallpaperWindow> _windows;
    private DisplayManager _displayManager;

    public void InitializeWallpapers();
    public void UpdateDisplay(DisplayInfo display);
    public void ShutdownAll();
}

public class WallpaperWindow
{
    private IntPtr _hwnd;
    private IntPtr _workerW;
    private WebView2Renderer _renderer;

    public void AttachToDesktop();
    public void Detach();
}
```

#### 2.2 InputManager
**Responsibilities:**
- Install/uninstall low-level hooks
- Desktop icon hit-testing
- Event forwarding to WebView2
- Focus management

**Critical Implementation Notes:**
- Hook must process events in <200ms (Windows timeout)
- Use dedicated thread for hooks with message pump
- Fall back to Raw Input API if hooks fail
- Graceful degradation if hooks removed by Windows

```csharp
public class InputManager
{
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private Thread _hookThread;

    public void InstallHooks();
    public bool IsClickOnWallpaper(Point pt);
    public void ForwardToWebView(InputEvent evt);
}
```

#### 2.3 WebView2Renderer
**Responsibilities:**
- WebView2 initialization and lifecycle
- Navigation and error handling
- Cookie persistence integration
- Performance optimization

```csharp
public class WebView2Renderer
{
    private WebView2 _webView;
    private CookieManager _cookieManager;
    private string _userDataFolder;

    public async Task InitializeAsync(string url);
    public async Task NavigateAsync(string url);
    public async Task PersistSessionAsync();
    public async Task RestoreSessionAsync();
}
```

#### 2.4 CookieStore
**Responsibilities:**
- Persist session cookies (WebView2 doesn't by default)
- Sync with browser if configured
- Secure storage using Windows Credential Manager

```csharp
public class CookieStore
{
    public async Task<IEnumerable<Cookie>> ExtractCookies();
    public async Task ImportCookies(IEnumerable<Cookie> cookies);
    public async Task SaveToSecureStorage();
    public async Task LoadFromSecureStorage();
}
```

---

## 3. Technical Implementation Details

### 3.1 WorkerW Window Technique (Windows 11 24H2 Compatible)

**Critical Finding from Research:**
Windows 11 24H2 introduced breaking changes - WorkerW window only spawns during wallpaper changes and may not persist. Microsoft acknowledged this broke wallpaper customization apps and is gradually removing compatibility holds.

**Solution Strategy:**
```csharp
public class WorkerWManager
{
    // Strategy 1: Trigger wallpaper change programmatically
    private void ForceWorkerWSpawn()
    {
        // Temporarily change wallpaper to trigger WorkerW creation
        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0,
            CurrentWallpaperPath, SPIF_UPDATEINIFILE);
        Thread.Sleep(100);
    }

    // Strategy 2: Monitor for WorkerW existence
    private void MonitorWorkerW()
    {
        _timer = new Timer(CheckWorkerW, null, 0, 5000);
    }

    // Strategy 3: Fallback to alternative parent window
    private IntPtr FindSuitableParentWindow()
    {
        IntPtr progman = FindWindow("Progman", null);

        // Try WorkerW first
        IntPtr workerW = FindWorkerW();
        if (workerW != IntPtr.Zero)
            return workerW;

        // Fallback: Try Progman directly
        // May render above desktop in some scenarios
        return progman;
    }
}
```

**Testing Requirements:**
- Windows 10 21H2, 22H2
- Windows 11 21H2, 22H2, 23H2, 24H2
- Multi-monitor configurations
- Display scaling variations (100%, 125%, 150%, 200%)

### 3.2 Input Handling (Low-Level Hooks)

**Best Practices from Research:**
1. **Dedicated Hook Thread:** Run hooks on separate thread with message pump
2. **Fast Processing:** Complete in <200ms to avoid Windows timeout
3. **Fallback Strategy:** Use Raw Input API if hooks fail

**Implementation:**
```csharp
public class InputHookManager
{
    private Thread _hookThread;
    private BlockingCollection<InputEvent> _eventQueue;

    public void Start()
    {
        _hookThread = new Thread(HookThreadProc);
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.IsBackground = true;
        _hookThread.Start();

        // Worker thread to process events
        Task.Run(ProcessEventQueue);
    }

    private void HookThreadProc()
    {
        // Install hooks
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL,
            MouseHookCallback, IntPtr.Zero, 0);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL,
            KeyboardHookCallback, IntPtr.Zero, 0);

        // Message pump
        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            // Quick processing - just queue the event
            var evt = new MouseInputEvent(wParam, lParam);
            _eventQueue.TryAdd(evt);
        }

        // Immediately pass to next hook
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private async Task ProcessEventQueue()
    {
        foreach (var evt in _eventQueue.GetConsumingEnumerable())
        {
            // Do expensive processing here
            if (await IsClickOnWallpaper(evt.Position))
            {
                await ForwardToWebView(evt);
            }
        }
    }

    private async Task<bool> IsClickOnWallpaper(Point pt)
    {
        // Check if clicking on desktop icon
        IntPtr hwnd = WindowFromPoint(pt);

        // Get window class
        var className = GetClassName(hwnd);

        // Icons are in SysListView32 window
        if (className.Contains("SysListView32"))
            return false;

        return true;
    }
}
```

### 3.3 WebView2 Cookie & Authentication Management

**Challenge:** WebView2 doesn't persist session cookies by default. Session cookies are deleted when all WebView2 instances close.

**Solution: Multi-Layer Approach**

#### Layer 1: Custom User Data Folder (Persistent Storage)
```csharp
var env = await CoreWebView2Environment.CreateAsync(
    userDataFolder: Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WebPaper", "WebView2Data"
    )
);

await _webView.EnsureCoreWebView2Async(env);
```

#### Layer 2: Session Cookie Persistence
```csharp
public class SessionCookieManager
{
    private CoreWebView2 _webView;
    private string _cookieStorePath;

    public async Task PersistSessionCookies()
    {
        var cookieManager = _webView.CookieManager;
        var cookies = await cookieManager.GetCookiesAsync("");

        var sessionCookies = cookies.Where(c =>
            c.IsSession ||
            c.Name.Contains("session", StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains("auth", StringComparison.OrdinalIgnoreCase)
        );

        var cookieData = sessionCookies.Select(c => new SerializableCookie
        {
            Name = c.Name,
            Value = c.Value,
            Domain = c.Domain,
            Path = c.Path,
            Expires = c.Expires,
            IsSecure = c.IsSecure,
            IsHttpOnly = c.IsHttpOnly,
            SameSite = c.SameSite
        });

        // Encrypt and store
        var json = JsonSerializer.Serialize(cookieData);
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json),
            null,
            DataProtectionScope.CurrentUser
        );

        await File.WriteAllBytesAsync(_cookieStorePath, encrypted);
    }

    public async Task RestoreSessionCookies()
    {
        if (!File.Exists(_cookieStorePath))
            return;

        var encrypted = await File.ReadAllBytesAsync(_cookieStorePath);
        var decrypted = ProtectedData.Unprotect(
            encrypted,
            null,
            DataProtectionScope.CurrentUser
        );
        var json = Encoding.UTF8.GetString(decrypted);

        var cookies = JsonSerializer.Deserialize<List<SerializableCookie>>(json);
        var cookieManager = _webView.CookieManager;

        foreach (var cookie in cookies)
        {
            var webCookie = cookieManager.CreateCookie(
                cookie.Name,
                cookie.Value,
                cookie.Domain,
                cookie.Path
            );

            webCookie.IsSecure = cookie.IsSecure;
            webCookie.IsHttpOnly = cookie.IsHttpOnly;
            webCookie.SameSite = cookie.SameSite;

            cookieManager.AddOrUpdateCookie(webCookie);
        }
    }
}
```

#### Layer 3: Browser Cookie Sync (Optional Feature)
```csharp
public class BrowserCookieSync
{
    // Import cookies from Edge/Chrome
    public async Task<IEnumerable<Cookie>> ImportFromEdge()
    {
        var edgeCookiePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\Edge\User Data\Default\Network\Cookies"
        );

        // Read SQLite database
        // Note: Browser must be closed or use temp copy
        using var connection = new SqliteConnection(
            $"Data Source={edgeCookiePath};Mode=ReadOnly"
        );

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT host_key, name, value, path, expires_utc,
                   is_secure, is_httponly, samesite
            FROM cookies
            WHERE host_key LIKE @domain
        ";
        command.Parameters.AddWithValue("@domain", "%example.com%");

        // Extract and return cookies
        // Implementation details...
    }
}
```

#### Layer 4: Manual Login Helper UI
```csharp
public class LoginHelper
{
    // Show full-size WebView2 window for login
    public async Task<bool> ShowLoginWindow(string loginUrl)
    {
        var loginWindow = new Window();
        var webView = new WebView2();

        loginWindow.Content = webView;
        await webView.EnsureCoreWebView2Async();

        webView.CoreWebView2.Navigate(loginUrl);

        // Monitor for successful login (redirect, cookie change, etc.)
        webView.CoreWebView2.NavigationCompleted += (s, e) =>
        {
            // Check if login successful
            // Copy cookies to main WebView2
        };

        loginWindow.ShowDialog();

        // Copy authenticated session to wallpaper WebView2
        await CopyCookies(webView, _wallpaperWebView);

        return true;
    }
}
```

### 3.4 Performance Optimizations

**Critical Requirements:**
- Pause rendering when fullscreen apps active
- Limit frame rate to conserve resources
- GPU acceleration for smooth rendering
- Memory management for long-running process

```csharp
public class PerformanceManager
{
    private Timer _fullscreenDetectionTimer;
    private bool _isPaused = false;

    public void Initialize()
    {
        // Check for fullscreen apps every 2 seconds
        _fullscreenDetectionTimer = new Timer(
            CheckFullscreenState, null, 0, 2000
        );

        // Enable WebView2 performance settings
        ConfigureWebViewPerformance();
    }

    private void CheckFullscreenState(object state)
    {
        IntPtr foreground = GetForegroundWindow();

        if (IsFullscreenWindow(foreground))
        {
            if (!_isPaused)
            {
                PauseWallpaper();
                _isPaused = true;
            }
        }
        else
        {
            if (_isPaused)
            {
                ResumeWallpaper();
                _isPaused = false;
            }
        }
    }

    private bool IsFullscreenWindow(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out RECT rect);

        var screen = Screen.FromHandle(hwnd);

        return rect.Width >= screen.Bounds.Width &&
               rect.Height >= screen.Bounds.Height;
    }

    private void ConfigureWebViewPerformance()
    {
        // Set environment variables before WebView2 creation
        var options = new CoreWebView2EnvironmentOptions();

        // Enable GPU acceleration
        options.AdditionalBrowserArguments =
            "--enable-gpu-rasterization " +
            "--enable-zero-copy " +
            "--enable-features=VaapiVideoDecoder " +
            "--disable-frame-rate-limit";

        // Apply options during environment creation
    }

    private void PauseWallpaper()
    {
        // Option 1: Hide window
        ShowWindow(_wallpaperHwnd, SW_HIDE);

        // Option 2: Suspend rendering
        // WebView2 doesn't have direct API for this
        // Best approach: navigate to about:blank

        // Option 3: Reduce resource usage
        _webView.CoreWebView2.ExecuteScriptAsync(@"
            document.querySelectorAll('video').forEach(v => v.pause());
            document.querySelectorAll('audio').forEach(a => a.pause());
        ");
    }
}
```

---

## 4. Development Roadmap

### Phase 1: Foundation & Proof of Concept (Weeks 1-3)

#### Week 1: Project Setup & Basic Window Management
**Goals:**
- Set up development environment
- Create WinUI 3 application structure
- Implement basic WorkerW technique

**Tasks:**
- [ ] Install Visual Studio 2022 with Windows App SDK
- [ ] Create WinUI 3 project structure
- [ ] Implement WorkerW window enumeration
- [ ] Create test window that renders behind desktop icons
- [ ] Test on Windows 11 24H2

**Deliverable:** Window successfully rendering behind desktop icons on Windows 11

#### Week 2: WebView2 Integration
**Goals:**
- Integrate WebView2
- Load and render webpage
- Basic window sizing/positioning

**Tasks:**
- [ ] Add WebView2 NuGet package
- [ ] Create WebView2Renderer class
- [ ] Implement webpage loading
- [ ] Handle navigation events
- [ ] Size WebView2 to screen dimensions
- [ ] Test with various websites (YouTube, Twitter, Gmail)

**Deliverable:** Static webpage rendering as wallpaper (no interaction yet)

#### Week 3: Basic Input Handling
**Goals:**
- Implement mouse hook
- Forward clicks to WebView2
- Basic interaction working

**Tasks:**
- [ ] Implement InputManager class
- [ ] Install low-level mouse hook
- [ ] Forward mouse events to WebView2
- [ ] Test with interactive website (e.g., click buttons)
- [ ] Handle desktop icon clicks properly

**Deliverable:** MVP with basic click interaction

**Milestone 1 Review:** If MVP works, proceed to Phase 2. If major issues, reassess approach.

---

### Phase 2: Full Interactivity & Cookie Management (Weeks 4-6)

#### Week 4: Advanced Input Handling
**Goals:**
- Add keyboard support
- Implement scroll handling
- Focus management

**Tasks:**
- [ ] Install keyboard hook
- [ ] Forward keyboard events
- [ ] Implement mouse wheel scrolling
- [ ] Handle focus (wallpaper vs desktop icons)
- [ ] Test with form inputs, text fields
- [ ] Multi-monitor input handling

**Deliverable:** Full mouse and keyboard interaction

#### Week 5: Cookie & Authentication System
**Goals:**
- Implement cookie persistence
- Create login helper UI
- Test with authenticated websites

**Tasks:**
- [ ] Implement SessionCookieManager
- [ ] Create encrypted cookie storage
- [ ] Implement login helper window
- [ ] Test with Gmail, Twitter, Reddit
- [ ] Implement browser cookie import (optional)
- [ ] Add cookie sync mechanism

**Deliverable:** Persistent authentication across app restarts

#### Week 6: Performance & Stability
**Goals:**
- Optimize resource usage
- Implement pause on fullscreen
- Memory leak testing

**Tasks:**
- [ ] Implement PerformanceManager
- [ ] Add fullscreen detection
- [ ] Optimize frame rate
- [ ] Memory profiling and leak detection
- [ ] CPU usage optimization
- [ ] Battery impact testing on laptop

**Deliverable:** Stable, performant wallpaper with <5% CPU usage when idle

**Milestone 2 Review:** Feature-complete core functionality

---

### Phase 3: User Interface & User Experience (Weeks 7-8)

#### Week 7: System Tray & Configuration UI
**Goals:**
- Create system tray interface
- Build settings window
- URL management

**Tasks:**
- [ ] Implement system tray icon
- [ ] Add context menu (enable/disable, settings, exit)
- [ ] Create settings window (WinUI 3)
- [ ] URL input and validation
- [ ] Wallpaper profiles (save multiple URLs)
- [ ] Auto-start on Windows login
- [ ] Quick preview before applying

**Deliverable:** User-friendly configuration interface

#### Week 8: Polish & Error Handling
**Goals:**
- Improve error messages
- Handle edge cases
- Add logging

**Tasks:**
- [ ] Implement comprehensive error handling
- [ ] Add logging system (Serilog or NLog)
- [ ] Handle network errors gracefully
- [ ] Handle webpage crashes
- [ ] Display user-friendly error messages
- [ ] Add "What's New" first-run experience
- [ ] Tooltips and help text

**Deliverable:** Polished, user-ready application

**Milestone 3 Review:** Ready for beta testing

---

### Phase 4: Testing & Distribution (Weeks 9-10)

#### Week 9: Comprehensive Testing
**Goals:**
- Test on various Windows versions
- Multi-monitor testing
- Edge case handling

**Tasks:**
- [ ] Test on Windows 10 21H2, 22H2
- [ ] Test on Windows 11 all versions including 24H2
- [ ] Multi-monitor configuration testing
- [ ] Different DPI scaling (100%, 125%, 150%, 200%)
- [ ] Virtual desktop testing
- [ ] Tablet mode testing
- [ ] Performance testing on low-end hardware

**Deliverable:** Comprehensive test report

#### Week 10: Distribution & Documentation
**Goals:**
- Create installer
- Write documentation
- Prepare for release

**Tasks:**
- [ ] Create MSIX package
- [ ] Set up Microsoft Store listing (optional)
- [ ] Write user documentation
- [ ] Create quick start guide
- [ ] Record demo video
- [ ] Set up GitHub repository
- [ ] Create README and contribution guidelines
- [ ] Plan v1.1 features based on feedback

**Deliverable:** Version 1.0 ready for public release

---

## 5. Detailed Cookie & Auth Strategy

### User Workflow Options

#### Option A: Direct Login (Recommended for MVP)
1. User enters URL in settings
2. Application shows full-size WebView2 login window
3. User logs in normally
4. Cookies captured and persisted
5. Wallpaper WebView2 uses same cookies
6. Cookies auto-refreshed before expiration

**Pros:** Simple, reliable, no browser dependency
**Cons:** Users must log in separately

#### Option B: Browser Cookie Import
1. User selects "Import from Edge/Chrome"
2. Application detects browser installation
3. Creates temporary copy of cookie database
4. Imports relevant cookies for specified domain
5. Applies to wallpaper WebView2

**Pros:** No separate login, uses existing auth
**Cons:** Requires browser to be closed, complex implementation

#### Option C: Browser Profile Sharing
1. Configure WebView2 to use Edge's user data folder
2. Share cookies automatically
3. Requires special permissions

**Pros:** Always in sync with browser
**Cons:** Risky, may corrupt browser data, not recommended

**Recommendation for v1.0:** Implement Option A (direct login), add Option B in v1.1

### Cookie Storage Security

```csharp
public class SecureCookieStore
{
    private readonly string _storePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WebPaper", "SecureStore"
    );

    public async Task SaveCookies(IEnumerable<Cookie> cookies)
    {
        var json = JsonSerializer.Serialize(cookies);
        var bytes = Encoding.UTF8.GetBytes(json);

        // Encrypt using Windows DPAPI (user-specific)
        var encrypted = ProtectedData.Protect(
            bytes,
            GetEntropy(),
            DataProtectionScope.CurrentUser
        );

        await File.WriteAllBytesAsync(_storePath, encrypted);
    }

    private byte[] GetEntropy()
    {
        // Additional entropy for encryption
        // Could use machine-specific data
        var machineId = Environment.MachineName;
        var userId = Environment.UserName;
        return SHA256.HashData(
            Encoding.UTF8.GetBytes($"{machineId}:{userId}")
        );
    }
}
```

---

## 6. Risk Mitigation & Contingency Plans

### Risk 1: WorkerW Technique Breaks in Future Windows Update
**Likelihood:** Medium
**Impact:** Critical

**Mitigation:**
- Implement monitoring for Windows updates
- Maintain fallback to alternative rendering method
- Monitor Lively Wallpaper for their solutions
- Consider alternative: Inject into explorer.exe (risky, not recommended)

**Contingency:**
- Research alternative desktop integration methods
- Consider notification to users about compatibility
- Rapid patch release when Windows update detected

### Risk 2: Low-Level Hooks Timeout or Removed by Windows
**Likelihood:** Medium
**Impact:** High

**Mitigation:**
- Fast hook processing (<200ms)
- Dedicated thread with message pump
- Implement fallback to Raw Input API
- Monitor hook health

**Contingency:**
```csharp
public class InputManagerWithFallback
{
    private bool _hooksWorking = true;
    private DateTime _lastHookCall;

    private void MonitorHookHealth()
    {
        if ((DateTime.Now - _lastHookCall).TotalSeconds > 10)
        {
            // Hooks may have been removed
            _hooksWorking = false;
            FallbackToRawInput();
        }
    }

    private void FallbackToRawInput()
    {
        // Register for raw input
        RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];

        // Mouse
        rid[0].usUsagePage = 0x01;
        rid[0].usUsage = 0x02;
        rid[0].dwFlags = RIDEV_INPUTSINK;
        rid[0].hwndTarget = _wallpaperHwnd;

        // Keyboard
        rid[1].usUsagePage = 0x01;
        rid[1].usUsage = 0x06;
        rid[1].dwFlags = RIDEV_INPUTSINK;
        rid[1].hwndTarget = _wallpaperHwnd;

        RegisterRawInputDevices(rid, 2, Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
    }
}
```

### Risk 3: WebView2 Not Installed on User System
**Likelihood:** Low (Windows 11 includes it)
**Impact:** Medium

**Mitigation:**
- Check for WebView2 runtime on startup
- Include WebView2 runtime in installer (evergreen bootstrapper)
- Show clear error message with download link

```csharp
public static async Task<bool> EnsureWebView2Runtime()
{
    try
    {
        var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
        return true;
    }
    catch
    {
        var result = MessageBox.Show(
            "WebView2 Runtime is required but not installed. Download now?",
            "WebPaper Setup",
            MessageBoxButton.YesNo
        );

        if (result == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                UseShellExecute = true
            });
        }

        return false;
    }
}
```

### Risk 4: High Resource Usage / Battery Drain
**Likelihood:** Medium
**Impact:** Medium

**Mitigation:**
- Aggressive pause on fullscreen
- Frame rate limiting
- Battery mode detection
- User-configurable performance settings

```csharp
public class PowerManager
{
    public void ConfigureForPowerMode()
    {
        var powerStatus = SystemInformation.PowerStatus;

        if (powerStatus.PowerLineStatus == PowerLineStatus.Offline)
        {
            // On battery - reduce performance
            SetFrameRate(15); // Lower FPS
            EnableAggressivePause();
            DisableAnimations();
        }
        else
        {
            // Plugged in - normal performance
            SetFrameRate(30);
            EnableNormalPause();
        }
    }
}
```

---

## 7. Feature Roadmap (Post-MVP)

### Version 1.1 (4 weeks after v1.0)
- [ ] Browser cookie import (Edge, Chrome)
- [ ] Multiple wallpaper profiles with quick switching
- [ ] Per-monitor different URLs
- [ ] Scheduled URL changes (time-based)
- [ ] Hotkey support (global keyboard shortcuts)

### Version 1.2 (2-3 months after v1.0)
- [ ] Wallpaper opacity control
- [ ] Custom CSS injection for websites
- [ ] JavaScript injection for customization
- [ ] Auto-pause on battery percentage threshold
- [ ] Zoom in/out support
- [ ] Dark mode enforcement for websites

### Version 2.0 (6 months after v1.0)
- [ ] macOS support (separate implementation)
- [ ] Cloud sync for settings & profiles
- [ ] Community wallpaper library
- [ ] Plugin system for extensions
- [ ] Advanced performance analytics

---

## 8. Success Metrics

### Technical Metrics
- **CPU Usage:** <5% when idle, <15% during interaction
- **Memory Usage:** <300MB RAM for single monitor
- **Startup Time:** <3 seconds to full wallpaper render
- **Crash Rate:** <0.1% (less than 1 crash per 1000 sessions)

### User Experience Metrics
- **Setup Time:** <2 minutes from install to first wallpaper
- **Input Latency:** <50ms click-to-response
- **Compatibility:** Works on 95%+ of Windows 11 systems

### Business Metrics (if applicable)
- **GitHub Stars:** Target 1000+ in first 3 months
- **Active Users:** Target 10,000+ in first 6 months
- **Microsoft Store Rating:** Target 4.5+ stars

---

## 9. Resources & Learning Materials

### Essential Reading
1. **Windows App SDK Documentation**
   https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/

2. **WebView2 Documentation**
   https://learn.microsoft.com/en-us/microsoft-edge/webview2/

3. **Windows Hooks Deep Dive**
   https://learn.microsoft.com/en-us/windows/win32/winmsg/hooks

4. **Lively Wallpaper Source Code** (study, don't fork)
   https://github.com/rocksdanister/lively

### Recommended Tools
- **Visual Studio 2022** (Community Edition is free)
- **Windows App SDK** (latest stable)
- **Spy++** (Windows inspection tool)
- **Process Explorer** (resource monitoring)
- **WinDbg** (debugging)

### Community Resources
- Lively Wallpaper Discord (for technical discussions)
- r/windows11 subreddit
- Stack Overflow (winui3, webview2 tags)

---

## 10. Next Immediate Steps (This Week)

### Day 1-2: Environment Setup
1. Install Visual Studio 2022 with WinUI 3 workload
2. Create new WinUI 3 project
3. Set up Git repository
4. Review Lively Wallpaper source code

### Day 3-4: WorkerW Proof of Concept
1. Implement WorkerW window enumeration
2. Create transparent window
3. Parent to WorkerW
4. Test on Windows 11 24H2
5. Document any issues encountered

### Day 5-7: WebView2 Integration
1. Add WebView2 to project
2. Load test webpage
3. Combine with WorkerW window
4. Test rendering quality
5. Measure performance baseline

**Week 1 Deliverable:** Working demo showing webpage behind desktop icons (even if not interactive yet)

---

## 11. Conclusion & Recommendation

**Project WebPaper is highly feasible** with a clear path to implementation. The technology stack is mature, documented, and battle-tested by Lively Wallpaper and similar applications.

**Recommended Approach:**
1. ✅ Build from scratch for learning and full control
2. ✅ Use WebView2 for modern, lightweight web rendering
3. ✅ Focus on Windows 11 first, Windows 10 as secondary
4. ✅ Start with direct login, add browser import later
5. ✅ 10-week timeline to production-ready v1.0

**Key Success Factors:**
- Early testing on Windows 11 24H2 (WorkerW compatibility)
- Robust input handling with fallback mechanisms
- Secure, reliable cookie persistence
- Performance optimization from day one
- Clean, maintainable codebase for future enhancements

**Ready to start coding?** Begin with Phase 1, Week 1 tasks. The foundation you build in the first 3 weeks will determine the quality of the entire project.

---

**Document Version:** 1.0
**Last Updated:** November 8, 2025
**Next Review:** After Phase 1 completion
