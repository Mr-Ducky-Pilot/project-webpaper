# Project WebPaper - Quick Start Guide

## 🎯 Project Overview
**Interactive webpage wallpaper for Windows 11** - Render any website as your desktop background with full interaction (scroll, click, type, login).

---

## ✅ Key Decisions Made

### Technology Stack
- **Language:** C# (.NET 8)
- **Framework:** WinUI 3
- **Web Engine:** WebView2 (Microsoft Edge)
- **Distribution:** MSIX (Microsoft Store ready)

### Approach
- **Build from scratch** (not forking Lively) for lightweight, focused solution
- **Windows 11 first**, Windows 10 compatibility as secondary
- **Direct login** for authentication (v1.0), browser import later (v1.1)

---

## 📋 Prerequisites

### Required Software
```powershell
# Install via Visual Studio Installer
- Visual Studio 2022 (Community Edition or higher)
- Windows App SDK workload
- .NET Desktop Development workload
- Windows 11 SDK (10.0.22621.0 or later)
```

### System Requirements
- Windows 11 (any version, including 24H2)
- Windows 10 21H2+ (for testing compatibility)
- 8GB RAM minimum
- GPU with DirectX 11+ support

---

## 🚀 Getting Started (Week 1)

### Day 1-2: Environment Setup

#### Step 1: Install Visual Studio 2022
```powershell
# Download from https://visualstudio.microsoft.com/downloads/
# Select workloads:
# - .NET Desktop Development
# - Windows application development
# - Windows App SDK C# Templates
```

#### Step 2: Create WinUI 3 Project
```powershell
# In Visual Studio:
# 1. File > New > Project
# 2. Search for "WinUI 3"
# 3. Select "Blank App, Packaged (WinUI 3 in Desktop)"
# 4. Name: WebPaper
# 5. Framework: .NET 8.0
```

#### Step 3: Add Required NuGet Packages
```xml
<!-- Add to WebPaper.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2651.64" />
  <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.240802000" />
  <PackageReference Include="CommunityToolkit.WinUI.UI" Version="8.1.240916" />
  <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
</ItemGroup>
```

#### Step 4: Project Structure
```
WebPaper/
├── App.xaml / App.xaml.cs          # Application entry
├── MainWindow.xaml / .cs            # System tray & config UI
├── Core/
│   ├── WallpaperWindow.cs          # Window behind desktop
│   ├── WorkerWManager.cs           # Desktop integration
│   ├── WebView2Renderer.cs         # Web rendering
│   ├── InputManager.cs             # Mouse/keyboard hooks
│   └── PerformanceManager.cs       # Resource optimization
├── Services/
│   ├── CookieManager.cs            # Cookie persistence
│   └── ConfigService.cs            # Settings management
├── Native/
│   └── NativeMethods.cs            # P/Invoke declarations
└── Models/
    ├── WallpaperConfig.cs
    └── Cookie.cs
```

### Day 3-4: WorkerW Proof of Concept

#### Create NativeMethods.cs
```csharp
using System;
using System.Runtime.InteropServices;

namespace WebPaper.Native
{
    public static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr hWndChildAfter,
            string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            SendMessageTimeoutFlags flags, uint timeout, out IntPtr result);

        [Flags]
        public enum SendMessageTimeoutFlags : uint
        {
            SMTO_NORMAL = 0x0,
            SMTO_BLOCK = 0x1,
            SMTO_ABORTIFHUNG = 0x2,
            SMTO_NOTIMEOUTIFNOTHUNG = 0x8
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }
    }
}
```

#### Create WorkerWManager.cs
```csharp
using System;
using WebPaper.Native;
using static WebPaper.Native.NativeMethods;

namespace WebPaper.Core
{
    public class WorkerWManager
    {
        public IntPtr FindWorkerW()
        {
            // Step 1: Find Progman window
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero)
            {
                throw new Exception("Could not find Progman window");
            }

            // Step 2: Send message to spawn WorkerW
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero,
                SendMessageTimeoutFlags.SMTO_NORMAL, 1000, out IntPtr result);

            // Step 3: Find WorkerW window
            IntPtr workerW = IntPtr.Zero;

            EnumWindows((topHandle, param) =>
            {
                IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero,
                    "SHELLDLL_DefView", null);

                if (shellView != IntPtr.Zero)
                {
                    workerW = FindWindowEx(IntPtr.Zero, topHandle,
                        "WorkerW", null);
                }

                return true;
            }, IntPtr.Zero);

            if (workerW == IntPtr.Zero)
            {
                // Fallback to Progman
                return progman;
            }

            return workerW;
        }

        public void AttachWindowToDesktop(IntPtr windowHandle)
        {
            IntPtr workerW = FindWorkerW();
            SetParent(windowHandle, workerW);
        }
    }
}
```

#### Test Code (in MainWindow.xaml.cs)
```csharp
using Microsoft.UI.Xaml;
using WebPaper.Core;

namespace WebPaper
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Test WorkerW
            var workerWManager = new WorkerWManager();

            this.Activated += async (s, e) =>
            {
                // Get window handle
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                // Attach to desktop
                workerWManager.AttachWindowToDesktop(hwnd);

                // Set window to cover screen
                var bounds = DisplayArea.Primary.WorkArea;
                AppWindow.Resize(new Windows.Graphics.SizeInt32
                {
                    Width = bounds.Width,
                    Height = bounds.Height
                });
            };
        }
    }
}
```

**Expected Result:** Your window should appear behind desktop icons!

### Day 5-7: WebView2 Integration

#### Create WebView2Renderer.cs
```csharp
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;

namespace WebPaper.Core
{
    public class WebView2Renderer
    {
        private WebView2 _webView;
        private string _userDataFolder;

        public WebView2Renderer(WebView2 webView)
        {
            _webView = webView;
            _userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebPaper", "WebView2Data"
            );
        }

        public async Task InitializeAsync()
        {
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: _userDataFolder
            );

            await _webView.EnsureCoreWebView2Async(env);

            // Configure settings
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

            // Handle navigation errors
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        }

        public async Task NavigateAsync(string url)
        {
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            _webView.CoreWebView2.Navigate(url);
        }

        private void OnNavigationCompleted(CoreWebView2 sender,
            CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                // Handle navigation error
                Console.WriteLine($"Navigation failed: {args.WebErrorStatus}");
            }
        }
    }
}
```

#### Update MainWindow.xaml
```xml
<Window
    x:Class="WebPaper.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <WebView2 x:Name="webView" />
    </Grid>
</Window>
```

#### Update MainWindow.xaml.cs
```csharp
public sealed partial class MainWindow : Window
{
    private WorkerWManager _workerWManager;
    private WebView2Renderer _renderer;

    public MainWindow()
    {
        this.InitializeComponent();
        _workerWManager = new WorkerWManager();
        _renderer = new WebView2Renderer(webView);

        this.Activated += OnActivated;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        // Initialize WebView2
        await _renderer.InitializeAsync();

        // Navigate to test page
        await _renderer.NavigateAsync("https://www.youtube.com");

        // Attach to desktop
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _workerWManager.AttachWindowToDesktop(hwnd);

        // Resize to screen
        var bounds = DisplayArea.Primary.WorkArea;
        AppWindow.Resize(new Windows.Graphics.SizeInt32
        {
            Width = bounds.Width,
            Height = bounds.Height
        });
    }
}
```

**Expected Result:** YouTube (or any webpage) rendering as your wallpaper!

---

## 📊 Week 1 Deliverable Checklist

- [ ] Visual Studio 2022 installed with WinUI 3 templates
- [ ] Project created and building successfully
- [ ] NativeMethods.cs with P/Invoke declarations
- [ ] WorkerWManager.cs successfully finding WorkerW
- [ ] Window rendering behind desktop icons
- [ ] WebView2 integrated and loading webpages
- [ ] Webpage visible as wallpaper (even without interaction)

**If all checked:** Proceed to Week 2 (Input Handling)
**If issues:** Document blockers and troubleshoot before moving forward

---

## 🐛 Common Issues & Solutions

### Issue 1: WorkerW not found on Windows 11 24H2
**Solution:** Try triggering wallpaper change programmatically
```csharp
SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, currentWallpaper, SPIF_UPDATEINIFILE);
Thread.Sleep(100);
// Then try finding WorkerW again
```

### Issue 2: WebView2 Runtime not found
**Solution:** Check if installed, prompt user to download
```csharp
try
{
    var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
}
catch
{
    // Download from: https://go.microsoft.com/fwlink/p/?LinkId=2124703
}
```

### Issue 3: Window appears black
**Solution:** Set window background to transparent
```csharp
// In MainWindow constructor
this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
// Or
this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
```

### Issue 4: Desktop icons not clickable
**Solution:** This is expected for Week 1. Input handling comes in Week 3.

---

## 📚 Learning Resources

### Must-Read Before Coding
1. **WorkerW Technique Explanation**
   - https://www.codeproject.com/Articles/856020/Draw-Behind-Desktop-Icons-in-Windows

2. **WebView2 Getting Started**
   - https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winui

3. **WinUI 3 Windows Management**
   - https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/windowing/windowing-overview

### Reference During Development
- **Win32 API Reference:** https://learn.microsoft.com/en-us/windows/win32/api/
- **WebView2 API Reference:** https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2
- **Lively Wallpaper Code:** https://github.com/rocksdanister/lively (for reference)

---

## 🎯 Success Criteria for Week 1

### Minimum Viable Demo
- [x] Window renders behind desktop icons
- [x] Webpage loads and displays
- [x] Desktop icons remain clickable
- [x] No crashes or major errors

### Stretch Goals
- [ ] Multi-monitor detection
- [ ] Basic error handling
- [ ] Logging system setup
- [ ] Code architecture documented

---

## 📅 Week 2 Preview: Input Handling

Next week you'll implement:
1. Low-level mouse hooks
2. Click forwarding to WebView2
3. Desktop icon detection
4. Basic interaction (clicking links, buttons)

**Preparation:** Review Windows hooks documentation:
https://learn.microsoft.com/en-us/windows/win32/winmsg/hooks

---

## 💡 Pro Tips

1. **Use Spy++ Tool** (included with Visual Studio)
   - Inspect window hierarchy
   - Find Progman and WorkerW windows
   - Verify window attachment

2. **Enable WebView2 DevTools**
   - Right-click on wallpaper → Inspect
   - Debug webpage issues directly

3. **Test on Virtual Desktop**
   - Create new virtual desktop (Win + Tab → New Desktop)
   - Test wallpaper behavior across desktops

4. **Monitor Resource Usage**
   - Use Task Manager → Performance
   - Target: <5% CPU, <300MB RAM

5. **Commit Often**
   - Commit working code at end of each day
   - Use descriptive commit messages
   - Branch for experimental features

---

## 🆘 Getting Help

### If Stuck
1. **Check Lively Wallpaper source code** for similar implementation
2. **Search GitHub issues** in Lively repository
3. **Ask on Stack Overflow** with tags: `winui3`, `webview2`, `win32`
4. **Windows App SDK Discord** community

### Document Issues
Create issues.md in project root:
```markdown
# Development Issues

## Issue 1: WorkerW not found on first run
**Date:** 2025-11-08
**Status:** Resolved
**Solution:** Added retry logic with 100ms delay

## Issue 2: ...
```

---

## ✅ Week 1 Quick Checklist

Copy this to your daily notes:

```
Day 1:
[ ] Install Visual Studio 2022
[ ] Create WinUI 3 project
[ ] Add NuGet packages
[ ] Build successfully

Day 2:
[ ] Create NativeMethods.cs
[ ] Create WorkerWManager.cs
[ ] Test WorkerW detection

Day 3:
[ ] Window renders behind icons
[ ] Document any Windows 11 24H2 issues

Day 4:
[ ] Add WebView2 to project
[ ] Create WebView2Renderer.cs

Day 5:
[ ] Combine WorkerW + WebView2
[ ] Test with multiple websites

Day 6-7:
[ ] Bug fixes and optimization
[ ] Code cleanup
[ ] Prepare Week 2 plan
[ ] Commit and push code
```

---

**Ready to start?** Open Visual Studio and create your first WinUI 3 project! 🚀

See **IMPLEMENTATION_PLAN.md** for comprehensive details on all 10 weeks.
