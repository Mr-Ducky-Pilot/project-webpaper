# WebPaper Deployment Guide

This guide explains how to build, package, and distribute WebPaper as an MSIX installer for Windows 10/11.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Building the Project](#building-the-project)
3. [Creating MSIX Package](#creating-msix-package)
4. [Testing the Package](#testing-the-package)
5. [Distribution Options](#distribution-options)
6. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Software

1. **Visual Studio 2022** (17.8 or later)
   - Workload: .NET Desktop Development
   - Workload: Universal Windows Platform development
   - Individual component: Windows 11 SDK (10.0.22621.0 or later)

2. **Windows 10/11**
   - Windows 10 version 1809 (build 17763) or later
   - Windows 11 recommended for development

3. **WebView2 Runtime**
   - Download: https://go.microsoft.com/fwlink/p/?LinkId=2124703
   - Usually pre-installed on Windows 11

### Optional Tools

- **Windows SDK Signing Tools** (for code signing)
- **Microsoft Store Developer Account** (for Store distribution)
- **Azure Code Signing** (for trusted certificates)

---

## Building the Project

### Method 1: Visual Studio GUI

1. **Open Solution:**
   ```
   File → Open → Project/Solution
   Navigate to: src/WebPaper/WebPaper.csproj
   ```

2. **Restore NuGet Packages:**
   ```
   Right-click solution → Restore NuGet Packages
   ```

3. **Select Configuration:**
   ```
   Configuration: Release
   Platform: x64 (or ARM64 for ARM devices)
   ```

4. **Build:**
   ```
   Build → Build Solution (Ctrl+Shift+B)
   ```

5. **Output Location:**
   ```
   src/WebPaper/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/
   ```

### Method 2: Command Line (MSBuild)

```bash
# Navigate to project directory
cd src/WebPaper

# Restore packages
dotnet restore

# Build Release x64
dotnet build -c Release -r win-x64

# Build Release ARM64 (for ARM devices)
dotnet build -c Release -r win-arm64
```

### Method 3: Publish for Deployment

```bash
# Publish self-contained (includes .NET runtime)
dotnet publish -c Release -r win-x64 --self-contained true

# Publish framework-dependent (requires .NET 8 installed)
dotnet publish -c Release -r win-x64 --self-contained false

# Output location
ls src/WebPaper/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/
```

---

## Creating MSIX Package

### Method 1: Visual Studio Packaging Project (Recommended)

#### Step 1: Add Packaging Project

1. **Add New Project:**
   ```
   File → Add → New Project
   Search: "Windows Application Packaging Project"
   Name: WebPaper.Package
   ```

2. **Add Reference to WebPaper:**
   ```
   Right-click WebPaper.Package → Add → Reference
   Select: WebPaper project
   ```

#### Step 2: Configure Package Manifest

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
  </Dependencies>

  <Resources>
    <Resource Language="en-US" />
  </Resources>

  <Applications>
    <Application Id="WebPaper" Executable="WebPaper.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="WebPaper"
        Description="Interactive webpage wallpaper for Windows"
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

#### Step 3: Add App Icons

Create assets in `WebPaper.Package/Assets/`:

| File | Size | Purpose |
|------|------|---------|
| Square44x44Logo.png | 44×44 | App list icon |
| Square150x150Logo.png | 150×150 | Start menu tile |
| Wide310x150Logo.png | 310×150 | Wide tile |
| StoreLogo.png | 50×50 | Store listing |

**Quick Icon Generation:**
```bash
# Using ImageMagick (install first)
magick convert -size 44x44 -background transparent -fill "#0078D4" -font Arial -pointsize 24 -gravity center label:"WP" Square44x44Logo.png
magick convert -size 150x150 -background transparent -fill "#0078D4" -font Arial -pointsize 80 -gravity center label:"WP" Square150x150Logo.png
magick convert -size 310x150 -background transparent -fill "#0078D4" -font Arial -pointsize 80 -gravity center label:"WebPaper" Wide310x150Logo.png
magick convert -size 50x50 -background transparent -fill "#0078D4" -font Arial -pointsize 28 -gravity center label:"WP" StoreLogo.png
```

#### Step 4: Build Package

```
Right-click WebPaper.Package → Publish → Create App Packages
```

**Options:**
- **Sideloading:** For direct distribution (not through Store)
- **Microsoft Store:** For Store submission
- **Architecture:** x64, ARM64, or Both
- **Automatic updates:** Configure update URL if needed

**Output:**
```
WebPaper.Package/AppPackages/WebPaper.Package_1.0.0.0_x64.msix
WebPaper.Package/AppPackages/WebPaper.Package_1.0.0.0_x64_bundle.msixbundle (if multiple architectures)
```

### Method 2: Command Line (MakeAppx)

```bash
# Build in Release mode first
dotnet publish -c Release -r win-x64

# Create package mapping file
cat > mapping.txt << EOF
[Files]
"bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\WebPaper.exe"  "WebPaper.exe"
"bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*.dll"  "*.dll"
"Assets\*"  "Assets\*"
"Package.appxmanifest"  "Package.appxmanifest"
EOF

# Create MSIX package
makeappx pack /f mapping.txt /p WebPaper.msix

# Sign package (required for installation)
signtool sign /fd SHA256 /a /f MyCertificate.pfx /p password WebPaper.msix
```

### Method 3: Using MSIXHero (GUI Tool)

1. **Download MSIXHero:** https://msixhero.net/
2. **Create Package:** Tools → Create Package
3. **Add Files:** Drag publish folder contents
4. **Configure Manifest:** Edit package properties
5. **Build:** File → Save Package As...

---

## Code Signing

### Why Sign?

- **Required** for installation outside Microsoft Store
- Establishes publisher identity
- Prevents tampering warnings
- Enables automatic updates

### Option 1: Self-Signed Certificate (Development/Testing)

```powershell
# Create self-signed certificate
New-SelfSignedCertificate -Type Custom -Subject "CN=YourName" -KeyUsage DigitalSignature -FriendlyName "WebPaper Dev Certificate" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export certificate
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
$cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object {$_.Subject -eq "CN=YourName"}
Export-PfxCertificate -Cert $cert -FilePath WebPaperCert.pfx -Password $password

# Sign package
signtool sign /fd SHA256 /a /f WebPaperCert.pfx /p YourPassword WebPaper.msix
```

**⚠️ Note:** Users must install your certificate as "Trusted Root" before installing the app.

### Option 2: Commercial Certificate (Production)

Purchase code signing certificate from:
- DigiCert
- Sectigo
- GlobalSign
- Certum

**Cost:** ~$100-400/year

**Benefits:**
- Trusted by all Windows systems
- No certificate installation needed
- Professional appearance

### Option 3: Azure Code Signing (Recommended)

**Setup:**
1. Create Azure account
2. Set up Azure Key Vault
3. Upload certificate to Key Vault
4. Configure signing in build pipeline

**Command:**
```bash
# Using Azure SignTool
azuresigntool sign -kvu https://yourvault.vault.azure.net -kvi <app-id> -kvt <tenant-id> -kvs <secret> -kvc <cert-name> WebPaper.msix
```

---

## Testing the Package

### Install Locally

```powershell
# Install MSIX package
Add-AppxPackage -Path WebPaper.msix

# Install with dependencies
Add-AppxPackage -Path WebPaper.msix -DependencyPath dependency1.msix,dependency2.msix

# Install with update
Add-AppxPackage -Path WebPaper.msix -Update

# Uninstall
Remove-AppxPackage WebPaper_1.0.0.0_x64__publisherid
```

### Install Certificate (Self-Signed Only)

```powershell
# Import certificate to Trusted Root
Import-Certificate -FilePath WebPaperCert.cer -CertStoreLocation Cert:\LocalMachine\Root

# Or double-click .cer file:
# 1. Install Certificate
# 2. Local Machine
# 3. Place in: Trusted Root Certification Authorities
```

### Testing Checklist

- [ ] Clean install on fresh Windows 10/11 VM
- [ ] Verify WebView2 auto-installs if missing
- [ ] Test wallpaper attachment to desktop
- [ ] Test input hooks (mouse, keyboard)
- [ ] Test cookie persistence (login/logout)
- [ ] Test performance manager (fullscreen detection)
- [ ] Test multi-monitor setup
- [ ] Test DPI scaling (100%, 125%, 150%)
- [ ] Test uninstall (removes all data)
- [ ] Test upgrade from v1.0 to v1.1

---

## Distribution Options

### Option 1: Direct Download (Sideloading)

**Pros:**
- Full control
- No Store fees
- Instant updates

**Cons:**
- Users must enable sideloading
- No automatic discovery
- Manual certificate trust (if self-signed)

**Distribution:**
1. Host MSIX file on website
2. Provide installation instructions
3. Optionally create `.appinstaller` for updates

**Example .appinstaller:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller Uri="https://yoursite.com/WebPaper.appinstaller" Version="1.0.0.0">
  <MainPackage
    Name="WebPaper"
    Publisher="CN=YourName"
    Version="1.0.0.0"
    Uri="https://yoursite.com/WebPaper_1.0.0.0.msix"
    ProcessorArchitecture="x64" />
  <UpdateSettings>
    <OnLaunch HoursBetweenUpdateChecks="24" />
  </UpdateSettings>
</AppInstaller>
```

### Option 2: Microsoft Store

**Pros:**
- Trusted distribution
- Automatic updates
- Built-in discovery
- No certificate needed

**Cons:**
- $19 registration fee
- Review process (2-3 days)
- 15% revenue share (if paid app)
- Strict requirements

**Steps:**
1. Create Microsoft Partner Center account
2. Reserve app name
3. Upload MSIX package
4. Fill Store listing (description, screenshots, etc.)
5. Submit for certification
6. Wait for approval

**Requirements:**
- Age rating disclosure
- Privacy policy
- App screenshots (1920×1080 minimum)
- Store logos
- Description (10-10,000 characters)

### Option 3: GitHub Releases

**Setup:**
```bash
# Create release on GitHub
git tag v1.0.0
git push origin v1.0.0

# Upload WebPaper.msix to GitHub Release
# Users can download directly
```

**Benefits:**
- Version control
- Community engagement
- Free hosting
- Automatic changelog

### Option 4: Winget (Windows Package Manager)

**Create manifest:**
```yaml
# WebPaper.yaml
PackageIdentifier: YourName.WebPaper
PackageVersion: 1.0.0
PackageName: WebPaper
Publisher: Your Name
License: MIT
ShortDescription: Interactive webpage wallpaper
PackageUrl: https://github.com/yourname/webpaper
Installers:
  - Architecture: x64
    InstallerType: msix
    InstallerUrl: https://github.com/yourname/webpaper/releases/download/v1.0.0/WebPaper.msix
    InstallerSha256: <sha256-hash>
    SignatureSha256: <signature-hash>
```

**Submit to winget-pkgs:**
```bash
# Fork microsoft/winget-pkgs
# Add manifest to manifests/y/YourName/WebPaper/1.0.0/
# Create pull request
```

**User installation:**
```powershell
winget install WebPaper
```

---

## Automatic Updates

### Using AppInstaller

Create update server with:
- `.appinstaller` file pointing to latest MSIX
- HTTPS hosting required
- Update check frequency configurable

**Example:**
```xml
<UpdateSettings>
  <OnLaunch HoursBetweenUpdateChecks="24" ShowPrompt="true" UpdateBlocksActivation="false" />
  <AutomaticBackgroundTask />
</UpdateSettings>
```

### Using Microsoft Store

- Automatic for Store apps
- No configuration needed
- Updates pushed to all users within 24-48 hours

### Custom Update Checker (Optional)

Add to App.xaml.cs:
```csharp
private async Task CheckForUpdatesAsync()
{
    using var client = new HttpClient();
    var response = await client.GetStringAsync("https://yoursite.com/version.txt");
    var latestVersion = Version.Parse(response.Trim());
    var currentVersion = typeof(App).Assembly.GetName().Version;

    if (latestVersion > currentVersion)
    {
        // Show update notification
        var dialog = new ContentDialog
        {
            Title = "Update Available",
            Content = $"Version {latestVersion} is available. Download from website?",
            PrimaryButtonText = "Download",
            CloseButtonText = "Later"
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            Process.Start("https://yoursite.com/download");
        }
    }
}
```

---

## Troubleshooting

### Issue 1: Certificate Error

**Error:** "Install failed. Please contact the software publisher."

**Solution:**
```powershell
# Install certificate as Trusted Root
Import-Certificate -FilePath WebPaperCert.cer -CertStoreLocation Cert:\LocalMachine\Root
```

### Issue 2: WebView2 Missing

**Error:** "WebView2 Runtime not found"

**Solution:**
- Include WebView2 bootstrapper in package
- Or add dependency in manifest:
```xml
<Dependencies>
  <PackageDependency Name="Microsoft.WebView2.Runtime" MinVersion="1.0.2651.64" Publisher="CN=Microsoft Corporation" />
</Dependencies>
```

### Issue 3: Build Fails

**Error:** "SDK not found"

**Solution:**
1. Install Windows SDK 10.0.22621.0 or later
2. Update Visual Studio to latest version
3. Repair Visual Studio installation

### Issue 4: Package Too Large

**Problem:** MSIX over 100MB

**Solution:**
- Use framework-dependent build (exclude .NET runtime)
- Enable MSIX compression
- Remove debug symbols
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

### Issue 5: Sideloading Disabled

**Error:** "Sideloading not enabled"

**Solution:**
```powershell
# Enable sideloading (requires admin)
Set-ItemProperty -Path HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock -Name AllowAllTrustedApps -Value 1

# Or via Settings:
# Settings → Update & Security → For Developers → Sideload apps
```

---

## Build Pipeline (CI/CD)

### GitHub Actions Example

```yaml
name: Build and Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies
      run: dotnet restore src/WebPaper/WebPaper.csproj

    - name: Build
      run: dotnet build src/WebPaper/WebPaper.csproj -c Release --no-restore

    - name: Publish
      run: dotnet publish src/WebPaper/WebPaper.csproj -c Release -r win-x64 --self-contained false -o publish/

    - name: Create MSIX
      run: |
        makeappx pack /d publish/ /p WebPaper.msix
        signtool sign /fd SHA256 /a /f ${{ secrets.CERTIFICATE_PATH }} /p ${{ secrets.CERTIFICATE_PASSWORD }} WebPaper.msix

    - name: Create Release
      uses: actions/create-release@v1
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      with:
        tag_name: ${{ github.ref }}
        release_name: Release ${{ github.ref }}
        draft: false
        prerelease: false

    - name: Upload MSIX
      uses: actions/upload-release-asset@v1
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      with:
        upload_url: ${{ steps.create_release.outputs.upload_url }}
        asset_path: ./WebPaper.msix
        asset_name: WebPaper.msix
        asset_content_type: application/octet-stream
```

---

## Versioning

### Semantic Versioning

Follow SemVer: `MAJOR.MINOR.PATCH`

- **MAJOR:** Breaking changes
- **MINOR:** New features (backward compatible)
- **PATCH:** Bug fixes

**Example:**
```
1.0.0 - Initial release
1.0.1 - Bug fix: Cookie encryption issue
1.1.0 - New feature: System tray icon
2.0.0 - Breaking: New settings format
```

### Update Version

**In WebPaper.csproj:**
```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <FileVersion>1.0.0.0</FileVersion>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
</PropertyGroup>
```

**In Package.appxmanifest:**
```xml
<Identity Name="WebPaper" Version="1.0.0.0" Publisher="CN=YourName" />
```

---

## Security Best Practices

### Code Signing

- ✅ **Always** sign packages before distribution
- ✅ Use timestamping to extend signature validity
- ✅ Store certificates securely (Azure Key Vault recommended)
- ✅ Use strong passwords for certificate files

### Package Integrity

```powershell
# Generate SHA256 hash for verification
Get-FileHash WebPaper.msix -Algorithm SHA256 | Format-List

# Verify signature
Get-AuthenticodeSignature WebPaper.msix
```

### Privacy

- Document what data is collected
- Explain cookie storage (DPAPI encrypted)
- Provide uninstall instructions
- Clear all data on uninstall

---

## Performance Optimization

### Reduce Package Size

1. **Framework-Dependent Build:**
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained false
   ```
   - Requires .NET 8 Runtime installed
   - Reduces package from ~150MB to ~5MB

2. **Trim Unused Code:**
   ```xml
   <PropertyGroup>
     <PublishTrimmed>true</PublishTrimmed>
     <TrimMode>link</TrimMode>
   </PropertyGroup>
   ```

3. **Compression:**
   ```bash
   makeappx pack /d publish/ /p WebPaper.msix /l  # Enables compression
   ```

### Optimize Startup

- Lazy-load non-critical components
- Use async initialization
- Pre-compile WebView2 scripts

---

## Resources

### Official Documentation

- [MSIX Packaging](https://learn.microsoft.com/en-us/windows/msix/)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- [WebView2 SDK](https://learn.microsoft.com/en-us/microsoft-edge/webview2/)

### Tools

- [MSIX Hero](https://msixhero.net/) - MSIX package manager
- [MakeAppx](https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool) - Command-line packager
- [SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool) - Code signing tool

### Community

- [MSIX Reddit](https://www.reddit.com/r/MSIX/)
- [Windows Dev Discord](https://discord.gg/windowsdev)
- [Stack Overflow - MSIX Tag](https://stackoverflow.com/questions/tagged/msix)

---

## Quick Command Reference

```bash
# Build
dotnet build -c Release -r win-x64

# Publish
dotnet publish -c Release -r win-x64 --self-contained false

# Create MSIX
makeappx pack /d publish/ /p WebPaper.msix

# Sign MSIX
signtool sign /fd SHA256 /a /f cert.pfx /p password WebPaper.msix

# Install locally
Add-AppxPackage -Path WebPaper.msix

# Uninstall
Remove-AppxPackage WebPaper_1.0.0.0_x64__publisherid

# Verify signature
Get-AuthenticodeSignature WebPaper.msix

# Calculate hash
Get-FileHash WebPaper.msix -Algorithm SHA256
```

---

**For further assistance, see the project README.md or open an issue on GitHub.**
