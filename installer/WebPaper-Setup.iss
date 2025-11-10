; WebPaper InnoSetup Installer Script
; Version: 1.0.0
; Last Updated: 2025-11-10
;
; This script creates a standalone .exe installer for WebPaper
; that includes all necessary files and dependencies.
;
; Prerequisites:
; - Inno Setup 6.3.3 or later (https://jrsoftware.org/isdl.php)
; - App must be built with: dotnet publish -c Release -r win-x64 --self-contained true

#define MyAppName "WebPaper"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Name"
#define MyAppURL "https://github.com/yourusername/project-webpaper"
#define MyAppExeName "WebPaper.exe"
#define PublishDir "E:\project-webpaper\src\WebPaper\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
; ==================== App Information ====================
AppId={{A8F7B2C1-3D4E-5F6A-B7C8-D9E0F1A2B3C4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright=Copyright (C) 2025 {#MyAppPublisher}

; ==================== Installation ====================
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=yes
DisableDirPage=no

; ==================== Output ====================
OutputDir=E:\project-webpaper\installer\output
OutputBaseFilename=WebPaper-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
InternalCompressLevel=max

; ==================== Architecture ====================
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; ==================== UI/UX ====================
WizardStyle=modern
WizardImageFile=compiler:WizClassicImage-IS.bmp
WizardSmallImageFile=compiler:WizClassicSmallImage-IS.bmp
; SetupIconFile={#PublishDir}\Assets\AppIcon.ico  ; TODO: Create AppIcon.ico from PNG assets
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
ShowLanguageDialog=auto

; ==================== Privileges ====================
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; ==================== Requirements ====================
MinVersion=10.0.17763
OnlyBelowVersion=0

; ==================== Signing (Optional) ====================
; SignTool=mysigntool
; SignedUninstaller=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}";
Name: "autostart"; Description: "Start WebPaper automatically when Windows starts"; GroupDescription: "Startup Options:"; Flags: unchecked;
Name: "startmenu"; Description: "Create Start Menu shortcuts"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked;

[Files]
; Copy all published application files
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
; Start Menu shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Transform any webpage into an interactive wallpaper"; Tasks: startmenu
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; Tasks: startmenu

; Desktop shortcut
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "Transform any webpage into an interactive wallpaper"

; Auto-start shortcut
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostart; Comment: "WebPaper - Interactive Wallpaper"

[Registry]
; Save installation path for app to use
Root: HKLM; Subkey: "Software\{#MyAppName}"; Flags: uninsdeletekeyifempty
Root: HKLM; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletevalue

[Run]
; Option to launch app after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallDelete]
; Clean up any files the app created
Type: files; Name: "{localappdata}\WebPaper\*"
Type: dirifempty; Name: "{localappdata}\WebPaper"
Type: filesandordirs; Name: "{localappdata}\WebPaper\WebView2Data"
Type: filesandordirs; Name: "{localappdata}\WebPaper\LoginHelper"

[Code]
// ==================== Helper Functions ====================

// Check if Windows App SDK Runtime is installed
function IsWindowsAppSDKInstalled(): Boolean;
begin
  // Check for Windows App SDK 1.6 in registry
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\WindowsAppRuntime\1.6') or
            RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\WindowsAppRuntime\1.6');
end;

// Check if WebView2 Runtime is installed
function IsWebView2Installed(): Boolean;
var
  Version: String;
begin
  // Check for WebView2 in registry
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
  if not Result then
    Result := RegQueryStringValue(HKLM64, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
end;

// Check if .NET 8 Runtime is installed
function IsDotNet8Installed(): Boolean;
begin
  // For self-contained builds, this is not needed
  // But check anyway for informational purposes
  Result := RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost') or
            RegKeyExists(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost');
end;

// ==================== Installation Checks ====================

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  MissingComponents: String;
  ResultCode: Integer;
begin
  Result := '';
  MissingComponents := '';

  // NOTE: Windows App SDK Runtime check removed because this is a self-contained build
  // All Windows App SDK DLLs are included in the application folder

  // Check for WebView2 (still required - not included in self-contained build)
  if not IsWebView2Installed() then
  begin
    MissingComponents := MissingComponents + '- Microsoft Edge WebView2 Runtime' + #13#10;
  end;

  // If components are missing, show warning
  if MissingComponents <> '' then
  begin
    if MsgBox('The following required component is missing:' + #13#10 + #13#10 +
              MissingComponents + #13#10 +
              'WebPaper requires WebView2 to display web content.' + #13#10 + #13#10 +
              'Would you like to continue with the installation?' + #13#10 +
              '(You can download WebView2 from: https://go.microsoft.com/fwlink/p/?LinkId=2124703)',
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := 'Installation cancelled by user.';
      Exit;
    end;
  end;
end;

// ==================== Post-Installation ====================

procedure CurStepChanged(CurStep: TSetupStep);
var
  MissingComponents: String;
  Instructions: String;
begin
  if CurStep = ssPostInstall then
  begin
    MissingComponents := '';
    Instructions := '';

    // NOTE: Windows App SDK Runtime check removed - this is a self-contained build
    // All runtime DLLs are included in the application folder

    // Check for WebView2 (still required)
    if not IsWebView2Installed() then
    begin
      MissingComponents := MissingComponents + '• Microsoft Edge WebView2 Runtime' + #13#10;
      Instructions := Instructions +
        'Install WebView2 Runtime:' + #13#10 +
        '  Download from: https://go.microsoft.com/fwlink/p/?LinkId=2124703' + #13#10 +
        '  OR run: winget install Microsoft.EdgeWebView2Runtime' + #13#10 + #13#10;
    end;

    // Show instructions if components are missing
    if MissingComponents <> '' then
    begin
      MsgBox('IMPORTANT: WebView2 Required' + #13#10 + #13#10 +
             'WebPaper requires the following component:' + #13#10 +
             MissingComponents + #13#10 +
             'Installation Instructions:' + #13#10 + #13#10 +
             Instructions +
             'WebPaper has been installed, but will not work until WebView2 is installed.' + #13#10 + #13#10 +
             'WebView2 is usually pre-installed on Windows 11, but may need to be downloaded on Windows 10.',
             mbInformation, MB_OK);
    end;
  end;
end;

// ==================== Uninstallation ====================

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  // Kill the app if it's running
  if FileExists(ExpandConstant('{app}\{#MyAppExeName}')) then
  begin
    Exec('taskkill', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserDataPath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Ask if user wants to delete user data
    UserDataPath := ExpandConstant('{localappdata}\WebPaper');
    if DirExists(UserDataPath) then
    begin
      if MsgBox('Do you want to remove all WebPaper user data and settings?' + #13#10 +
                '(This includes saved cookies and configuration)' + #13#10 + #13#10 +
                'Location: ' + UserDataPath,
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(UserDataPath, True, True, True);
      end;
    end;
  end;
end;

// ==================== UI Customization ====================

procedure InitializeWizard();
begin
  // You can add custom pages or UI elements here
  WizardForm.WelcomeLabel2.Caption :=
    'This will install {#MyAppName} on your computer.' + #13#10 + #13#10 +
    '{#MyAppName} transforms any webpage into an interactive desktop wallpaper.' + #13#10 + #13#10 +
    'It is recommended that you close all other applications before continuing.';
end;
