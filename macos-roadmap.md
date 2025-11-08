# Interactive Webpage Wallpaper - macOS Roadmap

## Executive Summary

**Feasibility:** ✅ **Feasible** - macOS has native support for this with existing solutions.

**Existing Solution to Build Upon:** [Plash](https://github.com/sindresorhus/Plash) - Previously open source (now closed but still free), actively maintained.

## 1. Technical Overview

### Core Concept
Create an NSWindow at desktop level that renders interactive web content using WKWebView, positioned behind desktop icons.

### Key Technical Requirements
1. **Window Management**: NSWindow with special window levels
2. **Web Rendering**: WKWebView (native WebKit engine)
3. **Input Handling**: Window events and browsing mode toggle
4. **Performance**: Hardware acceleration, GPU rendering

## 2. Existing Open Source Solutions

### Primary Reference: Plash
- **Website**: https://apps.apple.com/app/id1494023538
- **Previous GitHub**: https://github.com/sindresorhus/Plash (no longer open source as of 2023)
- **Tech Stack**: Swift, SwiftUI, WKWebView
- **Features**:
  - Website as wallpaper
  - Multiple websites support
  - Opacity control
  - Reload intervals
  - Browsing mode (for interaction)
  - Per-monitor URL assignment

**Note:** Plash is no longer open source but serves as proof-of-concept. The developer closed the source due to lack of contributions and App Store clones.

### Historical Open Source Alternative: Qdesktop
- Older solution, less maintained
- Similar concept
- Can be studied for techniques

## 3. Technical Implementation Details

### 3.1 Window Level Management

**The Window Level Hierarchy:**

macOS has a well-defined window level system. The key levels:
```
kCGDesktopWindowLevel      = -2147483623  (Desktop wallpaper)
kCGDesktopIconWindowLevel  = -2147483603  (Desktop icons)
kCGNormalWindowLevel       =  0           (Normal windows)
```

**Implementation:**

```swift
import Cocoa
import WebKit

class DesktopWebWindow: NSWindow {
    override init(contentRect: NSRect, 
                 styleMask style: NSWindow.StyleMask, 
                 backing backingStoreType: NSWindow.BackingStoreType, 
                 defer flag: Bool) {
        
        super.init(contentRect: contentRect, 
                  styleMask: [.borderless], 
                  backing: backingStoreType, 
                  defer: flag)
        
        // Set window level below desktop icons
        self.level = NSWindow.Level(rawValue: Int(CGWindowLevelForKey(.desktopWindow)))
        
        // Prevent window from becoming key or main
        self.ignoresMouseEvents = false
        
        // Collection behavior for Spaces/Mission Control
        self.collectionBehavior = [
            .canJoinAllSpaces,
            .stationary,
            .ignoresCycle
        ]
        
        self.backgroundColor = NSColor.clear
        self.isOpaque = false
        self.hasShadow = false
    }
    
    // Prevent window from becoming key
    override var canBecomeKey: Bool { 
        return false 
    }
    
    override var canBecomeMain: Bool { 
        return false 
    }
}
```

**Window Level Testing:**
```swift
// Find the correct level (desktop is actually around -2147483623)
let desktopLevel = CGWindowLevelForKey(.desktopWindow)
let iconLevel = CGWindowLevelForKey(.desktopIconWindow)

// Set to just above desktop, below icons
window.level = NSWindow.Level(rawValue: desktopLevel + 1)
// Or use: window.level = NSWindow.Level(rawValue: -1000)
// Testing shows -1000 works for interactive windows
```

### 3.2 Web Rendering with WKWebView

**WKWebView Integration (Native WebKit):**

```swift
import WebKit

class WebWallpaperView: WKWebView {
    init(frame: CGRect, configuration: WKWebViewConfiguration) {
        let config = configuration
        config.preferences.javaScriptEnabled = true
        config.allowsAirPlayForMediaPlayback = true
        
        // Enable modern web features
        config.preferences.setValue(true, forKey: "developerExtrasEnabled")
        
        super.init(frame: frame, configuration: config)
        
        // Configure for performance
        self.configuration.preferences.setValue(true, forKey: "WebKitAcceleratedCompositingEnabled")
        
        // Allow inline media playback
        self.configuration.allowsInlineMediaPlayback = true
        self.configuration.mediaTypesRequiringUserActionForPlayback = []
    }
    
    required init?(coder: NSCoder) {
        fatalError("init(coder:) not implemented")
    }
}
```

**Loading Web Content:**

```swift
class WallpaperController {
    let webView: WKWebView
    let window: DesktopWebWindow
    
    func loadURL(_ urlString: String) {
        guard let url = URL(string: urlString) else { return }
        
        let request = URLRequest(url: url)
        webView.load(request)
    }
    
    func loadHTMLString(_ html: String) {
        webView.loadHTMLString(html, baseURL: nil)
    }
    
    func reload() {
        webView.reload()
    }
}
```

### 3.3 Input Handling - Browsing Mode

Unlike Windows, macOS desktop-level windows can receive input more easily, but you need to toggle between static and interactive modes.

**Interactive Mode Implementation:**

```swift
class InteractiveWallpaperWindow: NSWindow {
    private var browsingMode: Bool = false
    
    func setBrowsingMode(_ enabled: Bool) {
        browsingMode = enabled
        
        if enabled {
            // Make interactive
            self.level = NSWindow.Level(rawValue: Int(CGWindowLevelForKey(.normalWindow)))
            self.ignoresMouseEvents = false
            self.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        } else {
            // Make non-interactive (behind icons)
            self.level = NSWindow.Level(rawValue: -1000)
            self.ignoresMouseEvents = true
            self.collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle]
        }
    }
    
    // Hotkey to toggle browsing mode
    func setupGlobalHotkey() {
        // Use Carbon or modern event tap
        // Example: Command+Shift+B to toggle
        let hotKeyCenter = MASShortcut.shared()
        hotKeyCenter?.register(shortcut: MASShortcut(keyCode: kVK_ANSI_B, 
                                                     modifierFlags: [.command, .shift]), 
                              withAction: {
            self.setBrowsingMode(!self.browsingMode)
        })
    }
}
```

**Alternative: Always Interactive with Passthrough:**

```swift
// Make window interactive but pass through events to desktop icons
override func sendEvent(_ event: NSEvent) {
    // Check if event is over a desktop icon
    if isEventOverDesktopIcon(event) {
        // Pass through to desktop
        NSApp.sendEvent(event)
    } else {
        // Handle in web view
        super.sendEvent(event)
    }
}

func isEventOverDesktopIcon(_ event: NSEvent) -> Bool {
    let location = event.locationInWindow
    let globalLocation = self.convertToScreen(NSRect(origin: location, size: .zero)).origin
    
    // Use Accessibility API to check for desktop icons
    let element = AXUIElementCreateSystemWide()
    // Query for element at point
    // Return true if it's a desktop icon
    return false // Simplified
}
```

### 3.4 Multi-Monitor Support

```swift
class MultiMonitorManager {
    var windowsPerScreen: [NSScreen: DesktopWebWindow] = [:]
    
    func setupWallpaperForAllScreens() {
        for screen in NSScreen.screens {
            let window = DesktopWebWindow(
                contentRect: screen.frame,
                styleMask: [.borderless],
                backing: .buffered,
                defer: false
            )
            
            window.setFrame(screen.frame, display: true)
            windowsPerScreen[screen] = window
        }
    }
    
    func handleScreenConfigurationChange() {
        // Listen to NSApplication.didChangeScreenParametersNotification
        NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            self?.updateScreenConfiguration()
        }
    }
    
    func updateScreenConfiguration() {
        // Recreate windows for new screen configuration
        windowsPerScreen.removeAll()
        setupWallpaperForAllScreens()
    }
}
```

### 3.5 Performance Optimizations

```swift
class WallpaperOptimizer {
    func configurePerformance(for webView: WKWebView) {
        // 1. Enable GPU acceleration
        webView.configuration.preferences.setValue(true, 
            forKey: "WebKitAcceleratedCompositingEnabled")
        
        // 2. Limit frame rate
        if #available(macOS 12.0, *) {
            webView.configuration.preferences.setValue(30, 
                forKey: "WebKitMaxFrameRate")
        }
        
        // 3. Pause when screen is locked
        NotificationCenter.default.addObserver(
            forName: NSWorkspace.screensDidSleepNotification,
            object: nil,
            queue: .main
        ) { [weak webView] _ in
            webView?.stopLoading()
        }
        
        NotificationCenter.default.addObserver(
            forName: NSWorkspace.screensDidWakeNotification,
            object: nil,
            queue: .main
        ) { [weak webView] _ in
            webView?.reload()
        }
    }
    
    func detectFullscreenApp() -> Bool {
        // Check if any app is in fullscreen
        guard let frontApp = NSWorkspace.shared.frontmostApplication else {
            return false
        }
        
        // Check window list for fullscreen windows
        let options = CGWindowListOption.optionOnScreenOnly
        let windowList = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]]
        
        return windowList?.contains { windowInfo in
            guard let ownerPID = windowInfo[kCGWindowOwnerPID as String] as? Int32,
                  ownerPID == frontApp.processIdentifier else {
                return false
            }
            
            let layer = windowInfo[kCGWindowLayer as String] as? Int32
            return layer == Int32(CGWindowLevelForKey(.maximumWindow))
        } ?? false
    }
}
```

## 4. Detailed Development Roadmap

### Phase 1: Foundation (2-3 weeks)

**Week 1: Project Setup & Research**
- [ ] Set up Xcode project (macOS app)
- [ ] Study Plash behavior (install and test)
- [ ] Test window level positioning
- [ ] Experiment with WKWebView
- [ ] Create proof-of-concept window behind icons

**Week 2: Web Rendering**
- [ ] Integrate WKWebView properly
- [ ] Test various websites
- [ ] Handle JavaScript execution
- [ ] Implement reload functionality
- [ ] Test memory usage

**Week 3: Basic Interactivity**
- [ ] Implement browsing mode toggle
- [ ] Add global hotkey support
- [ ] Test mouse/keyboard input
- [ ] Handle edge cases

**Deliverable:** Basic working prototype with toggle between static and interactive modes.

### Phase 2: Core Features (3-4 weeks)

**Week 4-5: Advanced Features**
- [ ] Multi-monitor support
- [ ] Per-monitor URL assignment
- [ ] Opacity controls
- [ ] Auto-reload intervals
- [ ] URL history/favorites

**Week 6: User Interface**
- [ ] Menu bar app icon
- [ ] Settings window (SwiftUI)
- [ ] URL input interface
- [ ] Preview functionality
- [ ] Enable/disable controls

**Week 7: Performance & Polish**
- [ ] GPU acceleration optimization
- [ ] Battery impact testing
- [ ] Screen sleep handling
- [ ] Memory leak testing
- [ ] Crash recovery

**Deliverable:** Full-featured application with polished UI.

### Phase 3: Distribution (2-3 weeks)

**Week 8: Testing**
- [ ] Test on macOS 12 (Monterey)
- [ ] Test on macOS 13 (Ventura)
- [ ] Test on macOS 14 (Sonoma)
- [ ] Test on macOS 15 (Sequoia)
- [ ] Test on Intel and Apple Silicon
- [ ] Multiple monitor configurations

**Week 9: Packaging**
- [ ] Code signing setup
- [ ] App notarization
- [ ] Create DMG installer
- [ ] Mac App Store preparation (optional)
- [ ] Documentation

**Week 10: Release**
- [ ] Beta testing
- [ ] GitHub release
- [ ] App Store submission (optional)
- [ ] Marketing materials
- [ ] User feedback collection

## 5. Technology Stack Recommendation

### Recommended Stack:
```
Language: Swift
Framework: AppKit + SwiftUI
Web Engine: WKWebView (native WebKit)
UI Framework: SwiftUI for preferences
Distribution: Standalone DMG + Mac App Store
Minimum OS: macOS 12.0 (Monterey)
```

### Build Tools:
```
IDE: Xcode 15+
Package Manager: Swift Package Manager
Code Signing: Developer ID Application
Notarization: Apple Notary Service
CI/CD: GitHub Actions (optional)
```

## 6. Key Limitations & Challenges

### Technical Challenges:

1. **Window Level Behavior**
   - Different behavior across macOS versions
   - Mission Control/Spaces integration tricky
   - Window may appear in wrong Space
   - **Solution:** Test extensively, use proper collection behaviors

2. **Sandbox Restrictions**
   - Mac App Store requires sandboxing
   - Limited access to window management APIs
   - Global hotkeys require accessibility permissions
   - **Mitigation:** Document required permissions, provide non-MAS version

3. **Performance on Older Macs**
   - Web rendering can be heavy
   - Intel Macs with integrated graphics struggle
   - High-resolution Retina displays are demanding
   - **Mitigation:** Frame rate limiting, pause when not visible

4. **Animation Flicker at Desktop Level**
   - Some users report flickering with animations
   - Related to window composition
   - More noticeable on older hardware
   - **Workaround:** Reduce animation complexity, lower frame rate

5. **Desktop Icon Interaction**
   - Tricky to detect if click is on icon
   - Accessibility API required
   - May need special permissions
   - **Solution:** Use browsing mode toggle instead

### Known Limitations:

- **Requires disabling System Integrity Protection** for some advanced features (NOT RECOMMENDED)
- **May conflict with third-party desktop tools** (Bartender, etc.)
- **Not compatible with custom dock replacements**
- **Background rendering still uses resources** even when not visible
- **Some websites don't work well** at desktop level (video autoplay, etc.)

## 7. Recommended Architecture

```
┌─────────────────────────────────────────────┐
│        AppDelegate (Cocoa App)              │
│   - Lifecycle management                    │
│   - Menu bar icon                           │
│   - Notification handling                   │
└─────────────────┬───────────────────────────┘
                  │
    ┌─────────────┴────────────┐
    │                          │
┌───▼───────────────┐  ┌───────▼──────────────┐
│ WindowManager     │  │  PreferencesWindow   │
│ - Window creation │  │  (SwiftUI)           │
│ - Level control   │  │  - URL input         │
│ - Multi-monitor   │  │  - Settings          │
│ - Screen changes  │  │  - Presets           │
└───┬───────────────┘  └──────────────────────┘
    │
┌───▼─────────────────────────────┐
│  DesktopWebWindow (NSWindow)    │
│  - Level: kCGDesktopWindowLevel │
│  - Borderless                   │
│  - All Spaces behavior          │
└───┬─────────────────────────────┘
    │
┌───▼──────────────────────────┐
│  WKWebView                   │
│  - Page rendering            │
│  - JavaScript execution      │
│  - Event handling            │
│  - Hardware acceleration     │
└──────────────────────────────┘
```

## 8. Build New vs Reference Existing

### Build New (Without Plash Source) ✅ RECOMMENDED

**Why:** Plash is no longer open source, so you'll need to build from scratch anyway.

**Pros:**
- Complete control
- Choose your own licensing
- Learn deeply
- Can innovate freely

**Cons:**
- Longer development time
- Need to solve problems others solved
- More testing required

**Approach:**
1. Install and study Plash behavior
2. Use it as reference for features
3. Build your own implementation
4. Test against Plash to ensure compatibility

### Considerations:

- **Don't clone Plash** - respect the developer's decision
- **Create something unique** - add your own features
- **Open source your work** - help the community
- **Be respectful** - credit inspiration sources

## 9. Estimated Timeline & Effort

### Full-Featured Application:
- **Time:** 10-12 weeks
- **Effort:** Part-time (15-20 hours/week)
- **Difficulty:** Medium

### Minimal Viable Product:
- **Time:** 6-8 weeks
- **Effort:** Part-time (10-15 hours/week)
- **Difficulty:** Medium

### Enhanced Version (With Advanced Features):
- **Time:** 16-20 weeks
- **Effort:** Full-time or serious part-time
- **Difficulty:** Medium-High

## 10. Resources & References

### Essential Documentation:
- [WKWebView Documentation](https://developer.apple.com/documentation/webkit/wkwebview)
- [NSWindow Documentation](https://developer.apple.com/documentation/appkit/nswindow)
- [Window Management Guide](https://developer.apple.com/library/archive/documentation/Cocoa/Conceptual/WinPanel/Introduction.html)
- [CGWindowLevel Reference](https://developer.apple.com/documentation/coregraphics/cgwindowlevel)

### Community Resources:
- Swift Forums
- r/macOSBeta subreddit
- Stack Overflow (macOS tag)

### Similar Projects:
- Plash: https://apps.apple.com/app/id1494023538
- GeekTool: Similar concept for widgets
- Übersicht: Desktop widgets with web tech

### Useful Swift Packages:
- KeyboardShortcuts: https://github.com/sindresorhus/KeyboardShortcuts
- LaunchAtLogin: https://github.com/sindresorhus/LaunchAtLogin
- Preferences: https://github.com/sindresorhus/Preferences

## 11. Distribution Strategy

### Option 1: Mac App Store
**Pros:**
- Wider reach
- Automatic updates
- Trusted distribution

**Cons:**
- Strict review process
- Sandboxing required
- 30% revenue share
- Limited functionality

### Option 2: Direct Distribution (Recommended)
**Pros:**
- Full functionality
- No review delays
- No revenue share
- More user trust for power users

**Cons:**
- Users must allow "App from unidentified developer"
- Manual update mechanism needed
- Code signing required
- Notarization recommended

### Option 3: Homebrew Cask
**Pros:**
- Easy for developers
- Command-line install
- Popular in dev community

**Cons:**
- Limited reach
- Technical users only

**Recommended:** Direct distribution + Homebrew cask + Mac App Store (if feasible)

## 12. Monetization Options

1. **Free & Open Source**
   - GitHub Sponsors
   - Buy Me a Coffee
   - Patreon

2. **Freemium**
   - Free basic version
   - Premium features (multiple URLs, presets)
   - One-time purchase or subscription

3. **Mac App Store Paid**
   - $4.99-$9.99 one-time
   - Clean monetization
   - Reach wider audience

## 13. Security & Privacy Considerations

### Required Permissions:
```xml
<!-- Info.plist -->
<key>NSSystemAdministrationUsageDescription</key>
<string>Required to position window behind desktop icons</string>

<key>NSAppTransportSecurity</key>
<dict>
    <key>NSAllowsArbitraryLoads</key>
    <true/>
</dict>
```

### Security Best Practices:
1. **Sandbox web content** - even if app isn't sandboxed
2. **Validate URLs** - prevent file:// or dangerous schemes
3. **Limit JavaScript capabilities** - disable dangerous APIs
4. **No credential storage** - don't store passwords
5. **HTTPS preference** - warn on HTTP sites

## 14. Next Steps

**Immediate Actions:**
1. ✅ Install Plash and test thoroughly
2. ✅ Create new Xcode project
3. ✅ Test window level positioning
4. ✅ Prototype WKWebView integration
5. ✅ Verify on your Mac

**This Week:**
- Implement basic window behind icons
- Test with simple HTML content
- Experiment with browsing mode
- Test performance impact

**This Month:**
- Complete Phase 1 development
- Create working prototype
- Test on multiple macOS versions
- Gather feedback from friends

---

## Conclusion

**Building an interactive webpage wallpaper for macOS is very feasible** using native APIs and WKWebView. The platform has excellent support for this use case, and the window management system is cleaner than Windows.

**Key Advantages of macOS:**
- Native WebKit integration (WKWebView)
- Clean window level system
- Excellent SwiftUI for settings
- Strong community support

**Main Challenges:**
- Plash is no longer open source (must build from scratch)
- Desktop icon detection is tricky
- Sandboxing limits some features
- Performance optimization for older Macs

**Recommended Approach:** Build a new implementation inspired by Plash's features, using Swift, AppKit, and WKWebView. Focus on a great user experience with browsing mode toggle for interactivity. Plan for 10-12 weeks of development time.

The macOS implementation is actually cleaner and more straightforward than Windows, making it a great platform for this project.
