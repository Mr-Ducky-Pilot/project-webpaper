# Project WebPaper - Key Technical Decisions

**Last Updated:** November 8, 2025

This document summarizes all major technical decisions with rationale.

---

## 1. Build Approach: From Scratch vs Fork Lively

**Decision:** ✅ Build from scratch

**Options Considered:**
- **Option A:** Fork Lively Wallpaper (existing open-source solution)
- **Option B:** Build from scratch, studying Lively's approach

**Chosen:** Option B - Build from scratch

**Rationale:**
| Factor | Fork Lively | Build from Scratch |
|--------|-------------|-------------------|
| Time to MVP | 4-6 weeks | 8-10 weeks |
| Codebase Size | Large (supports videos, games, shaders) | Minimal (web-only) |
| Learning | Limited | Deep understanding |
| Control | Limited | Full control |
| Maintenance | Must track upstream | Independent |
| License | GPL-v3 (restrictive) | Our choice |
| Focus | General wallpapers | Web-specific optimization |

**Trade-off Accepted:** Longer initial development (extra 4 weeks) for:
- Cleaner, more maintainable codebase
- Specific optimization for web content
- Better authentication/cookie handling
- Full architectural control

---

## 2. Web Rendering Engine: WebView2 vs CefSharp

**Decision:** ✅ WebView2

**Options Compared:**

| Criterion | WebView2 | CefSharp |
|-----------|----------|----------|
| Distribution Size | ~5MB | ~100-150MB |
| Integration | Native Windows | C++ wrapper |
| Updates | Automatic (OS) | Manual updates |
| Support | Official Microsoft | Community |
| Performance | Equivalent | Equivalent |
| Sandboxing | Yes (separate process) | No (in-process) |
| Offscreen Rendering | No | Yes |
| Future | Actively developed | Maintenance mode |
| Windows Only | Yes | Yes (same limitation) |

**Chosen:** WebView2

**Rationale:**
1. **Size:** 95% smaller distribution package
2. **Maintenance:** No need to ship or update Chromium runtime
3. **Security:** Sandboxed, separate process (crashes don't kill app)
4. **Support:** Official Microsoft backing, better docs
5. **Future-proof:** Microsoft's strategic web embedding solution

**Trade-off Accepted:** No offscreen rendering (not needed for our use case)

**Cookie Persistence Workaround:**
```csharp
// WebView2 doesn't persist session cookies by default
// Solution: Manual persistence using CoreWebView2.CookieManager
await SaveSessionCookies();  // On app close
await RestoreSessionCookies();  // On app start
```

---

## 3. Programming Language: C# vs C++

**Decision:** ✅ C# with .NET 8

**Rationale:**
- **Productivity:** Faster development, less boilerplate
- **Safety:** Memory-safe, no manual memory management
- **Ecosystem:** Rich NuGet packages for everything
- **WinUI 3:** First-class C# support
- **WebView2:** Official C# bindings
- **Async/Await:** Natural for web operations
- **Debugging:** Excellent tooling in Visual Studio

**Performance Impact:** Negligible (UI is bottleneck, not language)

---

## 4. UI Framework: WinUI 3 vs WPF vs WinForms

**Decision:** ✅ WinUI 3

**Options:**

| Framework | Pros | Cons |
|-----------|------|------|
| **WinUI 3** | Modern, Windows 11 design, future | Newer, fewer resources |
| **WPF** | Mature, lots of examples | Older look, legacy |
| **WinForms** | Simple, lightweight | Outdated, limited styling |

**Chosen:** WinUI 3

**Rationale:**
1. **Modern UI:** Matches Windows 11 aesthetic
2. **Future-proof:** Microsoft's recommended framework for new apps
3. **Performance:** Better hardware acceleration
4. **WebView2:** Native integration
5. **MSIX:** Easy Microsoft Store distribution

**Learning Curve Accepted:** Less Stack Overflow answers, more docs reading required

---

## 5. Authentication Strategy

**Decision:** ✅ Direct Login (v1.0), Browser Import (v1.1)

**Options for User Authentication:**

**A. Direct Login** (Chosen for v1.0)
```csharp
// Show full-size WebView2 login window
var loginWindow = new Window { Content = webView };
loginWindow.ShowDialog();
// Capture and persist cookies
await SaveCookies(webView.CoreWebView2.CookieManager);
```

**Pros:**
- Simple, reliable implementation
- No browser dependencies
- Full control over process
- Works offline once authenticated

**Cons:**
- Users must log in separately
- Not synced with browser

**B. Browser Cookie Import** (Planned for v1.1)
```csharp
// Import from Edge/Chrome cookie database
var edgeCookies = ImportFromEdge();
await ApplyToWebView(edgeCookies);
```

**Pros:**
- Seamless, uses existing auth
- No separate login

**Cons:**
- Requires browser to be closed
- Complex implementation
- Database schema changes
- Security implications

**C. Profile Sharing** (Rejected)
```csharp
// Share Edge's user data folder - RISKY!
```

**Rejected because:**
- Risk of corrupting browser data
- File locking issues
- Not recommended by Microsoft

**Decision:** Start simple (A), add advanced (B) later based on user feedback.

---

## 6. Cookie Storage Security

**Decision:** ✅ Windows DPAPI Encryption

**Implementation:**
```csharp
// Encrypt cookies using Windows Data Protection API
var encrypted = ProtectedData.Protect(
    cookieBytes,
    entropy: GetMachineEntropy(),
    scope: DataProtectionScope.CurrentUser
);
```

**Why DPAPI:**
- Built into Windows (no external dependencies)
- User-specific encryption (secure per-user)
- Automatic key management
- Cannot be decrypted by other users
- Industry standard for local secrets

**Alternatives Rejected:**
- Plain JSON: Insecure
- Custom encryption: Reinventing the wheel, key management complexity
- Credential Manager API: Overkill for cookies

---

## 7. Input Handling: Hooks vs Raw Input

**Decision:** ✅ Low-Level Hooks with Raw Input Fallback

**Primary: Low-Level Windows Hooks**
```csharp
SetWindowsHookEx(WH_MOUSE_LL, MouseHookCallback, ...);
SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardHookCallback, ...);
```

**Pros:**
- Capture all input system-wide
- Can filter before processing
- Detect desktop icon clicks

**Cons:**
- Can be removed by Windows if timeout
- Performance sensitive (<200ms processing)
- Flagged by some antivirus

**Fallback: Raw Input API**
```csharp
RegisterRawInputDevices(devices, ...);
```

**Why Both:**
- Hooks for rich interaction (primary)
- Raw Input as safety net if hooks fail
- Automatic failover on hook timeout

**Microsoft Recommendation:** Prefer Raw Input
**Our Decision:** Try hooks first (better UX), fallback to Raw Input

---

## 8. Performance Strategy

**Decision:** ✅ Aggressive Pause on Fullscreen

**Implementation:**
```csharp
// Detect fullscreen app every 2 seconds
if (IsFullscreenApp(foregroundWindow))
{
    webView.Visibility = Visibility.Collapsed;  // Pause rendering
    // Result: ~0% CPU/GPU usage
}
```

**Additional Optimizations:**
1. **Frame Rate Limit:** 30 FPS max (vs unlimited)
2. **Battery Mode:** Reduce to 15 FPS when unplugged
3. **GPU Acceleration:** Enabled by default
4. **Memory Management:** Periodic JavaScript GC trigger

**Target Metrics:**
- Idle: <5% CPU, <300MB RAM
- Active: <15% CPU, <500MB RAM
- Fullscreen app running: ~0% CPU/GPU

---

## 9. Multi-Monitor Support

**Decision:** ✅ One Wallpaper Window Per Monitor (v1.0)

**Approach:**
```csharp
foreach (var display in GetAllDisplays())
{
    var wallpaper = new WallpaperWindow(display);
    wallpaper.LoadUrl(GetUrlForDisplay(display));
}
```

**v1.0:** Same URL on all monitors
**v1.1:** Different URL per monitor

**Why Not Single Window Spanning:**
- Complex DPI handling across monitors
- Different refresh rates
- Independent pause/resume per display
- Easier to manage

---

## 10. Distribution Strategy

**Decision:** ✅ MSIX (Microsoft Store) + Sideload Option

**Primary: MSIX Package**
```xml
<Package>
  <Identity Publisher="CN=WebPaper" />
  <Applications>
    <Application Id="WebPaper" ... />
  </Applications>
</Package>
```

**Benefits:**
- Microsoft Store distribution
- Automatic updates
- Sandboxed installation
- Easy uninstall
- Digital signatures

**Secondary: Sideload MSIX**
- For users who prefer not to use Store
- Same package, manual install

**Not Chosen:**
- Traditional installer (WiX): Harder updates
- ZIP download: No auto-update
- ClickOnce: Legacy technology

---

## 11. Windows Version Support

**Decision:** ✅ Windows 11 First, Windows 10 Second

**Rationale:**

**Windows 11 Priority:**
- Target demographic (modern users)
- Latest features and APIs
- Better WebView2 integration
- Longer support lifecycle

**Windows 10 Compatibility (Best Effort):**
- Will test on Windows 10 21H2+
- May have degraded experience
- No dedicated optimization

**Not Supporting:**
- Windows 8.1 and earlier (WebView2 not available)
- Windows 10 pre-21H2 (too old)

**Testing Matrix:**
| Version | Priority | Target Support |
|---------|----------|----------------|
| Windows 11 24H2 | High | Full support |
| Windows 11 23H2 | High | Full support |
| Windows 11 22H2 | Medium | Full support |
| Windows 10 22H2 | Low | Best effort |
| Windows 10 21H2 | Low | Best effort |

---

## 12. Error Handling Philosophy

**Decision:** ✅ Graceful Degradation, Never Crash

**Principles:**
1. **Fail Silently (UX):** Don't show technical errors to users
2. **Log Verbosely (Dev):** Capture everything for debugging
3. **Fallback Always:** Have Plan B for critical features
4. **Recover Automatically:** Restart renderer on crash

**Example:**
```csharp
try
{
    await webView.NavigateAsync(url);
}
catch (WebView2RuntimeNotFoundException)
{
    // Show user-friendly dialog with download link
    ShowWebView2DownloadDialog();
}
catch (Exception ex)
{
    // Log error
    _logger.Error(ex, "Navigation failed");

    // Show simple message
    ShowNotification("Could not load webpage");

    // Fallback: Show placeholder
    LoadErrorPage();
}
```

**Never Do:**
- Unhandled exceptions
- Generic "Something went wrong"
- Crash without logging
- Modal error dialogs blocking workflow

---

## 13. Logging & Telemetry

**Decision:** ✅ Local File Logging, No Telemetry (v1.0)

**Logging:**
```csharp
// Using Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/webpaper-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

**What We Log:**
- Application lifecycle events
- WorkerW window states
- WebView2 navigation events
- Input hook installations
- Performance metrics
- Errors and exceptions

**What We DON'T Log:**
- User URLs (privacy)
- Cookie values (security)
- Webpage content
- User input

**Telemetry:** None in v1.0 (user privacy first)

**Future (v1.1+):**
- Opt-in anonymous usage stats
- Crash reporting (with user consent)
- Performance analytics

---

## 14. Open Source License

**Decision:** ✅ MIT License (TBD - Pending User Preference)

**Options:**

| License | Permissions | Restrictions | Commercial Use |
|---------|-------------|--------------|----------------|
| **MIT** | Very permissive | Attribution only | Yes |
| **GPL-v3** | Copyleft | Derivatives must be GPL | Yes, but copyleft |
| **Apache 2.0** | Permissive + patents | Attribution + notices | Yes |
| **Proprietary** | None | All reserved | Exclusive |

**Recommended: MIT**
- Maximum adoption
- Commercial-friendly
- Simple and well-understood
- Allows forks and derivatives

**If User Prefers:**
- GPL-v3: To enforce open-source derivatives (like Lively)
- Proprietary: For commercial product

---

## 15. Development Timeline

**Decision:** ✅ 10-Week MVP, 14-Week Production v1.0

**Breakdown:**

```
Week 1-3: Foundation (✅ Current Phase)
├─ WorkerW implementation
├─ WebView2 integration
└─ Basic interaction

Week 4-6: Core Features
├─ Full input handling
├─ Cookie persistence
└─ Performance optimization

Week 7-8: User Experience
├─ System tray UI
├─ Settings window
└─ Error handling

Week 9-10: Testing & Release
├─ Cross-version testing
├─ Documentation
└─ Packaging

Week 11-14: Beta & Polish (if needed)
├─ User feedback
├─ Bug fixes
└─ Final release
```

**Aggressive but Achievable:** Based on Lively's proven approach and WebView2's maturity.

---

## Summary Table: All Key Decisions

| Decision Area | Choice | Alternative | Rationale |
|---------------|--------|-------------|-----------|
| **Approach** | From Scratch | Fork Lively | Full control, lightweight |
| **Web Engine** | WebView2 | CefSharp | Smaller, native, supported |
| **Language** | C# .NET 8 | C++ | Productivity, safety |
| **UI Framework** | WinUI 3 | WPF | Modern, future-proof |
| **Auth** | Direct Login | Browser Import | Simple, reliable for v1.0 |
| **Security** | DPAPI | Custom crypto | Native, proven |
| **Input** | Hooks + Raw Input | Hooks only | Redundancy |
| **Performance** | Aggressive Pause | Always On | Battery life |
| **Distribution** | MSIX | Traditional | Store, updates |
| **Platform** | Win11 first | Win10 first | Modern features |
| **License** | MIT (TBD) | GPL/Proprietary | Permissive |

---

## Decision-Making Principles

Throughout this project, decisions were guided by:

1. **User First:** Prioritize UX over technical elegance
2. **Security:** Never compromise on security for convenience
3. **Performance:** Must be better than Lively for web content
4. **Simplicity:** Start simple, add complexity only when needed
5. **Standards:** Use platform-native solutions over third-party
6. **Future-Proof:** Choose actively maintained technologies
7. **Pragmatism:** Proven solutions over cutting-edge experiments

---

**Next Steps:** See `QUICK_START.md` to begin implementation!

**Questions About Decisions?** Document in `decisions-questions.md` for team discussion.

---

*This document will be updated as new decisions are made during development.*
