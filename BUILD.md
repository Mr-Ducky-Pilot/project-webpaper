# WebPaper - Build and Run Guide

## Prerequisites

### Required Software

1. **Windows 11** (Windows 10 21H2+ also supported)

2. **Visual Studio 2022** (Community, Professional, or Enterprise)
   - Download: https://visualstudio.microsoft.com/downloads/

3. **Workloads to Install:**
   - .NET Desktop Development
   - Windows application development
   - Windows App SDK C# Templates

4. **.NET 8 SDK**
   - Usually included with Visual Studio 2022
   - Manual download: https://dotnet.microsoft.com/download/dotnet/8.0

5. **Windows 11 SDK (10.0.22621.0 or later)**
   - Included with Visual Studio workloads

6. **WebView2 Runtime**
   - Pre-installed on Windows 11
   - Manual download (if needed): https://go.microsoft.com/fwlink/p/?LinkId=2124703

---

## Building the Project

### Option 1: Using Visual Studio 2022 (Recommended)

#### Step 1: Open the Project

```powershell
# Navigate to the project directory
cd path\to\project-webpaper

# Open the solution in Visual Studio
start src\WebPaper\WebPaper.csproj
```

Or:
1. Launch Visual Studio 2022
2. Click "Open a project or solution"
3. Navigate to `src/WebPaper/WebPaper.csproj`
4. Click "Open"

#### Step 2: Restore NuGet Packages

Visual Studio should automatically restore packages. If not:

1. Right-click on the solution in Solution Explorer
2. Click "Restore NuGet Packages"
3. Wait for completion

Or use Package Manager Console:
```powershell
Update-Package -reinstall
```

#### Step 3: Build the Project

**Method 1: Using Visual Studio UI**
1. Select "Debug" or "Release" configuration
2. Select "x64" platform (not "Any CPU")
3. Press `Ctrl+Shift+B` or click Build → Build Solution

**Method 2: Using Developer Command Prompt**
```powershell
# Open Developer Command Prompt for VS 2022
# Navigate to project directory
cd src\WebPaper

# Build Debug
msbuild WebPaper.csproj /p:Configuration=Debug /p:Platform=x64

# Build Release
msbuild WebPaper.csproj /p:Configuration=Release /p:Platform=x64
```

#### Step 4: Run the Application

**Method 1: From Visual Studio**
1. Press `F5` (Debug mode) or `Ctrl+F5` (Run without debugging)
2. The wallpaper should appear behind your desktop icons!

**Method 2: From Command Line**
```powershell
# After building
cd src\WebPaper\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64

# Run the executable
.\WebPaper.exe
```

---

### Option 2: Using .NET CLI

#### Build
```powershell
cd src\WebPaper

# Restore packages
dotnet restore

# Build Debug
dotnet build -c Debug

# Build Release
dotnet build -c Release
```

#### Run
```powershell
# Run directly
dotnet run

# Or run the built executable
cd bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64
.\WebPaper.exe
```

---

## First Run Checklist

### ✅ Expected Behavior

1. **Window appears** covering your entire screen
2. **YouTube loads** (or whatever URL is set in MainWindow.xaml.cs line 129)
3. **Desktop icons** remain visible and clickable on top of the webpage
4. **Webpage is interactive** (you can scroll, click links)

### ❌ Common Issues

#### Issue 1: "WebView2 Runtime not found"

**Solution:**
1. Download WebView2 Runtime: https://go.microsoft.com/fwlink/p/?LinkId=2124703
2. Install the "Evergreen Standalone Installer"
3. Restart the application

#### Issue 2: Black screen / Nothing appears

**Possible causes:**
1. **WorkerW not found** (Windows 11 24H2 issue)
   - Check console output for error messages
   - Try restarting Windows Explorer:
     ```powershell
     taskkill /f /im explorer.exe
     start explorer.exe
     ```

2. **WebView2 initialization failed**
   - Check console for error messages
   - Ensure WebView2 Runtime is installed

#### Issue 3: Desktop icons are not clickable

**This means:**
- The WorkerW technique is not working correctly
- Window is covering desktop icons instead of sitting behind them

**Solution:**
- Check if running on Windows 11 24H2 (known issue)
- Try restarting the app
- Check console output for WorkerW-related errors

#### Issue 4: Build errors about missing references

**Solution:**
```powershell
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

Or in Visual Studio:
1. Build → Clean Solution
2. Build → Rebuild Solution

---

## Debugging

### Enable Console Output

The app writes to console, but WinUI 3 apps don't show a console by default.

**To see console output:**

1. **Use Visual Studio Output Window**
   - View → Output
   - Select "Debug" from the dropdown

2. **Run from Command Prompt**
   ```powershell
   cd src\WebPaper\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64
   WebPaper.exe
   ```

3. **Attach a Console (Advanced)**
   Add to `App.xaml.cs` in the `App()` constructor:
   ```csharp
   [System.Runtime.InteropServices.DllImport("kernel32.dll")]
   static extern bool AllocConsole();

   public App()
   {
       AllocConsole(); // Shows console window
       this.InitializeComponent();
   }
   ```

### Debug WorkerW Detection

Uncomment this line in `MainWindow.xaml.cs` after line 127:
```csharp
WorkerWManager.DebugEnumerateWindows();
```

This will print all top-level windows to help troubleshoot.

### Enable WebView2 DevTools

Right-click on the wallpaper and select "Inspect" to open Chrome DevTools.

Or add to `MainWindow.xaml.cs`:
```csharp
// After WebView2 initialization
webView.CoreWebView2.OpenDevToolsWindow();
```

---

## Changing the Default URL

Edit `src/WebPaper/MainWindow.xaml.cs`, line ~129:

```csharp
// Change this URL to whatever you want
webView.CoreWebView2.Navigate("https://www.youtube.com");
```

Suggested URLs to try:
- `https://www.youtube.com` - YouTube homepage
- `https://twitter.com` - Twitter/X feed
- `https://www.reddit.com` - Reddit
- `https://earth.google.com/web/` - Google Earth
- `https://www.shadertoy.com` - Shader artwork
- `http://localhost:3000` - Your local development server

---

## Project Structure

```
project-webpaper/
├── src/
│   └── WebPaper/
│       ├── WebPaper.csproj          # Project file
│       ├── Package.appxmanifest     # MSIX manifest
│       ├── app.manifest             # Application manifest
│       ├── App.xaml                 # Application definition
│       ├── App.xaml.cs              # Application logic
│       ├── MainWindow.xaml          # Main window UI
│       ├── MainWindow.xaml.cs       # Main window logic
│       ├── Native/
│       │   └── NativeMethods.cs     # Windows API P/Invoke
│       ├── Core/
│       │   └── WorkerWManager.cs    # Desktop integration
│       ├── Services/                # (Future: Cookie manager, etc.)
│       ├── Models/                  # (Future: Data models)
│       ├── Properties/
│       │   └── launchSettings.json  # Debug settings
│       └── Assets/                  # App icons and images
├── IMPLEMENTATION_PLAN.md
├── QUICK_START.md
├── DECISIONS.md
├── README.md
└── BUILD.md                         # This file
```

---

## Performance Tips

### Reduce CPU/GPU Usage

1. **Limit frame rate** (edit MainWindow.xaml.cs):
   ```csharp
   // Add after WebView2 initialization
   webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
       "example.com",
       "path\\to\\folder",
       CoreWebView2HostResourceAccessKind.Allow
   );
   ```

2. **Pause when inactive** (future feature - see IMPLEMENTATION_PLAN.md)

### Memory Usage

- Normal: 200-400MB (depends on webpage complexity)
- High usage websites (YouTube, Twitter): 500MB+
- Leaks: If memory keeps growing, there may be a leak (report as bug)

---

## Next Steps

### Week 1 Goals

- [x] Project builds successfully
- [x] Window appears behind desktop icons
- [x] Webpage loads
- [ ] Test with different URLs
- [ ] Test on Windows 11 24H2 (if available)

### Week 2 Goals (see QUICK_START.md)

- [ ] Implement mouse input forwarding
- [ ] Implement keyboard input forwarding
- [ ] Desktop icon detection
- [ ] Full interactivity

---

## Troubleshooting Build Issues

### "The type or namespace name 'WebView2' could not be found"

**Solution:** NuGet packages not restored
```powershell
dotnet restore
```

### "NETSDK1045: The current .NET SDK does not support targeting .NET 8.0"

**Solution:** Install .NET 8 SDK
- Download: https://dotnet.microsoft.com/download/dotnet/8.0

### "Could not load file or assembly 'Microsoft.WindowsAppSDK'"

**Solution:** Install Windows App SDK
```powershell
dotnet workload install microsoft-windows-10-0-19041-0
```

### "MSB4018: The "CreateAppxManifest" task failed unexpectedly"

**Solution:** Install Windows SDK via Visual Studio Installer
- Launch Visual Studio Installer
- Modify → Individual Components
- Check "Windows 11 SDK (10.0.22621.0)"

---

## Clean Build (Nuclear Option)

If nothing works:

```powershell
# Delete all build artifacts
cd src\WebPaper
Remove-Item -Recurse -Force bin, obj

# Clean NuGet cache
dotnet nuget locals all --clear

# Restore and rebuild
dotnet restore
dotnet build -c Debug
```

---

## Platform Requirements

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| **OS** | Windows 10 21H2 | Windows 11 23H2+ |
| **RAM** | 4 GB | 8 GB+ |
| **CPU** | Any x64 | Modern i5/Ryzen 5+ |
| **GPU** | DirectX 11 | DirectX 12 |
| **Disk Space** | 500 MB | 1 GB |

---

## Support

### Getting Help

1. **Check Console Output** - Most errors are logged
2. **Read Error Messages** - They usually tell you what's wrong
3. **Check QUICK_START.md** - Common issues section
4. **GitHub Issues** - Report bugs (when repo is public)

### Reporting Bugs

Include:
1. Windows version (run `winver`)
2. WebView2 version (check in Control Panel)
3. Error messages / console output
4. Steps to reproduce

---

**Ready to run?** Press F5 in Visual Studio and watch your desktop come to life! 🚀
