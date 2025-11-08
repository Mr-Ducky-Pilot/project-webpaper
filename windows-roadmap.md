# Interactive Webpage Wallpaper - Windows Roadmap

## Executive Summary

**Feasibility:** ✅ **Highly Feasible** - Windows has the best support for this concept with existing successful implementations.

**Existing Solution to Build Upon:** [Lively Wallpaper](https://github.com/rocksdanister/lively) - Open source, actively maintained, WinUI 3-based.

## 1. Technical Overview

### Core Concept
Create a window that sits between the desktop wallpaper layer and desktop icons, rendering interactive web content using a browser engine.

### Key Technical Requirements
1. **Window Management**: Position window behind desktop icons using WorkerW window
2. **Web Rendering**: Embed Chromium-based browser engine (CEF or similar)
3. **Input Handling**: Capture and forward mouse/keyboard events using low-level hooks
4. **Performance**: GPU acceleration, pause on fullscreen apps

## 2. Existing Open Source Solutions

### Primary Option: Lively Wallpaper
- **Repository**: https://github.com/rocksdanister/lively
- **Tech Stack**: C#, WinUI 3, CEF (Chromium Embedded Framework)
- **Features**:
  - Already supports webpage wallpapers
  - Hardware-accelerated rendering
  - Multi-monitor support
  - Auto-pause on fullscreen apps
  - Open source (GPL-v3)
  
**Recommendation**: Fork Lively Wallpaper and enhance the web wallpaper features rather than building from scratch.

### Alternative: Build from Scratch
If you want a lightweight, focused solution specifically for web content.

## 3. Technical Implementation Details

### 3.1 Window Positioning (Behind Desktop Icons)

**The WorkerW Window Technique:**

```csharp
// Step 1: Find the Progman window
IntPtr progman = FindWindow("Progman", null);

// Step 2: Send message 0x052C to spawn WorkerW
SendMessageTimeout(progman, 0x052C, 
    IntPtr.Zero, IntPtr.Zero, 
    SendMessageTimeoutFlags.SMTO_NORMAL, 
    1000, out IntPtr result);

// Step 3: Find the WorkerW window that will host our window
IntPtr workerw = IntPtr.Zero;
EnumWindows((tophandle, topparamhandle) => {
    IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, 
                           "SHELLDLL_DefView", IntPtr.Zero);
    if (p != IntPtr.Zero) {
        // Get the WorkerW window after SHELLDLL_DefView
        workerw = FindWindowEx(IntPtr.Zero, tophandle, 
                              "WorkerW", IntPtr.Zero);
    }
    return true;
}, IntPtr.Zero);

// Step 4: Set your window as child of WorkerW
SetParent(yourWindowHandle, workerw);
```

**Key Points:**
- Works on Windows 8+ (but Windows 11 24H2 has changes - needs testing)
- Desktop icons remain clickable
- Your window renders behind icons
- Undocumented API (may break in future Windows updates)

### 3.2 Web Rendering Engine Options

#### Option A: CEF (Chromium Embedded Framework) - RECOMMENDED
**Pros:**
- Full Chrome compatibility
- Hardware acceleration
- Offscreen rendering support
- Battle-tested (used by Steam, Spotify, Discord)

**Cons:**
- Large binary size (~100-150 MB)
- Complex setup
- Requires native C++ integration

**Integration:**
- Use CefSharp for C# (.NET wrapper for CEF)
- Repository: https://github.com/cefsharp/CefSharp
- NuGet packages available

```csharp
// Example CefSharp setup for offscreen rendering
var settings = new CefSettings();
settings.WindowlessRenderingEnabled = true;
Cef.Initialize(settings);

var browser = new ChromiumWebBrowser("https://example.com");
browser.CreateOffscreenBrowser(IntPtr.Zero);
```

#### Option B: WebView2 (Microsoft Edge WebView2)
**Pros:**
- Native Windows component
- Smaller distribution size
- Official Microsoft support
- Automatic updates through Windows

**Cons:**
- Requires Windows 10 1803+
- Less control than CEF
- Newer, less mature APIs

**Integration:**
```csharp
// WebView2 setup
var webView = new WebView2();
await webView.EnsureCoreWebView2Async();
webView.Source = new Uri("https://example.com");
```

#### Option C: Electron (Not Recommended for this use case)
- Too heavy for wallpaper application
- Designed for standalone apps, not embedded usage

### 3.3 Input Handling (CRITICAL CHALLENGE)

Windows at desktop level don't naturally receive input. You need low-level hooks.

**Solution: Global Windows Hooks**

```csharp
// Install low-level mouse hook
private static IntPtr mouseHookID = IntPtr.Zero;

public static void InstallMouseHook() {
    using (Process curProcess = Process.GetCurrentProcess())
    using (ProcessModule curModule = curProcess.MainModule) {
        mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, 
            MouseHookCallback, 
            GetModuleHandle(curModule.ModuleName), 0);
    }
}

private static IntPtr MouseHookCallback(int nCode, 
                                       IntPtr wParam, 
                                       IntPtr lParam) {
    if (nCode >= 0) {
        MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal
            .PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
        
        // Check if click is on wallpaper area (not on icons)
        if (IsClickOnWallpaper(hookStruct.pt)) {
            // Forward to web view
            ForwardMouseEventToBrowser(wParam, hookStruct);
        }
    }
    return CallNextHookEx(mouseHookID, nCode, wParam, lParam);
}
```

**Keyboard Hooks:**
```csharp
// Similar approach for keyboard (WH_KEYBOARD_LL)
private static IntPtr keyboardHookID = IntPtr.Zero;

public static void InstallKeyboardHook() {
    using (Process curProcess = Process.GetCurrentProcess())
    using (ProcessModule curModule = curProcess.MainModule) {
        keyboardHookID = SetWindowsHookEx(WH_KEYBOARD_LL, 
            KeyboardHookCallback, 
            GetModuleHandle(curModule.ModuleName), 0);
    }
}
```

**Icon Detection:**
- Use `WindowFromPoint()` to check if click is on desktop icon
- If icon found, don't consume event
- If no icon, forward to web browser

### 3.4 Performance Optimizations

```csharp
// 1. Pause on fullscreen apps
private void DetectFullscreenApp() {
    IntPtr foregroundWindow = GetForegroundWindow();
    GetWindowRect(foregroundWindow, out RECT rect);
    
    if (IsFullscreen(rect)) {
        PauseWallpaper();
    } else {
        ResumeWallpaper();
    }
}

// 2. GPU Acceleration (CEF)
var settings = new CefSettings {
    CefCommandLineArgs.Add("enable-gpu", "1");
    CefCommandLineArgs.Add("enable-gpu-compositing", "1");
};

// 3. Frame rate limiting
CefCommandLineArgs.Add("disable-frame-rate-limit", "0");
CefCommandLineArgs.Add("max-fps", "30"); // Conserve resources
```

## 4. Detailed Development Roadmap

### Phase 1: Foundation (2-3 weeks)

**Week 1: Project Setup & Research**
- [ ] Set up development environment (Visual Studio 2022)
- [ ] Install WinUI 3 SDK
- [ ] Study Lively Wallpaper source code
- [ ] Test WorkerW window technique
- [ ] Create proof-of-concept: window behind desktop icons

**Week 2: Web Rendering Integration**
- [ ] Choose web engine (CEF vs WebView2)
- [ ] Integrate CefSharp or WebView2
- [ ] Create basic web browser window
- [ ] Test offscreen rendering
- [ ] Implement proper window sizing/positioning

**Week 3: Basic Input Handling**
- [ ] Implement low-level mouse hooks
- [ ] Test mouse click forwarding
- [ ] Implement keyboard hooks
- [ ] Test with simple interactive webpage

**Deliverable:** Basic working prototype that renders a webpage behind desktop icons with limited interactivity.

### Phase 2: Core Features (3-4 weeks)

**Week 4-5: Advanced Input Management**
- [ ] Implement desktop icon detection
- [ ] Handle multi-monitor scenarios
- [ ] Add scroll event handling
- [ ] Implement focus management
- [ ] Test with complex web applications

**Week 6: Performance & Stability**
- [ ] Add GPU acceleration
- [ ] Implement fullscreen app detection
- [ ] Add performance monitoring
- [ ] Memory leak testing
- [ ] CPU usage optimization

**Week 7: User Interface**
- [ ] Create system tray icon
- [ ] Add configuration window
- [ ] URL input and management
- [ ] Wallpaper preview
- [ ] Enable/disable toggle

**Deliverable:** Functional application with solid performance and user controls.

### Phase 3: Polish & Distribution (2-3 weeks)

**Week 8: Testing & Bug Fixes**
- [ ] Test on Windows 10 (various versions)
- [ ] Test on Windows 11 (including 24H2)
- [ ] Multi-monitor testing
- [ ] Different DPI scaling tests
- [ ] Edge case handling

**Week 9: Documentation & Distribution**
- [ ] User documentation
- [ ] Developer documentation
- [ ] Create installer
- [ ] Set up GitHub repository
- [ ] Create Microsoft Store package (optional)

**Week 10: Community & Maintenance**
- [ ] Beta testing with users
- [ ] Gather feedback
- [ ] Fix reported issues
- [ ] Plan future features

## 5. Technology Stack Recommendation

### Recommended Stack:
```
Language: C#
Framework: .NET 8 + WinUI 3
Web Engine: CefSharp (CEF3)
UI Framework: WinUI 3
Installer: WiX Toolset or MSIX
Package Manager: NuGet
```

### Alternative Lightweight Stack:
```
Language: C++
Framework: Win32 API + WinRT
Web Engine: WebView2
Build System: CMake
Installer: NSIS
```

## 6. Key Limitations & Challenges

### Technical Challenges:

1. **Windows 11 24H2 Changes**
   - WorkerW behavior changed in latest Windows 11
   - Window only spawns during wallpaper change
   - Need alternative approach or workaround
   - **Solution:** Monitor for changes, maintain window state

2. **Input Handling Complexity**
   - Distinguishing wallpaper clicks from icon clicks
   - Performance overhead of global hooks
   - Potential conflicts with other software using hooks
   - **Mitigation:** Efficient hit-testing, selective hook activation

3. **Performance**
   - Web rendering is resource-intensive
   - Background rendering still uses GPU
   - Battery drain on laptops
   - **Mitigation:** Adaptive frame rates, pause when idle

4. **Security Considerations**
   - Running web content with system-level hooks
   - Potential for malicious websites
   - **Mitigation:** Sandbox web content, URL validation

5. **Compatibility**
   - Different behavior across Windows versions
   - Third-party shell replacements
   - Virtual desktop software interference
   - **Testing:** Extensive testing on various configurations

### Known Limitations:

- **Cannot directly replace static wallpaper image** (sits on top of it)
- **May conflict with other desktop customization software**
- **Undocumented APIs may break** in future Windows updates
- **Higher resource usage** than static wallpapers
- **Security software may flag** low-level hooks

## 7. Recommended Architecture

```
┌─────────────────────────────────────────┐
│         Main Application (WinUI 3)      │
│  - System Tray                          │
│  - Settings UI                          │
│  - Lifecycle Management                 │
└─────────────────┬───────────────────────┘
                  │
    ┌─────────────┴─────────────┐
    │                           │
┌───▼──────────────┐  ┌────────▼─────────────┐
│ Window Manager   │  │  Input Manager       │
│ - WorkerW setup  │  │  - Mouse hooks       │
│ - Positioning    │  │  - Keyboard hooks    │
│ - Multi-monitor  │  │  - Icon detection    │
└───┬──────────────┘  └─────────┬────────────┘
    │                           │
    └─────────────┬─────────────┘
                  │
         ┌────────▼──────────┐
         │   Web Renderer    │
         │   (CefSharp/      │
         │    WebView2)      │
         │  - Page rendering │
         │  - Event handling │
         │  - JS execution   │
         └───────────────────┘
```

## 8. Build Upon Lively vs Build from Scratch

### Fork Lively Wallpaper ✅ RECOMMENDED

**Pros:**
- Already has 90% of infrastructure
- Battle-tested with real users
- Active community
- Multi-wallpaper type support
- Professional UI

**Cons:**
- Larger codebase to understand
- Must maintain GPL-v3 license
- May have features you don't need

**Approach:**
1. Fork Lively repository
2. Study web wallpaper implementation
3. Enhance web interaction features
4. Submit improvements as pull requests
5. Maintain your enhanced fork

### Build from Scratch

**Pros:**
- Complete control
- Minimal codebase
- Learn everything deeply
- Choose your own license

**Cons:**
- Reinventing the wheel
- 3-4 months development time
- Need to solve known problems
- Less testing/validation

**Approach:**
1. Study Lively's approach
2. Start with minimal viable product
3. Iteratively add features
4. Focus on web-specific optimizations

## 9. Estimated Timeline & Effort

### Fork & Enhance Lively:
- **Time:** 4-6 weeks
- **Effort:** Part-time (10-15 hours/week)
- **Difficulty:** Medium

### Build from Scratch (Full-Featured):
- **Time:** 12-16 weeks
- **Effort:** Full-time or serious part-time
- **Difficulty:** High

### Minimal Viable Product (Scratch):
- **Time:** 6-8 weeks
- **Effort:** Part-time (15-20 hours/week)
- **Difficulty:** Medium-High

## 10. Resources & References

### Essential Documentation:
- [CefSharp Documentation](https://github.com/cefsharp/CefSharp/wiki)
- [WebView2 Documentation](https://learn.microsoft.com/en-us/microsoft-edge/webview2/)
- [Windows Hooks Documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/hooks)
- [WinUI 3 Documentation](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)

### Community Resources:
- Lively Wallpaper Discord
- r/wallpaperengine subreddit
- Windows Desktop Development forums

### Similar Projects:
- Lively Wallpaper: https://github.com/rocksdanister/lively
- Wallpaper Engine: Commercial, closed source
- RainWallpaper: Chinese app, similar concept

## 11. Monetization Options (if desired)

1. **Open Source + Donations**
   - GitHub Sponsors
   - Ko-fi/Buy Me a Coffee
   - Patreon for supporters

2. **Freemium Model**
   - Basic features free
   - Premium features (themes, presets) paid

3. **Microsoft Store**
   - One-time purchase
   - Reach wider audience
   - Automatic updates

## 12. Next Steps

**Immediate Actions:**
1. ✅ Clone Lively Wallpaper repository
2. ✅ Set up development environment
3. ✅ Study web wallpaper implementation
4. ✅ Create simple proof-of-concept
5. ✅ Test on your system

**This Week:**
- Experiment with WorkerW technique
- Test different websites as wallpaper
- Identify specific enhancements needed
- Decide: Fork Lively or build from scratch

**This Month:**
- Complete Phase 1 development
- Create working prototype
- Test with beta users
- Gather feedback

---

## Conclusion

**Building an interactive webpage wallpaper for Windows is highly feasible** and there's already excellent open source foundation to build upon. The WorkerW technique is proven, CEF provides robust web rendering, and Lively Wallpaper demonstrates this is production-ready technology.

**Recommended Path:** Fork Lively Wallpaper and enhance its web wallpaper capabilities with improved interactivity features. This gives you a 4-6 week timeline to a polished product rather than 3-4 months building from scratch.

The main challenges are input handling complexity and maintaining compatibility across Windows versions, but both are solvable with the techniques outlined in this roadmap.
