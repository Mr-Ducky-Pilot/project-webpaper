# Contributing to WebPaper

Thank you for your interest in contributing to WebPaper! This guide will help you get started.

---

## 🤝 Ways to Contribute

### 1. Report Bugs 🐛
Found a bug? Please report it!

- Use our [Bug Report Template](../../issues/new?template=bug_report.md)
- Provide clear steps to reproduce
- Include system information and logs

### 2. Suggest Features 💡
Have an idea to make WebPaper better?

- Use our [Feature Request Template](../../issues/new?template=feature_request.md)
- Explain the use case
- Describe the proposed solution

### 3. Improve Documentation 📝
Documentation can always be better!

- Fix typos or unclear explanations
- Add examples and screenshots
- Improve code comments
- Write tutorials or guides

### 4. Submit Code 💻
Want to contribute code? Awesome!

- Fix bugs from the issue tracker
- Implement requested features
- Improve performance
- Add tests

### 5. Test & Provide Feedback 🧪
Help us test WebPaper!

- Test on different Windows versions
- Try different websites and report compatibility
- Provide feedback on UX/UI
- Share your use cases

---

## 🔧 Development Setup

### Prerequisites

1. **Visual Studio 2022** (v17.8 or later)
   - Download: https://visualstudio.microsoft.com/

2. **Workloads to Install:**
   - .NET Desktop Development
   - Universal Windows Platform Development

3. **.NET 8 SDK**
   - Verify: `dotnet --version` (should be 8.0+)

4. **Windows 11 SDK**
   - Minimum: 10.0.22621.0
   - Included with Visual Studio workload

### Getting Started

```bash
# 1. Fork the repository on GitHub
# 2. Clone your fork
git clone https://github.com/YOUR_USERNAME/project-webpaper.git
cd project-webpaper

# 3. Create a feature branch
git checkout -b feature/your-feature-name

# 4. Open in Visual Studio
start src/WebPaper/WebPaper.csproj

# 5. Build and run
# Press F5 in Visual Studio or:
dotnet build
dotnet run --project src/WebPaper/WebPaper.csproj
```

---

## 📋 Code Standards

### C# Style Guide

**Follow the existing code style:**

```csharp
// ✅ Good: PascalCase for public methods
public void InstallHooks()
{
    // camelCase for private fields
    _mouseHookId = SetWindowsHookEx(...);
}

// ✅ Good: XML documentation for public APIs
/// <summary>
/// Installs low-level mouse and keyboard hooks
/// </summary>
/// <param name="webView">WebView2 control instance</param>
public void InstallHooks(CoreWebView2 webView)

// ✅ Good: Descriptive variable names
var wallpaperWindow = FindWindow("WorkerW", null);

// ❌ Bad: Single letter variables (except loop counters)
var w = FindWindow("WorkerW", null);
```

### Error Handling

```csharp
// ✅ Always use structured logging with Serilog
try
{
    // Your code here
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to perform operation");
    // Handle gracefully
}

// ❌ Don't use Console.WriteLine in production code
Console.WriteLine($"Error: {ex.Message}");  // ❌
```

### Async/Await

```csharp
// ✅ Use async/await properly
public async Task InitializeAsync()
{
    await LoadConfigurationAsync();
    await RestoreCookiesAsync();
}

// ❌ Don't block on async code
Task.Run(() => LoadConfiguration()).Wait();  // ❌
```

### Resource Management

```csharp
// ✅ Always dispose unmanaged resources
public void Dispose()
{
    if (!_disposed)
    {
        UnhookWindowsHookEx(_mouseHookId);
        _webView = null;
        _disposed = true;
    }
}
```

---

## 🧪 Testing

### Manual Testing Checklist

Before submitting a pull request, test the following:

**Basic Functionality:**
- [ ] Application starts without errors
- [ ] Webpage loads and displays correctly
- [ ] Mouse clicks work on the webpage
- [ ] Keyboard typing works in input fields
- [ ] Mouse wheel scroll works
- [ ] Desktop icons remain clickable

**Settings & Configuration:**
- [ ] Settings window opens from system tray
- [ ] URL can be changed and saved
- [ ] Settings persist after restart

**Edge Cases:**
- [ ] Application handles bad URLs gracefully
- [ ] Works on Windows 10 and Windows 11
- [ ] Works on different screen resolutions
- [ ] Handles internet disconnection
- [ ] Recovers from webpage crashes

**Performance:**
- [ ] CPU usage <2% when idle
- [ ] No memory leaks (run for 1+ hour)
- [ ] Auto-pause works during fullscreen apps

---

## 📝 Pull Request Process

### Before Submitting

1. **Create an Issue First** (for major changes)
   - Discuss the approach
   - Get feedback from maintainers
   - Avoid wasted effort

2. **Keep Changes Focused**
   - One feature or fix per PR
   - Avoid unrelated changes
   - Keep diffs small and reviewable

3. **Update Documentation**
   - Add/update XML comments
   - Update relevant wiki pages
   - Include examples if needed

4. **Test Thoroughly**
   - Follow the testing checklist above
   - Test on Windows 10 and 11 if possible
   - Verify no regressions

### Submitting Your PR

1. **Push to Your Fork:**
   ```bash
   git add .
   git commit -m "Add: Brief description of your change"
   git push origin feature/your-feature-name
   ```

2. **Create Pull Request:**
   - Go to the original repository
   - Click "New Pull Request"
   - Select your fork and branch
   - Fill in the PR template

3. **PR Description Should Include:**
   - What problem does this solve?
   - What changes were made?
   - How was it tested?
   - Screenshots/GIFs (for UI changes)
   - Related issues (e.g., "Fixes #123")

### Example PR Description

```markdown
## Description
Adds support for custom keyboard shortcuts to open settings window.

## Changes
- Added global keyboard hook for Ctrl+Shift+S
- Implemented settings window activation on shortcut
- Added configuration option to enable/disable shortcut

## Testing
- Tested on Windows 10 21H2 and Windows 11 23H2
- Verified shortcut works when wallpaper is active
- Verified shortcut doesn't conflict with other apps
- Tested with settings window already open (no duplicate windows)

## Screenshots
![Settings Shortcut](screenshot.png)

Fixes #45
```

### Code Review

- Be patient - reviews may take a few days
- Respond to feedback constructively
- Make requested changes promptly
- Squash commits if requested

---

## 🎯 Good First Issues

Looking for something to work on? Check these labels:

- **`good first issue`** - Perfect for newcomers
- **`help wanted`** - We need help with these
- **`documentation`** - Improve docs
- **`enhancement`** - New features

---

## 🐛 Debugging Tips

### Enable Debug Logging

In `App.xaml.cs`:
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()  // Change from Information to Debug
    .WriteTo.File(logPath, ...)
    .CreateLogger();
```

### View Logs

```powershell
# Open log directory
explorer %LOCALAPPDATA%\WebPaper\Logs

# Tail logs in real-time
Get-Content "%LOCALAPPDATA%\WebPaper\Logs\webpaper.log" -Wait
```

### Common Issues

**"Hooks not working"**
- Check if hooks are installed: Look for "Input hooks installed successfully" in logs
- Verify `_mouseHookId` and `_keyboardHookId` are not zero
- Check if another app is blocking hooks

**"WorkerW not found"**
- Windows 11 24H2 changed WorkerW behavior
- Check logs for "WorkerW handle" or "Progman fallback"
- Verify `WorkerWManager.cs` is using fallback correctly

**"WebView2 not loading"**
- Verify WebView2 Runtime is installed
- Check `%LOCALAPPDATA%\WebPaper\WebView2Data\` permissions
- Look for WebView2 errors in logs

---

## 📚 Resources

### Microsoft Documentation
- [WebView2 Docs](https://docs.microsoft.com/en-us/microsoft-edge/webview2/)
- [WinUI 3 Docs](https://docs.microsoft.com/en-us/windows/apps/winui/)
- [Win32 API Docs](https://docs.microsoft.com/en-us/windows/win32/api/)

### WebPaper Documentation
- [Technical Overview](Technical-Overview) - Architecture and design
- [User Guide](User-Guide) - End-user documentation
- [FAQ](FAQ) - Frequently asked questions

### Community
- [GitHub Issues](../../issues) - Bug reports and features
- [GitHub Discussions](../../discussions) - General discussions
- Email: omprakashj2010@gmail.com (for collaborations)

---

## ⚖️ License & Attribution

By contributing to WebPaper, you agree that your contributions will be licensed under the same license as the project (Personal & Hobbyist Use License).

**Important:**
- Commercial use requires separate licensing
- Contributors retain copyright to their contributions
- Attribution to original author (Omprakash J / MrDuck) must be maintained

---

## 🙏 Thank You!

Every contribution, no matter how small, makes WebPaper better. Whether you're fixing a typo, reporting a bug, or implementing a major feature — **thank you for being part of the WebPaper community!** ✨

---

**Questions?** Feel free to [open a discussion](../../discussions) or reach out to omprakashj2010@gmail.com

**Happy coding!** 🚀
