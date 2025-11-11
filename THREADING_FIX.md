# 🔧 WebPaper Threading Fix - DispatcherQueue Implementation

**Date:** November 11, 2025
**Issue:** COMException when calling ExecuteScriptAsync from hook callback thread
**Root Cause:** WebView2 COM objects require UI thread affinity (STA threading model)
**Solution:** Implement DispatcherQueue marshaling pattern (WinUI 3 best practice)

---

## 🎯 Problem Analysis

### The Threading Violation

When a user clicked on the wallpaper, the application flow was:

1. **Mouse Hook Callback** (Background Thread)
   - Low-level mouse hook receives WM_LBUTTONDOWN
   - Calls `ForwardMouseEvent()` → `SimulateClickViaJavaScript()`

2. **JavaScript Execution Attempt** (Background Thread)
   - Tries to call `_webView.ExecuteScriptAsync(script)`
   - **FAILS** with COMException

3. **Error Message:**
   ```
   JavaScript execution FAILED: This method can only be called from the thread that created the object.
   Exception type: COMException
   ```

### Why This Happened

**WebView2 COM Threading Requirements:**
- WebView2 (Microsoft Edge Chromium) uses COM (Component Object Model)
- COM objects are created on a specific thread (UI thread in this case)
- COM uses STA (Single-Threaded Apartment) threading model
- **Rule:** COM objects MUST be accessed from the thread that created them

**Our Hook Callback Runs on Different Thread:**
- Windows hook callbacks execute on the thread that installed the hook
- This is NOT the UI thread
- Result: Threading violation when accessing WebView2

---

## ✅ Solution: DispatcherQueue Marshaling

### What is DispatcherQueue?

**DispatcherQueue** is the WinUI 3 / Windows App SDK mechanism for:
- Marshaling calls from background threads to the UI thread
- Queuing work items to execute on specific threads
- Modern replacement for older patterns like `Dispatcher.Invoke()` (WPF)

**Key API:**
```csharp
DispatcherQueue.TryEnqueue(DispatcherQueuePriority priority, DispatcherQueueHandler callback)
```

### Implementation Steps

#### 1. MainWindow.xaml.cs - Capture DispatcherQueue on UI Thread

**Added namespace:**
```csharp
using Microsoft.UI.Dispatching;
```

**Added field:**
```csharp
private DispatcherQueue? _dispatcherQueue;
```

**Captured in constructor (runs on UI thread):**
```csharp
public MainWindow()
{
    this.InitializeComponent();

    // CRITICAL: Capture DispatcherQueue on UI thread for thread marshaling
    // This is needed to execute WebView2 operations from background threads (like input hooks)
    _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    // ... rest of initialization
}
```

**Pass to InputManager:**
```csharp
// Install hooks (CRITICAL: Pass main window handle + DispatcherQueue for thread marshaling)
_inputManager.InstallHooks(webView.CoreWebView2, webViewHandle, _windowHandle, _dispatcherQueue!);
```

---

#### 2. InputManager.cs - Use DispatcherQueue for Thread Marshaling

**Added namespace:**
```csharp
using Microsoft.UI.Dispatching;
```

**Added field:**
```csharp
private DispatcherQueue? _dispatcherQueue; // CRITICAL: For marshaling WebView2 calls to UI thread
```

**Updated InstallHooks signature:**
```csharp
public void InstallHooks(CoreWebView2 webView, IntPtr webViewHandle, IntPtr mainWindowHandle, DispatcherQueue dispatcherQueue)
{
    // ... existing code ...
    _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
}
```

**Modified SimulateClickViaJavaScript:**
```csharp
private void SimulateClickViaJavaScript(int x, int y)
{
    try
    {
        if (_webView == null)
        {
            Console.WriteLine("  ERROR: _webView is null!");
            return;
        }

        if (_dispatcherQueue == null)
        {
            Console.WriteLine("  ERROR: _dispatcherQueue is null! Cannot marshal to UI thread.");
            return;
        }

        // JavaScript to find element at coordinates and click it
        string script = $@"
            (function() {{
                try {{
                    var element = document.elementFromPoint({x}, {y});
                    if (element) {{
                        console.log('WebPaper: Clicking element at ({x}, {y}):', element.tagName);

                        // Dispatch click event
                        var clickEvent = new MouseEvent('click', {{
                            view: window,
                            bubbles: true,
                            cancelable: true,
                            clientX: {x},
                            clientY: {y}
                        }});
                        element.dispatchEvent(clickEvent);

                        // Also try direct click() method
                        if (element.click) {{
                            element.click();
                        }}

                        return 'SUCCESS: ' + element.tagName;
                    }} else {{
                        console.log('WebPaper: No element found at ({x}, {y})');
                        return 'ERROR: No element found';
                    }}
                }} catch (e) {{
                    console.error('WebPaper: Click simulation error:', e);
                    return 'ERROR: ' + e.message;
                }}
            }})();
        ";

        Console.WriteLine($"  Marshaling JavaScript execution to UI thread...");

        // CRITICAL FIX: Use DispatcherQueue.TryEnqueue to execute on UI thread
        // This is the WinUI 3 / Windows App SDK way to marshal calls to UI thread
        // WebView2 COM objects require UI thread affinity (STA threading model)
        bool enqueued = _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
        {
            try
            {
                Console.WriteLine($"  Executing JavaScript on UI thread...");
                var result = await _webView.ExecuteScriptAsync(script);
                Console.WriteLine($"  JavaScript result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  JavaScript execution FAILED: {ex.Message}");
                Console.WriteLine($"  Exception type: {ex.GetType().Name}");
                if (ex.StackTrace != null)
                {
                    Console.WriteLine($"  Stack: {ex.StackTrace}");
                }
            }
        });

        if (!enqueued)
        {
            Console.WriteLine($"  ERROR: Failed to enqueue JavaScript execution to UI thread!");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"InputManager: SimulateClickViaJavaScript failed - {ex.Message}");
    }
}
```

---

## 🎓 Key Concepts

### DispatcherQueue.TryEnqueue Behavior

**Parameters:**
- `DispatcherQueuePriority.Normal` - Standard priority (other options: Low, High)
- `async () => { ... }` - Lambda that executes on UI thread

**Execution Flow:**
1. Hook callback (background thread) calls `SimulateClickViaJavaScript()`
2. `TryEnqueue` adds work item to UI thread's message queue
3. UI thread picks up work item from queue
4. Lambda executes on UI thread
5. `ExecuteScriptAsync` succeeds (same thread that created WebView2)

**Return Value:**
- `true` - Successfully enqueued
- `false` - Failed to enqueue (queue shutdown or full)

---

## 🧪 Testing Instructions

### Build the Project

```bash
# On Windows machine with .NET SDK installed
cd E:\project-webpaper
dotnet build src/WebPaper/WebPaper.csproj -c Release
```

### Run the Application

```bash
cd src/WebPaper/bin/Release/net8.0-windows10.0.19041.0/win-x64
./WebPaper.exe
```

### What to Test

#### 1. **Click Simulation** ✅
   - Click anywhere on the wallpaper webpage
   - Expected console output:
     ```
     InputManager: LCLICK at screen(X,Y) -> client(X,Y)
       Using JavaScript simulation (bypasses Windows focus)
       Marshaling JavaScript execution to UI thread...
       Executing JavaScript on UI thread...
       JavaScript result: "SUCCESS: DIV" (or other element)
     ```
   - Expected behavior: Links should navigate, buttons should activate

#### 2. **No COMException** ✅
   - Previously saw: `JavaScript execution FAILED: This method can only be called...`
   - Should now see: `JavaScript result: "SUCCESS: ..."`

#### 3. **Webpage Interaction** ✅
   - Click on links → Page navigates
   - Click on buttons → Buttons activate
   - Click on search boxes → (may need keyboard support)

#### 4. **Performance** ⏱️
   - Click latency should be ~50-200ms (acceptable for wallpaper)
   - No UI freezes or hangs
   - Smooth operation

---

## 📊 Expected Console Output

### Successful Click Example:

```
InputManager: LCLICK at screen(1215,599) -> client(1215,599)
  Using JavaScript simulation (bypasses Windows focus)
  Marshaling JavaScript execution to UI thread...
  Executing JavaScript on UI thread...
  JavaScript result: "SUCCESS: A"
```

**What This Means:**
- ✅ Click detected at screen coordinates (1215, 599)
- ✅ Converted to client coordinates successfully
- ✅ Marshaled to UI thread successfully
- ✅ JavaScript executed successfully
- ✅ Found element (anchor tag "A") and clicked it

### Failed Click Example (No Element):

```
InputManager: LCLICK at screen(100,100) -> client(100,100)
  Using JavaScript simulation (bypasses Windows focus)
  Marshaling JavaScript execution to UI thread...
  Executing JavaScript on UI thread...
  JavaScript result: "ERROR: No element found"
```

**What This Means:**
- ✅ Click detected and marshaled correctly
- ❌ No clickable element at those coordinates
- This is expected for empty areas of the page

---

## 🚨 Potential Issues

### Issue 1: TryEnqueue Returns False

**Symptom:**
```
ERROR: Failed to enqueue JavaScript execution to UI thread!
```

**Possible Causes:**
- DispatcherQueue has been shut down
- UI thread message queue is full (unlikely)

**Solution:**
- Check if MainWindow is still alive
- Verify DispatcherQueue was captured correctly

---

### Issue 2: JavaScript Execution Still Fails

**Symptom:**
```
JavaScript execution FAILED: [some error]
```

**Possible Causes:**
- WebView2 not fully initialized
- Script syntax error
- Content Security Policy blocking script execution

**Solution:**
- Check WebView2 initialization logs
- Test with simpler script: `"alert('test')"`
- Check browser console in DevTools (F12)

---

### Issue 3: Clicks Still Don't Work

**Symptom:**
- JavaScript reports SUCCESS but nothing happens

**Possible Causes:**
- Element is found but not interactive (e.g., `<div>` instead of `<a>`)
- Element has event handlers that aren't triggered by simulated events
- Page uses React/Vue/Angular and needs synthetic events

**Solution:**
- Try different click simulation techniques
- Use element.click() instead of dispatchEvent
- Consider using SendMessage/PostMessage as fallback

---

## 📚 Technical References

### Microsoft Documentation

1. **DispatcherQueue Class**
   https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.dispatching.dispatcherqueue

2. **Threading in WinUI 3**
   https://learn.microsoft.com/en-us/windows/apps/develop/threading-async/

3. **WebView2 Threading Considerations**
   https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model

### Key Learnings

1. **WinUI 3 uses DispatcherQueue, not Dispatcher**
   - `DispatcherQueue` is the Windows App SDK way
   - `Dispatcher` is the old WPF/UWP way

2. **COM Threading Model (STA)**
   - Single-Threaded Apartment
   - Objects must be accessed from creating thread
   - Cross-thread access requires marshaling

3. **Hook Callbacks Run on Different Thread**
   - Low-level hooks don't run on UI thread
   - Must marshal UI operations back to UI thread

4. **Modern Pattern (2024+)**
   - Use DispatcherQueue.TryEnqueue
   - Avoid deprecated AttachThreadInput
   - No need for Invoke/BeginInvoke

---

## 🎉 Why This Will Work

### Previous Approach (Failed):
```
Hook Thread → ExecuteScriptAsync() → COMException ❌
```

### New Approach (Fixed):
```
Hook Thread → TryEnqueue() → UI Thread → ExecuteScriptAsync() → Success ✅
```

### Threading Diagram:

```
┌─────────────────────────────────────────────────────────────┐
│ BACKGROUND THREAD (Hook Callback)                           │
│                                                              │
│  1. User clicks on wallpaper                                │
│  2. WH_MOUSE_LL hook fires                                  │
│  3. MouseHookProc() called                                  │
│  4. SimulateClickViaJavaScript() called                     │
│  5. DispatcherQueue.TryEnqueue() called                     │
│     └──> Adds lambda to UI thread queue                     │
│                                                              │
└─────────────────────────────────────────────────────────────┘
                           │
                           │ (Marshaling via DispatcherQueue)
                           ▼
┌─────────────────────────────────────────────────────────────┐
│ UI THREAD (MainWindow / WebView2 Owner)                     │
│                                                              │
│  6. UI thread picks up queued lambda                        │
│  7. Lambda executes on UI thread                            │
│  8. _webView.ExecuteScriptAsync() called                    │
│     └──> SUCCESS! Same thread that created WebView2        │
│  9. JavaScript executes in webpage                          │
│ 10. Element receives click event                            │
│ 11. Link navigates / Button activates                       │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## ✅ Success Criteria

The fix is successful if:

1. **No COMException** - JavaScript executes without threading errors
2. **Clicks Work** - Webpage elements respond to clicks
3. **Console Output** - Shows "SUCCESS" messages
4. **No Performance Issues** - No freezes or delays
5. **No Side Effects** - Other features still work (settings, tray icon, etc.)

---

## 🔄 Next Steps

If this fix works:

1. ✅ **Mark Issue Resolved** - Threading violation fixed
2. 🎯 **Test Thoroughly** - All click scenarios
3. 📝 **Update Documentation** - Add threading notes
4. 🚀 **Test Other Input** - Keyboard, scroll, right-click

If this fix doesn't work:

1. 🔍 **Check Console Output** - Look for new error messages
2. 🧪 **Try Simpler Script** - Test with `"alert('test')"`
3. 🔧 **Alternative Approaches**:
   - Use WebView2's MoveFocus API
   - Try SendMessage/PostMessage fallback
   - Implement two-window architecture

---

## 📝 Commit Information

**Commit Hash:** 210a0cb
**Branch:** claude/windows-app-development-011CV24uvbatx3BYRTb3bZgL
**Date:** November 11, 2025

**Commit Message:**
```
CRITICAL FIX: Use DispatcherQueue to marshal WebView2 calls to UI thread

Root cause: ExecuteScriptAsync was being called from hook callback thread,
but WebView2 COM objects require UI thread affinity (STA threading model).
This caused COMException: "This method can only be called from the thread
that created the object."

Solution: Implement DispatcherQueue marshaling pattern (WinUI 3 best practice)
```

**Files Changed:**
- `src/WebPaper/MainWindow.xaml.cs` - Capture and pass DispatcherQueue
- `src/WebPaper/Core/InputManager.cs` - Use DispatcherQueue for marshaling

---

**Last Updated:** November 11, 2025
**Status:** ✅ Ready for Testing
**Next Step:** Build and test on Windows 10/11 machine
