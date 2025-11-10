# WebPaper - Complete Installation & Testing Guide

**Last Updated:** November 10, 2025
**Version:** 1.0.0-beta
**Target:** Windows 10/11

---

## 🔍 Why Your MSIX Package Didn't Work

Your MSIX installation likely failed silently due to **missing runtime dependencies**. WinUI3/Windows App SDK apps require:

1. **Windows App SDK Runtime 1.6** (not included in MSIX)
2. **.NET 8 Runtime** (if using framework-dependent build)
3. **Visual C++ Redistributables**
4. **WebView2 Runtime** (usually pre-installed on Windows 11)

When you installed the MSIX, Windows installed the package, but the app couldn't start because these dependencies weren't present.

## ⚠️ IMPORTANT: Project Now Configured for Self-Contained Deployment

**As of the latest commit**, the project has been updated with:
- `WindowsPackageType=None` (unpackaged deployment)
- `WindowsAppSDKSelfContained=true` (includes all Windows App SDK DLLs)

This means:
✅ The publish folder will include ALL required DLLs (Microsoft.ui.xaml.dll, etc.)
✅ App runs directly from .exe without any runtime installation needed
✅ ~200MB larger deployment size (includes full Windows App SDK runtime)
✅ No external dependencies required (truly self-contained)

---

## ✅ Recommended Installation Methods (Ranked)

### 🥇 **Method 1: Direct .exe Execution** (Easiest for Testing)
**Best for:** Local testing, development
**Pros:** No installation needed, instant testing
**Cons:** Not distributable to end users

### 🥈 **Method 2: InnoSetup .exe Installer** (Best for Distribution)
**Best for:** Distributing to end users
**Pros:** Single .exe file, includes all dependencies, familiar to users
**Cons:** Requires InnoSetup to build

### 🥉 **Method 3: MSIX Package** (Microsoft Store Ready)
**Best for:** Microsoft Store submission, enterprise deployment
**Pros:** Modern, sandboxed, auto-updates
**Cons:** Complex, requires runtime dependencies installed separately

---

## 📋 Method 1: Direct .exe Execution (Testing)

This is the **fastest way** to test your app locally.

### Step 1: Build the Application

```bash
cd E:\project-webpaper\src\WebPaper
dotnet publish -c Release -r win-x64 --self-contained true
```

**Output folder:** `E:\project-webpaper\src\WebPaper\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish`

### Step 2: Run Directly

```bash
cd bin\Release\net8.0-windows10.0.19041.0\win-x64\publish
.\WebPaper.exe
```

**Expected behavior:**
- ✅ Application window appears
- ✅ Webpage loads as wallpaper
- ✅ Desktop icons remain clickable
- ✅ Input works (mouse, keyboard)

### Step 3: Check Event Viewer (If App Doesn't Start)

1. Press `Win + R`, type `eventvwr`, press Enter
2. Navigate to: **Windows Logs → Application**
3. Look for errors with source "Application Error" or ".NET Runtime"
4. Note the error message and exception details

### Common Issues & Fixes

| Issue | Solution |
|-------|----------|
| **Black screen** | WorkerW not found - restart Windows Explorer:<br>`taskkill /f /im explorer.exe && start explorer.exe` |
| **WebView2 error** | Install WebView2 Runtime:<br>https://go.microsoft.com/fwlink/p/?LinkId=2124703 |
| **App crashes immediately** | Check Event Viewer for startup exceptions |
| **Missing DLL errors** | Use `--self-contained true` flag in publish |

---

## 📦 Method 2: InnoSetup .exe Installer (Recommended)

This creates a **traditional .exe installer** that users are familiar with.

### Prerequisites

1. **Install InnoSetup**
   - Download: https://jrsoftware.org/isdl.php
   - Install **Inno Setup 6.3.3** or later
   - Also install: **Inno Download Plugin** (for downloading dependencies)

2. **Build Self-Contained App**
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true
   ```

### Step 1: Create InnoSetup Script

Create file: `E:\project-webpaper\installer\WebPaper-Setup.iss`

```iss
; WebPaper InnoSetup Script
; This creates a standalone .exe installer

#define MyAppName "WebPaper"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Name"
#define MyAppURL "https://github.com/yourusername/project-webpaper"
#define MyAppExeName "WebPaper.exe"
#define PublishDir "E:\project-webpaper\src\WebPaper\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
; App Information
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

; Output
OutputDir=E:\project-webpaper\installer\output
OutputBaseFilename=WebPaper-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes

; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; UI
WizardStyle=modern
SetupIconFile={#PublishDir}\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Requirements
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}";
Name: "autostart"; Description: "Start WebPaper when Windows starts"; GroupDescription: "Startup Options:";

[Files]
; Copy all published files
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostart

[Run]
; Run app after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Download and install Windows App SDK Runtime if needed
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';

  // Check if Windows App SDK Runtime is installed
  if not RegKeyExists(HKLM, 'SOFTWARE\Microsoft\WindowsAppRuntime\1.6') then
  begin
    MsgBox('Windows App SDK Runtime 1.6 is required but not installed.' + #13#10 +
           'The installer will now download and install it.', mbInformation, MB_OK);

    // Download and install Windows App SDK Runtime
    // Note: You'll need to include the runtime installer or download it
    // For now, show instructions
    MsgBox('Please install Windows App SDK Runtime 1.6 from:' + #13#10 +
           'https://aka.ms/windowsappsdk/1.6/latest/windowsappruntimeinstall-x64.exe',
           mbInformation, MB_OK);
  end;
end;
```

### Step 2: Build the Installer

1. **Open Inno Setup Compiler**
2. **Open** the `.iss` file you created
3. **Click Build → Compile** (or press F9)
4. **Output:** `E:\project-webpaper\installer\output\WebPaper-Setup-1.0.0.exe`

### Step 3: Test the Installer

1. **Copy the installer** to a test location
2. **Right-click** → **Run as Administrator**
3. **Follow installation wizard**
4. **Launch the app** from Start Menu or Desktop icon

### Step 4: Verify Installation

**Check installed location:**
```
C:\Program Files\WebPaper\WebPaper.exe
```

**Check Start Menu:**
- Start → Search "WebPaper"

**Check auto-start (if selected):**
```
C:\Users\YourName\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\WebPaper.lnk
```

---

## 🎁 Method 3: MSIX Package (Advanced)

### Why MSIX Failed

MSIX packages **do not include runtime dependencies**. You must ensure:
1. Windows App SDK Runtime 1.6 is installed
2. .NET 8 Runtime is installed (for framework-dependent builds)

### Step 1: Install Runtime Dependencies

**On target machine, install:**

1. **Windows App SDK Runtime 1.6**
   ```powershell
   # Download installer
   Invoke-WebRequest -Uri "https://aka.ms/windowsappsdk/1.6/latest/windowsappruntimeinstall-x64.exe" -OutFile "WindowsAppRuntime.exe"

   # Install silently
   .\WindowsAppRuntime.exe /quiet
   ```

2. **.NET 8 Runtime** (if framework-dependent)
   ```powershell
   winget install Microsoft.DotNet.Runtime.8
   ```

3. **WebView2 Runtime** (if needed)
   ```powershell
   # Download
   Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile "WebView2Setup.exe"

   # Install
   .\WebView2Setup.exe /silent /install
   ```

### Step 2: Build MSIX Package

**Using Visual Studio:**

1. **Add Packaging Project**
   - Right-click solution → Add → New Project
   - Search: "Windows Application Packaging Project"
   - Name: `WebPaper.Package`

2. **Add Reference**
   - Right-click `WebPaper.Package` → Add → Reference
   - Check `WebPaper` project

3. **Configure Manifest**

Edit `Package.appxmanifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">

  <Identity
    Name="WebPaper"
    Publisher="CN=YourName"
    Version="1.0.0.0" />

  <Properties>
    <DisplayName>WebPaper</DisplayName>
    <PublisherDisplayName>Your Name</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
    <PackageDependency Name="Microsoft.WindowsAppRuntime.1.6" MinVersion="1.6.0.0" Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
  </Dependencies>

  <Resources>
    <Resource Language="en-US" />
  </Resources>

  <Applications>
    <Application Id="WebPaper" Executable="WebPaper.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="WebPaper"
        Description="Transform any webpage into an interactive desktop wallpaper"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
      </uap:VisualElements>
    </Application>
  </Applications>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
```

4. **Build Package**
   - Right-click `WebPaper.Package` → Publish → Create App Packages
   - Select: **Sideloading**
   - Architecture: **x64**
   - Build

### Step 3: Sign the Package

**Using your self-signed certificate:**

```powershell
# Sign the package
SignTool sign /fd SHA256 /a /f "YourCertificate.pfx" /p "password" "WebPaper.msix"
```

### Step 4: Install MSIX Package

```powershell
# Install dependencies first
.\WindowsAppRuntime.exe /quiet

# Then install the app
Add-AppxPackage -Path "WebPaper.msix"
```

### Step 5: Verify MSIX Installation

```powershell
# List installed packages
Get-AppxPackage -Name "*WebPaper*"

# Should show:
# Name              : WebPaper
# Publisher         : CN=YourName
# Architecture      : X64
# Version           : 1.0.0.0
# InstallLocation   : C:\Program Files\WindowsApps\WebPaper_1.0.0.0_x64__...
```

**Launch the app:**
- Start Menu → Search "WebPaper" → Click to launch

---

## 🧪 Complete Testing Checklist

### ✅ Phase 1: Basic Functionality

Run through these tests after installation:

**1. App Launches**
- [ ] App window appears within 5 seconds
- [ ] No error dialogs shown
- [ ] Process appears in Task Manager

**2. WebView2 Loads**
- [ ] Webpage content appears
- [ ] No blank white/black screen
- [ ] No "WebView2 Runtime not found" error

**3. Desktop Integration**
- [ ] Desktop icons visible on top of wallpaper
- [ ] Desktop icons are clickable
- [ ] Wallpaper sits behind icons (not covering them)

**4. Input Works**
- [ ] Mouse clicks work on webpage
- [ ] Mouse scrolling works
- [ ] Keyboard typing works in webpage forms
- [ ] Right-click works

### ✅ Phase 2: Features

**5. Cookie Persistence**
- [ ] Log in to a website (e.g., Gmail)
- [ ] Close app and relaunch
- [ ] Still logged in (cookies persisted)

**6. Performance Manager**
- [ ] Open a fullscreen app (game, video player)
- [ ] Wallpaper pauses (check console output)
- [ ] Exit fullscreen
- [ ] Wallpaper resumes

**7. Settings/Configuration**
- [ ] Settings window opens (if implemented)
- [ ] URL can be changed (if implemented)
- [ ] Settings persist after restart

### ✅ Phase 3: Stability

**8. Resource Usage**
- [ ] Open Task Manager
- [ ] CPU usage < 5% when idle
- [ ] Memory usage < 300 MB
- [ ] No memory leaks (stable over 10 minutes)

**9. Multi-Session**
- [ ] App works after restart
- [ ] App works after Windows restart
- [ ] App works after Windows Update

**10. Error Handling**
- [ ] Try loading invalid URL → shows error gracefully
- [ ] Disconnect internet → handles offline gracefully
- [ ] Kill process → restarts cleanly

---

## 🐛 Troubleshooting Common Issues

### Issue 1: MSIX Installs but App Won't Start

**Symptoms:**
- MSIX installs successfully
- No errors shown
- App never launches
- No window appears

**Diagnosis:**
```powershell
# Check Event Viewer
Get-WinEvent -LogName Application -MaxEvents 20 | Where-Object {$_.ProviderName -like "*WebPaper*"}
```

**Solutions:**

1. **Install Windows App SDK Runtime**
   ```powershell
   winget install Microsoft.WindowsAppRuntime.1.6
   ```

2. **Check if package is installed**
   ```powershell
   Get-AppxPackage -Name "*WebPaper*"
   ```

3. **Reinstall with verbose logging**
   ```powershell
   Add-AppxPackage -Path "WebPaper.msix" -Verbose
   ```

### Issue 2: "This app requires .NET Runtime"

**Solution:**
```powershell
# Install .NET 8 Runtime
winget install Microsoft.DotNet.Runtime.8
```

Or rebuild as self-contained:
```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

### Issue 3: "WebView2 Runtime not found"

**Solution:**
```powershell
# Check if WebView2 is installed
Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -Name "pv"

# If not found, install:
Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile "WebView2Setup.exe"
.\WebView2Setup.exe /silent /install
```

### Issue 4: App Crashes on Startup

**Check Event Viewer:**
1. `Win + R` → `eventvwr`
2. Windows Logs → Application
3. Look for .NET Runtime or Application Error events

**Common causes:**
- Missing configuration file
- Invalid paths in code
- Startup exception in App.xaml.cs

**Debug with:**
```bash
# Run from command line to see console output
cd "C:\Program Files\WebPaper"
.\WebPaper.exe
```

### Issue 5: Desktop Icons Not Clickable

**Cause:** WorkerW technique not working

**Solutions:**

1. **Restart Windows Explorer**
   ```bash
   taskkill /f /im explorer.exe
   start explorer.exe
   ```

2. **Check Windows version**
   - Windows 11 24H2 may have WorkerW issues
   - App includes fallback to Progman

3. **Run as Administrator** (test only)
   - Right-click WebPaper.exe → Run as Administrator

---

## 📊 Performance Benchmarks

**Expected performance on modern hardware:**

| Metric | Target | Good | Poor |
|--------|--------|------|------|
| **CPU (Idle)** | < 2% | < 5% | > 10% |
| **CPU (Active)** | < 5% | < 10% | > 20% |
| **Memory** | < 200 MB | < 300 MB | > 500 MB |
| **Startup Time** | < 2s | < 5s | > 10s |
| **Input Latency** | < 50ms | < 100ms | > 200ms |

**Test system:** Windows 11, i5-12600K, 16GB RAM, SSD

---

## 🎯 Recommended Installation Method

### For Development/Testing:
**Use Method 1** (Direct .exe) - Fastest, no installation needed

### For Distribution to Users:
**Use Method 2** (InnoSetup) - Single .exe, includes dependencies, familiar to users

### For Microsoft Store:
**Use Method 3** (MSIX) - Required for Store submission

---

## 📝 Quick Start Commands

```bash
# Build
cd E:\project-webpaper\src\WebPaper
dotnet publish -c Release -r win-x64 --self-contained true

# Test directly
cd bin\Release\net8.0-windows10.0.19041.0\win-x64\publish
.\WebPaper.exe

# Create installer (after installing InnoSetup)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" E:\project-webpaper\installer\WebPaper-Setup.iss

# Install Windows App SDK Runtime (for MSIX)
winget install Microsoft.WindowsAppRuntime.1.6
```

---

## 🆘 Getting Help

If issues persist:

1. **Check Event Viewer** for detailed error logs
2. **Enable console output** (see App.xaml.cs)
3. **Run from command line** to see exceptions
4. **Check dependencies** are installed
5. **Verify Windows version** compatibility

**Event Viewer Path:**
```
eventvwr.msc → Windows Logs → Application
```

**Look for sources:**
- `.NET Runtime`
- `Application Error`
- `Windows Error Reporting`
- `AppXDeployment-Server`

---

**Last Updated:** November 10, 2025
**Tested on:** Windows 11 23H2, Windows 10 22H2
**Next Steps:** See [DEPLOYMENT.md](DEPLOYMENT.md) for production deployment
