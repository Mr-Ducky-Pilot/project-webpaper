# Interactive Webpage Wallpaper - Linux Roadmap

## Executive Summary

**Feasibility:** ⚠️ **Feasible but Complex** - Linux has multiple solutions, but significant fragmentation between X11/Wayland and desktop environments.

**Existing Solutions to Build Upon:** 
- **Hidamari** (for video/web wallpapers)
- **Komorebi** (older, less maintained)
- **Xwinwrap** (X11 only, video focus)

## 1. Technical Overview

### Core Challenge: Fragmentation

Linux presents unique challenges:
- **Display Servers:** X11 vs Wayland (completely different approaches)
- **Desktop Environments:** GNOME, KDE, XFCE, i3, Sway (each has different wallpaper management)
- **Window Managers:** Tiling vs Floating, compositors vs non-compositors

### Key Technical Requirements
1. **Display Server Support:** Separate implementations for X11 and Wayland
2. **Web Rendering:** WebKitGTK or CEF integration
3. **Desktop Environment Compatibility:** Works with DE wallpaper systems
4. **Input Handling:** Varies by display server

## 2. Existing Open Source Solutions

### Primary Reference: Hidamari
- **Repository**: https://github.com/jeffshee/Hidamari
- **Platform**: Flatpak (works on both X11 and Wayland)
- **Tech Stack**: Python, GTK, VLC (for video), WebKitGTK (for web)
- **Status**: Actively maintained
- **Features**:
  - Local video wallpapers
  - Streaming video (YouTube, etc.)
  - **Webpage wallpapers** (perfect starting point!)
  - Hardware acceleration
  - Works on GNOME (X11 & Wayland)

### Secondary Reference: Komorebi
- **Repository**: https://github.com/cheesecakeufo/komorebi
- **Platform**: Native Linux app
- **Tech Stack**: Vala, GTK3, Clutter, WebKit2
- **Status**: Not actively maintained (last update 2018)
- **Note**: Desktop icons disappear when active

### X11-Specific: Xwinwrap
- **Repository**: Multiple forks (https://github.com/mmhobi7/xwinwrap)
- **Purpose**: Wraps applications to desktop background
- **Limitation**: X11 only, doesn't support true interactivity with icons
- **Use Case**: Video wallpapers primarily

## 3. Technical Implementation by Display Server

### 3.1 X11 Implementation

#### Approach 1: Using Xwinwrap (Simpler, Limited Interaction)

**Concept:** Xwinwrap creates a window that X11 treats as desktop background.

```bash
# Basic xwinwrap usage
xwinwrap -g 1920x1080+0+0 \
    -ov -ni -b -nf -un -o 1.0 \
    -- /path/to/your/browser/app
```

**Implementation with Browser:**

```python
# Python wrapper for xwinwrap with web browser
import subprocess
import os

class XwinwrapWallpaper:
    def __init__(self):
        self.xwinwrap_process = None
        self.browser_process = None
    
    def start(self, url, geometry="1920x1080+0+0"):
        # Start browser with specific URL
        browser_cmd = [
            "chromium",
            "--app=" + url,
            "--window-size=1920,1080",
            "--window-position=0,0",
            "--disable-gpu",  # May need for stability
            "--no-sandbox"
        ]
        
        # Wrap it with xwinwrap
        xwinwrap_cmd = [
            "xwinwrap",
            "-g", geometry,
            "-ov",  # Override redirect
            "-ni",  # No input
            "-b",   # Below windows
            "-nf",  # No focus
            "-un",  # Undecorated
            "-o", "1.0",  # Opacity
            "--",
            *browser_cmd
        ]
        
        self.xwinwrap_process = subprocess.Popen(xwinwrap_cmd)
```

**Limitations:**
- No true interactivity (desktop icons disappear or are behind wallpaper)
- `-ni` flag means no input
- Removing `-ni` allows input but breaks desktop functionality

#### Approach 2: Direct X11 Window Manipulation (More Complex, Better Control)

**Concept:** Create window and set `_NET_WM_WINDOW_TYPE` to `_NET_WM_WINDOW_TYPE_DESKTOP`.

```python
import gi
gi.require_version('Gtk', '3.0')
gi.require_version('WebKit2', '4.0')
gi.require_version('Gdk', '3.0')
from gi.repository import Gtk, WebKit2, Gdk, GdkX11

class DesktopWebWindow(Gtk.Window):
    def __init__(self):
        super().__init__()
        
        # Window setup
        self.set_decorated(False)
        self.set_type_hint(Gdk.WindowTypeHint.DESKTOP)
        self.stick()  # Show on all workspaces
        self.set_keep_below(True)
        
        # Get screen size
        screen = Gdk.Screen.get_default()
        width = screen.get_width()
        height = screen.get_height()
        self.set_default_size(width, height)
        self.move(0, 0)
        
        # Create WebKit WebView
        self.webview = WebKit2.WebView()
        self.add(self.webview)
        
        # Connect signals
        self.connect("realize", self.on_realize)
        
        self.show_all()
    
    def on_realize(self, widget):
        # Get X11 window
        gdk_window = self.get_window()
        if isinstance(gdk_window, GdkX11.X11Window):
            xid = gdk_window.get_xid()
            
            # Set window type to desktop using xprop
            import subprocess
            subprocess.run([
                "xprop",
                "-id", str(xid),
                "-f", "_NET_WM_WINDOW_TYPE", "32a",
                "-set", "_NET_WM_WINDOW_TYPE",
                "_NET_WM_WINDOW_TYPE_DESKTOP"
            ])
    
    def load_url(self, url):
        self.webview.load_uri(url)
```

**Input Handling Challenge:**

```python
# Make window pass through events to desktop icons
def setup_input_passthrough(self):
    # This is VERY tricky on X11
    # Options:
    # 1. Use XInput2 to monitor events
    # 2. Check if click is on desktop icon
    # 3. Forward events accordingly
    
    # Pseudocode:
    def on_button_press(widget, event):
        # Check if click is on desktop icon
        if self.is_over_desktop_icon(event.x, event.y):
            # Pass through to desktop
            # This is complex with X11
            return False
        else:
            # Handle in WebView
            return True
    
    self.webview.connect("button-press-event", on_button_press)
```

### 3.2 Wayland Implementation

Wayland is fundamentally different - no concept of "desktop window" or global input.

#### The Wayland Challenge:

1. **No Global Window Hierarchy:** Each window is isolated
2. **No Desktop Background Window:** Wallpaper is handled by compositor
3. **No Input Passthrough:** Can't intercept global events
4. **Compositor-Specific:** Each compositor has different APIs

#### Solution: Layer Shell Protocol

**Use wlr-layer-shell protocol** (supported by wlroots-based compositors: Sway, Hyprland, etc.)

```python
# Requires python-layershell or similar bindings
from gi.repository import Gtk, GtkLayerShell, WebKit2

class WaylandDesktopWebWindow(Gtk.Window):
    def __init__(self):
        super().__init__()
        
        # Initialize layer shell
        GtkLayerShell.init_for_window(self)
        
        # Set layer to background
        GtkLayerShell.set_layer(self, GtkLayerShell.Layer.BACKGROUND)
        
        # Set to cover entire screen
        GtkLayerShell.set_anchor(self, GtkLayerShell.Edge.TOP, True)
        GtkLayerShell.set_anchor(self, GtkLayerShell.Edge.BOTTOM, True)
        GtkLayerShell.set_anchor(self, GtkLayerShell.Edge.LEFT, True)
        GtkLayerShell.set_anchor(self, GtkLayerShell.Edge.RIGHT, True)
        
        # Exclusive zone (don't reserve space)
        GtkLayerShell.set_exclusive_zone(self, -1)
        
        # Set keyboard interactivity
        GtkLayerShell.set_keyboard_mode(
            self,
            GtkLayerShell.KeyboardMode.NONE  # or ON_DEMAND for interactive
        )
        
        # Create WebView
        self.webview = WebKit2.WebView()
        self.add(self.webview)
        
        self.show_all()
    
    def load_url(self, url):
        self.webview.load_uri(url)
```

**Interactive Mode Toggle:**

```python
def set_interactive(self, interactive):
    if interactive:
        # Allow keyboard input
        GtkLayerShell.set_keyboard_mode(
            self,
            GtkLayerShell.KeyboardMode.EXCLUSIVE
        )
        # Move to OVERLAY layer (above desktop)
        GtkLayerShell.set_layer(self, GtkLayerShell.Layer.OVERLAY)
    else:
        # Disable keyboard
        GtkLayerShell.set_keyboard_mode(
            self,
            GtkLayerShell.KeyboardMode.NONE
        )
        # Back to BACKGROUND
        GtkLayerShell.set_layer(self, GtkLayerShell.Layer.BACKGROUND)
```

**Limitation:** Desktop icons still disappear on most DEs because you're replacing the wallpaper layer.

### 3.3 Web Rendering Engine Options

#### Option A: WebKitGTK (Recommended for Linux)

**Pros:**
- Native GTK integration
- Lightweight compared to CEF
- Well-supported on Linux
- Used by GNOME Web (Epiphany)

**Cons:**
- Slightly behind Chromium in features
- Less powerful developer tools

**Installation:**
```bash
# Debian/Ubuntu
sudo apt install libwebkit2gtk-4.0-dev

# Fedora
sudo dnf install webkit2gtk3-devel

# Arch
sudo pacman -S webkit2gtk
```

**Usage:**
```python
from gi.repository import WebKit2

webview = WebKit2.WebView()
settings = webview.get_settings()
settings.set_enable_javascript(True)
settings.set_enable_webgl(True)
settings.set_hardware_acceleration_policy(
    WebKit2.HardwareAccelerationPolicy.ALWAYS
)

webview.load_uri("https://example.com")
```

#### Option B: CEF (Chromium Embedded Framework)

**Pros:**
- Full Chrome compatibility
- Better web standards support
- More features

**Cons:**
- Large binary size
- Complex build process
- C++ required (though Python bindings exist)

**Python CEF:**
```python
from cefpython3 import cefpython as cef

# Initialize CEF
settings = {
    "windowless_rendering_enabled": True,
}
cef.Initialize(settings)

# Create browser (offscreen)
browser = cef.CreateBrowserSync(
    url="https://example.com",
    window_info=cef.WindowInfo()
)
```

#### Option C: Electron (Not Recommended)

Too heavy for background rendering, but technically possible.

## 4. Desktop Environment Specific Challenges

### GNOME (X11 & Wayland)

**Challenge:** GNOME Shell/Nautilus manages desktop
- Desktop icons drawn by GNOME Shell
- Wallpaper managed by gnome-settings-daemon

**Solutions:**
1. **Disable GNOME's desktop drawing:**
   ```bash
   gsettings set org.gnome.desktop.background show-desktop-icons false
   ```
2. **Use layer-shell on Wayland** (icons disappear)
3. **Extension-based approach:** Create GNOME Shell extension

### KDE Plasma

**Challenge:** Plasma Desktop manages everything
- Wallpaper is a plasmoid
- Complex widget system

**Solutions:**
1. **Replace wallpaper plugin**
2. **Use window behind desktop (X11)**
3. **May conflict with desktop effects**

### XFCE

**Challenge:** xfdesktop draws desktop
- Simpler than GNOME/KDE
- Better compatibility with window-based approaches

**Solutions:**
1. **Xwinwrap works reasonably well**
2. **Replace xfdesktop temporarily**

### i3/Sway (Tiling Window Managers)

**Best Case Scenario:**
- No desktop icons to worry about
- Direct control over windows
- Layer shell works perfectly on Sway

## 5. Detailed Development Roadmap

### Phase 1: Foundation (3-4 weeks)

**Week 1: Environment Setup & Testing**
- [ ] Set up Linux development VM
- [ ] Install development dependencies (GTK, WebKit)
- [ ] Study Hidamari source code
- [ ] Test existing solutions (Hidamari, Komorebi)
- [ ] Create basic GTK application

**Week 2: Display Server Detection & Basic Window**
- [ ] Implement X11 detection
- [ ] Implement Wayland detection
- [ ] Create basic borderless window
- [ ] Test window positioning on X11
- [ ] Test layer shell on Wayland

**Week 3: WebKit Integration**
- [ ] Integrate WebKitGTK
- [ ] Load simple HTML page
- [ ] Test JavaScript execution
- [ ] Test various websites
- [ ] Measure performance

**Week 4: Desktop Environment Testing**
- [ ] Test on GNOME (X11)
- [ ] Test on GNOME (Wayland)
- [ ] Test on KDE Plasma
- [ ] Test on XFCE
- [ ] Document compatibility

**Deliverable:** Basic window rendering web content, tested on multiple DEs.

### Phase 2: Interactivity & Features (4-5 weeks)

**Week 5-6: Input Handling**
- [ ] Implement interactive mode toggle
- [ ] X11 input handling
- [ ] Wayland input handling
- [ ] Hotkey support (global if possible)
- [ ] Focus management

**Week 7: Multi-Monitor Support**
- [ ] Detect connected monitors
- [ ] Create window per monitor
- [ ] Handle monitor changes
- [ ] Per-monitor URL assignment

**Week 8: Performance Optimization**
- [ ] Enable hardware acceleration
- [ ] Frame rate limiting
- [ ] Idle detection
- [ ] Screensaver detection
- [ ] Battery mode optimization

**Week 9: Configuration & UI**
- [ ] GTK settings window
- [ ] URL input and management
- [ ] Presets/favorites
- [ ] Startup on boot
- [ ] System tray icon

**Deliverable:** Full-featured application with configuration UI.

### Phase 3: Packaging & Distribution (2-3 weeks)

**Week 10: Packaging**
- [ ] Create Flatpak package
- [ ] Create AppImage
- [ ] Create Snap (optional)
- [ ] Debian package
- [ ] AUR package (Arch)
- [ ] RPM package (Fedora)

**Week 11: Testing & Documentation**
- [ ] Test on Ubuntu 22.04, 24.04
- [ ] Test on Fedora Workstation
- [ ] Test on Arch Linux
- [ ] Test on Pop!_OS
- [ ] Write comprehensive documentation
- [ ] Create video tutorial

**Week 12: Release & Community**
- [ ] GitHub release
- [ ] Submit to Flathub
- [ ] Post to r/unixporn
- [ ] Post to OMG! Ubuntu
- [ ] Gather feedback
- [ ] Fix critical issues

## 6. Technology Stack Recommendation

### Recommended Stack (GTK):
```
Language: Python 3.9+
GUI Framework: GTK 3 (or GTK 4 for future)
Web Engine: WebKitGTK 2.x
Display Servers: X11 (Xlib) + Wayland (layer-shell)
Build System: Meson + Ninja
Packaging: Flatpak (primary), AppImage, .deb
```

### Alternative Stack (Native):
```
Language: C++ or Rust
GUI Framework: GTK3/GTK4 or Qt
Web Engine: QtWebEngine or WebKitGTK
Display Servers: X11 + Wayland
Build System: CMake or Cargo
Packaging: Native packages + Flatpak
```

### Dependencies:
```bash
# Development dependencies
libgtk-3-dev
libwebkit2gtk-4.0-dev  
python3-gi
python3-gi-cairo
gir1.2-gtklayershell-0.1  # For Wayland
libx11-dev  # For X11
```

## 7. Key Limitations & Challenges

### Major Challenges:

1. **Desktop Icons Incompatibility**
   - **X11:** Icons disappear or window in wrong layer
   - **Wayland:** No good solution yet
   - **Workaround:** Disable icons, or use toggle mode
   - **Best case:** Tiling WMs (no icons)

2. **Display Server Fragmentation**
   - Need TWO separate implementations
   - Different APIs, different behaviors
   - Can't test both simultaneously
   - **Solution:** Detect and use appropriate backend

3. **Desktop Environment Conflicts**
   - Each DE handles wallpaper differently
   - GNOME Shell is particularly difficult
   - KDE Plasma's widget system conflicts
   - **Solution:** Document compatibility, offer disable instructions

4. **Distribution Complexity**
   - Many package formats
   - Dependencies vary across distros
   - Older distros have old WebKitGTK
   - **Solution:** Flatpak as primary, others as secondary

5. **Performance on Old Hardware**
   - Web rendering is heavy
   - Integrated graphics struggle
   - **Solution:** Frame limiting, low-power mode

### Critical Limitations:

- **Desktop icons and web wallpaper don't coexist well** on most setups
- **Works best on tiling window managers** (i3, Sway, Hyprland)
- **GNOME Wayland is problematic** due to strict compositor rules
- **Requires recent display server** (Wayland needs layer-shell support)
- **May break with compositor updates**

## 8. Architecture Diagram

```
┌───────────────────────────────────────────────────┐
│           Main Application (Python/GTK)           │
│  - Configuration                                  │
│  - System tray icon                               │
│  - Settings UI                                    │
└────────────────────┬──────────────────────────────┘
                     │
      ┌──────────────┴──────────────┐
      │                             │
┌─────▼────────────┐      ┌─────────▼─────────────┐
│ Display Server   │      │  Multi-Monitor        │
│ Detection        │      │  Manager              │
│ - X11 or Wayland │      │  - Screen enumeration │
│ - Capabilities   │      │  - Change detection   │
└─────┬────────────┘      └─────────┬─────────────┘
      │                             │
      └──────────────┬──────────────┘
                     │
      ┌──────────────┴───────────────┐
      │                              │
┌─────▼────────┐           ┌─────────▼──────────┐
│ X11 Backend  │           │ Wayland Backend    │
│ - Window     │           │ - Layer Shell      │
│ - _NET_WM    │           │ - wlr protocol     │
│ - Input      │           │ - Compositor       │
└─────┬────────┘           └─────────┬──────────┘
      │                              │
      └──────────────┬───────────────┘
                     │
            ┌────────▼─────────┐
            │  WebKitGTK       │
            │  WebView         │
            │  - HTML/CSS/JS   │
            │  - GPU accel     │
            │  - Event routing │
            └──────────────────┘
```

## 9. Build from Scratch vs Fork Existing

### Fork Hidamari ✅ RECOMMENDED

**Why:**
- Already supports webpage wallpapers
- Works on both X11 and Wayland
- Python codebase (easy to modify)
- Flatpak infrastructure ready
- Active maintenance

**Pros:**
- Save 6-8 weeks of development
- Proven compatibility
- Established user base
- Good documentation

**Cons:**
- Learn existing codebase
- May need refactoring
- Python (not everyone's preference)

**Approach:**
1. Fork Hidamari
2. Focus on improving web wallpaper features
3. Add interactivity controls
4. Contribute improvements upstream

### Build from Scratch

**When to choose:**
- Want different tech stack (Rust, C++)
- Want minimal dependencies
- Want different architecture
- Learning experience

**Time:** 12-16 weeks vs 6-8 weeks forking

## 10. Estimated Timeline & Effort

### Fork Hidamari & Enhance:
- **Time:** 6-8 weeks
- **Effort:** Part-time (10-15 hours/week)
- **Difficulty:** Medium

### Build from Scratch (Full):
- **Time:** 14-18 weeks  
- **Effort:** Full-time or serious part-time
- **Difficulty:** High

### Minimal Viable Product (Scratch):
- **Time:** 8-10 weeks
- **Effort:** Part-time (15-20 hours/week)
- **Difficulty:** Medium-High

## 11. Distribution Strategy

### Recommended Order:

1. **Flatpak** (Primary)
   - Cross-distro compatibility
   - Sandboxed
   - Easy updates
   - Submit to Flathub

2. **AUR** (Arch)
   - Easy for Arch users
   - Quick to maintain
   - Popular in community

3. **AppImage**
   - Universal binary
   - No installation needed
   - Good for testing

4. **Native Packages**
   - .deb for Debian/Ubuntu
   - .rpm for Fedora
   - More work to maintain

### Flatpak Example:

```yaml
# org.example.WebWallpaper.yml
app-id: org.example.WebWallpaper
runtime: org.gnome.Platform
runtime-version: '45'
sdk: org.gnome.Sdk
command: webwallpaper
finish-args:
  - --share=ipc
  - --socket=fallback-x11
  - --socket=wayland
  - --device=dri
  - --share=network
modules:
  - name: webwallpaper
    buildsystem: simple
    build-commands:
      - pip3 install --prefix=/app .
    sources:
      - type: dir
        path: .
```

## 12. Resources & References

### Essential Documentation:
- [GTK Documentation](https://docs.gtk.org/)
- [WebKitGTK API](https://webkitgtk.org/reference/webkit2gtk/stable/)
- [wlr-layer-shell Protocol](https://wayland.app/protocols/wlr-layer-shell-unstable-v1)
- [X11 Window Properties](https://specifications.freedesktop.org/wm-spec/wm-spec-latest.html)

### Source Code References:
- Hidamari: https://github.com/jeffshee/Hidamari
- Komorebi: https://github.com/cheesecakeufo/komorebi
- Xwinwrap: https://github.com/mmhobi7/xwinwrap

### Community:
- r/unixporn
- r/linux
- GNOME Discourse
- KDE Forums

## 13. Testing Strategy

### Test Matrix:

| Distribution | X11 | Wayland | DE | Priority |
|-------------|-----|---------|-----|----------|
| Ubuntu 24.04 | ✓ | ✓ | GNOME | High |
| Fedora 40 | ✓ | ✓ | GNOME | High |
| Arch Linux | ✓ | ✓ | KDE | High |
| Pop!_OS | ✓ | ✗ | GNOME | Medium |
| Manjaro | ✓ | ✓ | XFCE | Medium |
| EndeavourOS | ✓ | ✓ | i3/Sway | High |

### Test Scenarios:
- [ ] Single monitor
- [ ] Dual monitors (same resolution)
- [ ] Dual monitors (different resolution)
- [ ] Monitor hotplug
- [ ] Suspend/resume
- [ ] Lock screen
- [ ] VT switch
- [ ] Different browsers as wallpaper
- [ ] Video-heavy websites
- [ ] WebGL content

## 14. Next Steps

**Week 1:**
1. Install Hidamari and test thoroughly
2. Fork Hidamari repository
3. Study web wallpaper implementation
4. Test on your Linux setup
5. Identify improvement areas

**Week 2:**
- Set up development environment
- Make first enhancement
- Test on X11 and Wayland
- Document your changes

**Month 1:**
- Complete core improvements
- Create Flatpak package
- Test on multiple distros
- Release beta version

---

## Conclusion

**Building an interactive webpage wallpaper for Linux is feasible but complex** due to display server and desktop environment fragmentation. The key insight is that **forking Hidamari** provides the best starting point since it already handles most complexity.

**Recommended Strategy:**
1. ✅ Fork Hidamari (saves months of work)
2. ✅ Focus on improving web wallpaper features
3. ✅ Add better interactivity controls
4. ✅ Package as Flatpak for wide distribution
5. ✅ Target tiling WMs for best experience

**Reality Check:**
- Desktop icons + interactive web wallpaper = **difficult on most DEs**
- Works **best on tiling window managers** (i3, Sway, Hyprland)
- **X11 is easier** but Wayland is the future
- **Expect 6-8 weeks** minimum even forking existing code

**Best Use Cases:**
- Tiling window managers (no desktop icons)
- Developer workstations
- Information dashboards
- Always-visible web apps
- Minimal desktop setups

Linux is the most challenging platform but also offers the most flexibility and control. With the right approach (forking Hidamari), you can have a working solution in 6-8 weeks rather than starting from scratch which would take 4-5 months.
