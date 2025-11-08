# 🌐 Project WebPaper

**Transform any webpage into a fully interactive desktop wallpaper for Windows 11**

![Status](https://img.shields.io/badge/status-planning-blue)
![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4)
![License](https://img.shields.io/badge/license-TBD-lightgrey)

---

## 🎯 What is WebPaper?

WebPaper is a lightweight Windows application that renders any webpage as your desktop wallpaper with **full interactivity**:

- ✅ **Scroll** through content
- ✅ **Click** links and buttons
- ✅ **Type** in search boxes and forms
- ✅ **Login** and stay authenticated
- ✅ **Watch** videos and streams
- ✅ Desktop icons remain on top and fully clickable

Think of it as having a live, interactive browser window as your desktop background - perfect for dashboards, social media feeds, news sites, monitoring tools, or just aesthetic web experiences.

---

## 🚀 Project Status

**Current Phase:** Planning & Architecture ✅ COMPLETE

**Timeline:**
- ✅ **Phase 0:** Research & Planning (November 8, 2025)
- 🔄 **Phase 1:** Foundation & Proof of Concept (Weeks 1-3)
- 📋 **Phase 2:** Core Features (Weeks 4-6)
- 📋 **Phase 3:** User Experience (Weeks 7-8)
- 📋 **Phase 4:** Testing & Release (Weeks 9-10)

**Target v1.0 Release:** ~10-14 weeks from start

---

## 📚 Documentation

This project has comprehensive documentation organized for different needs:

### 🏁 Getting Started
- **[QUICK_START.md](QUICK_START.md)** - Start coding TODAY! Week 1 implementation guide with code samples
  - Environment setup (Visual Studio 2022)
  - First proof-of-concept (window behind desktop icons)
  - WebView2 integration
  - Common issues & solutions

### 📖 Comprehensive Planning
- **[IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)** - Complete 10-week development roadmap
  - Detailed architecture design
  - Technical implementation details
  - Phase-by-phase breakdown
  - Code examples and best practices
  - Risk mitigation strategies

### 🎯 Technical Decisions
- **[DECISIONS.md](DECISIONS.md)** - All major technical decisions with rationale
  - Why WebView2 over CefSharp
  - Why build from scratch instead of forking Lively
  - Authentication strategy
  - Performance optimization choices
  - Technology stack justification

### 🗺️ Platform Roadmaps
- **[windows-roadmap.md](windows-roadmap.md)** - Original Windows implementation research
- **[macos-roadmap.md](macos-roadmap.md)** - Future macOS support plan
- **[linux-roadmap.md](linux-roadmap.md)** - Future Linux support plan
- **[cross-platform-summary.md](cross-platform-summary.md)** - Platform comparison

---

## 🛠️ Technology Stack

| Component | Technology | Why? |
|-----------|-----------|------|
| **Language** | C# (.NET 8) | Productivity, safety, modern async/await |
| **UI Framework** | WinUI 3 | Modern Windows 11 design, future-proof |
| **Web Engine** | WebView2 | Native, lightweight (5MB vs 150MB), auto-updates |
| **Distribution** | MSIX | Microsoft Store ready, automatic updates |
| **Auth Storage** | Windows DPAPI | Secure, built-in encryption |

**Key Libraries:**
```xml
<PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2651.64" />
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.240802000" />
<PackageReference Include="CommunityToolkit.WinUI.UI" Version="8.1.240916" />
```

---

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│              Main Application (WinUI 3)                 │
│                                                         │
│  System Tray UI → Settings → URL Management            │
└─────────────────┬───────────────────────────────────────┘
                  │
    ┌─────────────┴─────────────┐
    │                           │
┌───▼──────────────┐  ┌────────▼─────────────┐
│ WallpaperWindow  │  │  InputManager        │
│ Manager          │  │  - Mouse hooks       │
│                  │  │  - Keyboard hooks    │
│ - WorkerW setup  │  │  - Icon detection    │
│ - Multi-monitor  │  │  - Event forwarding  │
└───┬──────────────┘  └─────────┬────────────┘
    │                           │
    └─────────────┬─────────────┘
                  │
         ┌────────▼──────────────┐
         │  WebView2Renderer     │
         │  - Page rendering     │
         │  - Cookie persistence │
         │  - Performance opts   │
         └───────────────────────┘
```

**Core Components:**

1. **WallpaperWindowManager** - Manages desktop integration using WorkerW technique
2. **InputManager** - Low-level hooks for mouse/keyboard, forwards to WebView2
3. **WebView2Renderer** - Renders webpages with hardware acceleration
4. **CookieManager** - Persists authentication across app restarts

---

## ✨ Key Features (Planned)

### v1.0 (MVP - 10 weeks)
- [x] Planning & architecture complete
- [ ] Webpage rendering as wallpaper
- [ ] Full mouse interaction (click, scroll)
- [ ] Full keyboard interaction (typing)
- [ ] Desktop icons remain clickable
- [ ] Authentication persistence (cookies)
- [ ] Multi-monitor support
- [ ] Performance optimization (pause on fullscreen apps)
- [ ] System tray controls
- [ ] Settings UI

### v1.1 (3-4 weeks after v1.0)
- [ ] Browser cookie import (Edge/Chrome)
- [ ] Multiple wallpaper profiles
- [ ] Per-monitor different URLs
- [ ] Scheduled URL changes
- [ ] Global hotkeys

### v2.0 (6+ months)
- [ ] macOS support
- [ ] Cloud sync
- [ ] Plugin system
- [ ] Community wallpaper library

---

## 🔧 Technical Highlights

### Desktop Integration (WorkerW Technique)
```csharp
// Attach window behind desktop icons using undocumented WorkerW API
IntPtr progman = FindWindow("Progman", null);
SendMessageTimeout(progman, 0x052C, ...);
IntPtr workerW = FindWorkerWWindow();
SetParent(yourWindow, workerW);
```

### Cookie Persistence
```csharp
// WebView2 doesn't persist session cookies by default
// Solution: Manual persistence with Windows DPAPI encryption
var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync();
var encrypted = ProtectedData.Protect(SerializeCookies(cookies), ...);
await SaveToSecureStorage(encrypted);
```

### Input Forwarding
```csharp
// Low-level hooks to capture input and forward to WebView2
SetWindowsHookEx(WH_MOUSE_LL, MouseHookCallback, ...);
// With intelligent desktop icon detection to avoid blocking icon clicks
if (!IsClickOnDesktopIcon(point))
    ForwardToWebView(mouseEvent);
```

---

## 🎓 Learning from Existing Solutions

This project studies successful implementations:

**[Lively Wallpaper](https://github.com/rocksdanister/lively)** (GPL-v3, C#, WinUI 3)
- Proven WorkerW technique
- Multi-wallpaper type support (video, web, games)
- Battle-tested with real users
- **Our approach:** Study their methods, build focused web-only solution

**Why Not Fork Lively?**
- We want laser focus on web content (Lively supports videos, shaders, games, etc.)
- Simpler codebase for easier maintenance
- Specific optimizations for web authentication
- Full control over architecture

See [DECISIONS.md](DECISIONS.md) for detailed rationale.

---

## 🚧 Known Challenges & Solutions

### Challenge 1: Windows 11 24H2 WorkerW Changes
**Issue:** WorkerW behavior changed in latest Windows update
**Solution:** Programmatic wallpaper change trigger + monitoring + fallback to Progman

### Challenge 2: Input Handling Performance
**Issue:** Low-level hooks can be removed if too slow (>200ms)
**Solution:** Dedicated thread + fast processing + fallback to Raw Input API

### Challenge 3: WebView2 Cookie Persistence
**Issue:** Session cookies deleted when WebView2 closes
**Solution:** Manual cookie extraction, DPAPI encryption, restore on startup

### Challenge 4: Desktop Icon Detection
**Issue:** Need to distinguish wallpaper clicks from icon clicks
**Solution:** `WindowFromPoint()` hit-testing for SysListView32 window class

See [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) Section 6 for complete risk mitigation strategies.

---

## 📋 Prerequisites

### Development Environment
- **Visual Studio 2022** (Community or higher)
- **Windows App SDK** (latest)
- **.NET 8 SDK**
- **Windows 11 SDK** (10.0.22621.0+)

### System Requirements
- **OS:** Windows 11 (primary), Windows 10 21H2+ (secondary)
- **RAM:** 8GB minimum
- **GPU:** DirectX 11+ support
- **WebView2 Runtime** (included with Windows 11)

---

## 🏃 Quick Start

**Want to start coding right now?**

```powershell
# 1. Clone the repository (when available)
git clone https://github.com/yourusername/project-webpaper.git
cd project-webpaper

# 2. Read the quick start guide
# See QUICK_START.md for detailed Week 1 implementation

# 3. Open Visual Studio 2022
# Create new WinUI 3 project named "WebPaper"

# 4. Add NuGet packages
# Microsoft.Web.WebView2
# Microsoft.WindowsAppSDK

# 5. Follow Day 1-2 tasks in QUICK_START.md
```

**Complete beginner?** Start with [QUICK_START.md](QUICK_START.md) - it has step-by-step instructions with code samples!

**Want the big picture?** Read [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) for the complete 10-week roadmap.

**Curious about decisions?** Check [DECISIONS.md](DECISIONS.md) for rationale behind every major choice.

---

## 📊 Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| **CPU (Idle)** | <5% | Task Manager |
| **CPU (Active)** | <15% | During interaction |
| **CPU (Fullscreen app)** | ~0% | Automatic pause |
| **RAM** | <300MB | Single monitor |
| **Startup Time** | <3s | App launch to visible |
| **Input Latency** | <50ms | Click to response |

---

## 🤝 Contributing

**Current Status:** Project in planning phase, not yet accepting contributions.

**Future Contribution Areas:**
- Code contributions (features, bug fixes)
- Testing on different Windows versions
- Documentation improvements
- Wallpaper profile sharing
- Translations (internationalization)

**Once development starts**, we'll add `CONTRIBUTING.md` with guidelines.

---

## 📜 License

**TBD** - License will be decided before first code commit.

**Options under consideration:**
- **MIT** - Maximum permissiveness, commercial-friendly
- **GPL-v3** - Ensure derivatives remain open source
- **Apache 2.0** - Permissive with patent grant

See [DECISIONS.md](DECISIONS.md) Section 14 for license discussion.

---

## 🙏 Acknowledgments

**Inspiration:**
- **[Lively Wallpaper](https://github.com/rocksdanister/lively)** by rocksdanister - Proving this concept works beautifully
- **Wallpaper Engine** - Commercial implementation showing market demand
- **RainWallpaper** - Alternative approach

**Technologies:**
- **Microsoft WebView2 Team** - Excellent web embedding solution
- **WinUI 3 Team** - Modern Windows UI framework
- **Windows Desktop Development Community**

---

## 📞 Contact & Support

**Project Status:** Planning phase

**Future Support Channels:**
- GitHub Issues (for bugs)
- GitHub Discussions (for questions)
- Discord Server (TBD)

---

## 🗺️ Roadmap at a Glance

```
2025 Q4: Planning & Foundation
├─ ✅ Research complete
├─ ✅ Architecture designed
├─ 🔄 Proof of concept (WorkerW)
└─ 🔄 WebView2 integration

2026 Q1: Core Development
├─ Input handling (hooks)
├─ Cookie persistence
├─ Performance optimization
└─ User interface

2026 Q1-Q2: Testing & Release
├─ Multi-version testing
├─ Documentation
├─ MSIX packaging
└─ v1.0 Public Release

2026 Q2+: Post-Release
├─ v1.1 features (browser import)
├─ v1.2 features (customization)
└─ v2.0 planning (macOS)
```

---

## 💡 Use Cases

**What can you do with an interactive webpage wallpaper?**

- 📊 **Live Dashboards** - Stock market, crypto, system monitoring
- 📰 **News Feeds** - Reddit, Twitter/X, news sites
- 📺 **Video Streams** - YouTube, Twitch (chat visible!)
- 🎵 **Music Players** - Spotify web, YouTube Music
- 📅 **Productivity** - Google Calendar, Trello boards
- 🎨 **Aesthetic** - Interactive art, visualizations
- 🎮 **Browser Games** - Playable directly on desktop
- 📚 **Documentation** - Always-visible reference material
- 🌦️ **Weather & Time** - Live, interactive widgets

**The only limit is your creativity!**

---

## ⚠️ Important Notes

### This is NOT:
- ❌ A video wallpaper player (use Lively for that)
- ❌ A widget system (like Rainmeter)
- ❌ A screen saver
- ❌ A second monitor emulator

### This IS:
- ✅ A full web browser as your wallpaper
- ✅ With complete interactivity
- ✅ With persistent authentication
- ✅ Optimized for low resource usage

---

## 🔬 Research & References

**Essential Reading:**
- [WorkerW Technique Explanation](https://www.codeproject.com/Articles/856020/Draw-Behind-Desktop-Icons-in-Windows)
- [WebView2 Documentation](https://learn.microsoft.com/en-us/microsoft-edge/webview2/)
- [Windows Hooks Deep Dive](https://learn.microsoft.com/en-us/windows/win32/winmsg/hooks)
- [WinUI 3 Documentation](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)

**Community:**
- Lively Wallpaper Discord
- r/wallpaperengine
- r/windows11
- Stack Overflow (tags: `winui3`, `webview2`)

---

## 📈 Project Metrics (Future)

Once released, we'll track:
- GitHub Stars ⭐
- Active Users 👥
- Microsoft Store Ratings ⭐⭐⭐⭐⭐
- Community Wallpaper Profiles 📚
- Performance Benchmarks 🚀

---

## 🎯 Success Criteria

**We'll consider v1.0 successful if:**
- ✅ Works on 95%+ of Windows 11 systems
- ✅ <5% CPU usage when idle
- ✅ Desktop icons remain fully functional
- ✅ Persistent authentication works reliably
- ✅ Subjectively feels "smooth" during interaction
- ✅ Comprehensive documentation
- ✅ Clean, maintainable codebase

---

## 📖 Documentation Navigation

**I want to...** → **Read this document:**

| Goal | Document |
|------|----------|
| Start coding today | [QUICK_START.md](QUICK_START.md) |
| Understand the full plan | [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) |
| Know why decisions were made | [DECISIONS.md](DECISIONS.md) |
| Learn about Windows implementation | [windows-roadmap.md](windows-roadmap.md) |
| Explore future macOS support | [macos-roadmap.md](macos-roadmap.md) |
| Compare platforms | [cross-platform-summary.md](cross-platform-summary.md) |

---

## 🚀 Let's Build This!

This project is in the exciting planning-to-implementation transition phase. All the research is done, architecture is designed, and the path forward is clear.

**Ready to start?** Head to [QUICK_START.md](QUICK_START.md) and let's turn this plan into reality! 🎉

---

**Last Updated:** November 8, 2025
**Project Phase:** Planning → Foundation
**Next Milestone:** Week 1 Proof of Concept

---

<p align="center">Made with ❤️ for the Windows desktop customization community</p>
