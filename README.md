<p align="center">
  <img src="src/WebPaper/Assets/WebPaperLogo.png" alt="WebPaper Logo" width="200" height="200"/>
</p>

<h1 align="center">🌐 WebPaper</h1>

<p align="center">
  <strong>Transform any webpage into a fully interactive desktop wallpaper</strong>
  <br><br>
  <em>Click • Type • Scroll • Interact</em>
  <br>
  <sub>Your favorite websites, AI assistants, and dashboards — right on your desktop</sub>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/status-beta-brightgreen" alt="Status"/>
  <img src="https://img.shields.io/badge/version-1.0.0--beta-blue" alt="Version"/>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4" alt="Platform"/>
  <img src="https://img.shields.io/badge/license-Personal%20Use-orange" alt="License"/>
</p>

---

## ✨ What is WebPaper?

WebPaper brings the **full power of the web** to your Windows desktop wallpaper. It's not just a background image — it's a **fully interactive browser** where you can click, type, and scroll through any website you want.

### 🎯 Perfect For

<table>
<tr>
<td width="50%">

**🤖 AI Assistants**
- Chat with ChatGPT right from your desktop
- Ask Gemini or Claude anything instantly
- Perplexity AI for quick research
- No need to open a browser tab!

</td>
<td width="50%">

**📊 Live Dashboards**
- Monitor your deployment status 24/7
- Grafana, Datadog, system metrics
- Server health at a glance
- Production monitoring made visual

</td>
</tr>
<tr>
<td width="50%">

**🎨 Your Favorite Sites**
- Beautiful websites as living wallpapers
- Portfolio sites, art galleries
- Interactive visualizations
- Make your desktop uniquely yours

</td>
<td width="50%">

**📺 Entertainment**
- YouTube, Twitch streams
- Spotify web player
- Reddit, news feeds
- Always-on information

</td>
</tr>
</table>

---

## 🚀 Use Cases

**Imagine having these on your desktop, always visible, always interactive:**

### 💬 AI & Productivity
- 🤖 **ChatGPT** - Your AI assistant on the desktop, ready to help anytime
- 🔍 **Perplexity AI** - Research assistant at your fingertips
- ✨ **Google Gemini** - AI chat right on your wallpaper
- 🧠 **Claude** - Anthropic's AI, accessible instantly
- 📝 **Notion** - Your notes and tasks, always visible
- 📅 **Google Calendar** - See your schedule at a glance

### 📊 Developer & Tech
- 📈 **Grafana Dashboards** - Real-time metrics without opening a browser
- 🚀 **Vercel/Netlify** - Deployment status on your desktop
- 🐙 **GitHub Actions** - CI/CD pipeline status visible 24/7
- 📊 **DataDog/New Relic** - Application monitoring at a glance
- 🔧 **Jenkins/CircleCI** - Build status always visible
- 💻 **System Dashboards** - CPU, memory, network stats

### 🎬 Entertainment & Media
- 📺 **YouTube** - Watch videos, streams, tutorials (with chat!)
- 🎵 **Spotify Web** - Control your music visually
- 📰 **Reddit** - Browse your favorite subreddits
- 🎮 **Twitch** - Watch streams and interact with chat
- 🌐 **Twitter/X** - Social feed on your desktop
- 📖 **Medium** - Read articles while working

### 🌍 Information & Tools
- 🌤️ **Weather** - Live radar, forecasts, interactive maps
- ✈️ **FlightRadar24** - Track flights in real-time
- 📈 **Trading View** - Stock market, crypto charts
- 🌌 **Stellarium** - Live sky map and astronomy
- 🗺️ **Google Earth** - Explore the world
- 🎨 **Shadertoy** - Beautiful interactive shaders

**The only limit is your imagination!**

---

## ⚡ Key Features

- ✅ **Fully Interactive** - Click, type, scroll — everything works (incl. Shift, Ctrl, Alt and arrow keys)
- ✅ **Stay Logged In** - Secure cookie storage keeps you authenticated
- ✅ **Desktop Icons Work** - Icons remain fully clickable on top
- ✅ **Foreground Apps Untouched** - Clicks on Chrome / VS Code / File Explorer pass through cleanly
- ✅ **Multi-Monitor** - Pick a single monitor or render the wallpaper on every monitor at once
- ✅ **Desktop Right-Click Menu** - Settings / Reload / Home / Toggle / About route into the running instance
- ✅ **Auto-Pause** - Saves resources during fullscreen apps and low battery
- ✅ **Beautiful UI** - Modern design with smooth animations
- ✅ **System Tray** - Quick controls and settings
- ✅ **Lightweight** - Only 1-2% CPU when idle
- ✅ **Privacy First** - Zero telemetry, zero tracking

---

## 📥 Installation

### Option 1: Download Release (Coming Soon)
1. Download the MSIX installer from [Releases](../../releases)
2. Double-click to install
3. Launch from Start Menu
4. Enter your desired webpage URL
5. Enjoy!

### Option 2: Build from Source
```bash
# Requirements: Visual Studio 2022, .NET 8 SDK
git clone https://github.com/Mr-Ducky-Pilot/project-webpaper.git
cd project-webpaper
dotnet build -c Release
dotnet run --project src/WebPaper/WebPaper.csproj
```

**📖 Need help?** See our [Wiki documentation](../../wiki)

---

## 💡 Why WebPaper?

> "I want to see my website as my wallpaper cause it's so pretty"
> — A friend, one evening

That simple wish sparked a question: **Why isn't this possible?**

After discovering Lively Wallpaper (which supports webpages but without full interactivity — no scrolling or typing), I decided to create something better. A **lightweight, fully interactive** webpage wallpaper that works exactly like a browser.

**Hence the name: WebPaper** = Webpage + Wallpaper ✨

---

## 🖥️ Multi-Monitor

WebPaper supports two layouts, configured via Settings:

| Mode             | Behavior                                                                              |
|------------------|---------------------------------------------------------------------------------------|
| **Single monitor** *(default)* | One wallpaper window on the monitor selected by `PreferredMonitorIndex`.   |
| **All monitors** | One wallpaper window per monitor, each rendering the same URL independently.          |

Under the hood, "All monitors" mode spawns one additional `MainWindow`/`WebView2`
per non-primary monitor, each parented into WorkerW and scoped to its own
screen rect — this is the same per-display approach that Lively Wallpaper uses.
Input forwarding is automatically restricted to the monitor each instance owns,
so clicking on monitor A only affects monitor A's webpage.

---

## 📚 Documentation

- **[User Guide](../../wiki/User-Guide)** - Installation, usage, troubleshooting
- **[Technical Overview](../../wiki/Technical-Overview)** - How WebPaper works
- **[Contributing](../../wiki/Contributing)** - Contribution guidelines
- **[FAQ](../../wiki/FAQ)** - Frequently asked questions

---

## 🐛 Found a Bug? 💡 Have an Idea?

- **Report Bugs:** Use our [Bug Report Template](../../issues/new?template=bug_report.md)
- **Request Features:** Use our [Feature Request Template](../../issues/new?template=feature_request.md)
- **Ask Questions:** [GitHub Discussions](../../discussions)

---

## 📜 License

**Personal & Hobbyist Use License**

- ✅ **Free for personal use** - Use it, modify it, enjoy it!
- ✅ **Free for hobbyists** - Build cool projects, share with friends
- ❌ **Not for commercial use** - Contact me for commercial licensing
- 📝 **Attribution required** - Please credit **Omprakash J (MrDuck)**

**For collaboration or commercial licensing:**
📧 Contact: omprakashj2010@gmail.com

See [LICENSE](LICENSE) for full details.

---

## 🙏 Credits

**Created by:** [Omprakash J](https://github.com/Mr-Ducky-Pilot) (MrDuck)

**Inspired by:** [Lively Wallpaper](https://github.com/rocksdanister/lively) - An excellent wallpaper engine that proved the concept

**Built with:**
- Microsoft WebView2 (Chromium-based rendering)
- WinUI 3 (Modern Windows UI)
- .NET 8 (High-performance runtime)

---

## 🌟 Show Your Support

If you love WebPaper:
- ⭐ **Star this repository**
- 🐛 **Report bugs** to help improve it
- 💡 **Share ideas** for new features
- 📢 **Spread the word** to friends and communities

---

<p align="center">
  <img src="src/WebPaper/Assets/WebPaperLogo.png" alt="WebPaper" width="80" height="80"/>
  <br><br>
  <strong>Made with ❤️ by MrDuck</strong>
  <br>
  <sub>Transform your desktop. Make it yours. ✨</sub>
</p>

---

**System Requirements:** Windows 10 (1809+) or Windows 11 • 4GB RAM • Internet connection
**Version:** 1.0.0-beta • **Status:** Feature complete & ready to use! 🎉
