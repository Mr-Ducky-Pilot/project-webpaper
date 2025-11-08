# WebPaper Input Testing Guide

**Purpose:** Test all interactive features of your webpage wallpaper
**Estimated Time:** 15-20 minutes
**Prerequisites:** WebPaper running with Week 2 implementation

---

## 🎯 Quick Test (5 minutes)

### Bare Minimum Tests

**These must work for basic functionality:**

1. **Load wallpaper with YouTube**
   - Expected: YouTube homepage appears as wallpaper
   - Status: ⬜ Pass / ⬜ Fail

2. **Click a video thumbnail**
   - Expected: Video starts playing
   - Status: ⬜ Pass / ⬜ Fail

3. **Type in search box**
   - Expected: Text appears as you type
   - Status: ⬜ Pass / ⬜ Fail

4. **Click desktop icon**
   - Expected: Icon selects (wallpaper NOT affected)
   - Status: ⬜ Pass / ⬜ Fail

**If all 4 pass:** ✅ Core functionality works!
**If any fail:** ⚠️ See Troubleshooting section below

---

## 🔬 Comprehensive Test Suite (20 minutes)

### Section 1: Mouse Click Events

#### Test 1.1: Left Click
```
Steps:
1. Find a link on the webpage
2. Left-click it
3. Observe result

Expected: Link opens/navigates
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 1.2: Right Click
```
Steps:
1. Right-click anywhere on webpage
2. Look for context menu

Expected: Browser context menu appears
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 1.3: Middle Click
```
Steps:
1. Middle-click a link (if supported by website)

Expected: Link opens in new tab or scrolls
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 1.4: Button Hover
```
Steps:
1. Move mouse over a button
2. Observe hover effect

Expected: Button changes appearance on hover
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

---

### Section 2: Mouse Movement & Scrolling

#### Test 2.1: Scroll Wheel Down
```
Steps:
1. Scroll wheel down
2. Observe page movement

Expected: Page scrolls down
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 2.2: Scroll Wheel Up
```
Steps:
1. Scroll wheel up
2. Observe page movement

Expected: Page scrolls up
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 2.3: Mouse Drag
```
Steps:
1. Click and hold on text
2. Drag to select multiple words
3. Release

Expected: Text is highlighted/selected
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

---

### Section 3: Keyboard Input

#### Test 3.1: Text Entry
```
Steps:
1. Click in a search box or text field
2. Type "test"

Expected: Letters appear in the field
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 3.2: Backspace/Delete
```
Steps:
1. Type some text
2. Press Backspace
3. Press Delete

Expected: Characters are removed
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 3.3: Enter Key
```
Steps:
1. Type in search box
2. Press Enter

Expected: Search executes
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 3.4: Keyboard Shortcut (Ctrl+F)
```
Steps:
1. Press Ctrl+F

Expected: Find dialog appears
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 3.5: Tab Navigation
```
Steps:
1. Press Tab key multiple times
2. Observe focus moving

Expected: Focus moves between interactive elements
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

---

### Section 4: Desktop Icon Protection

#### Test 4.1: Desktop Icon Click
```
Steps:
1. Click a desktop icon

Expected: Icon selects (NOT webpage action)
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 4.2: Desktop Icon Double-Click
```
Steps:
1. Double-click a desktop icon

Expected: Application/file opens (NOT webpage action)
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 4.3: Desktop Icon Right-Click
```
Steps:
1. Right-click a desktop icon

Expected: Windows context menu (NOT webpage menu)
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 4.4: Desktop Icon Drag
```
Steps:
1. Drag a desktop icon to new position

Expected: Icon moves (NOT webpage drag event)
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

---

### Section 5: Complex Interactions

#### Test 5.1: YouTube Video Playback
```
URL: https://www.youtube.com

Steps:
1. Navigate to YouTube
2. Click a video
3. Click play/pause
4. Adjust volume
5. Seek timeline

Expected: All controls work
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 5.2: Twitter/X Interaction
```
URL: https://twitter.com

Steps:
1. Navigate to Twitter
2. Scroll through timeline
3. Click on a tweet
4. Like a tweet

Expected: All interactions work
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 5.3: Google Search
```
URL: https://www.google.com

Steps:
1. Navigate to Google
2. Type in search box
3. See autocomplete suggestions
4. Press Enter
5. Click a search result

Expected: Search works end-to-end
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 5.4: Form Submission
```
URL: Any website with a form

Steps:
1. Find a form (login, search, contact)
2. Type in fields
3. Submit form

Expected: Form submits successfully
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

---

### Section 6: Performance Testing

#### Test 6.1: CPU Usage (Idle)
```
Steps:
1. Let wallpaper sit idle for 30 seconds
2. Check Task Manager → WebPaper.exe

Expected: <5% CPU
Actual: ________%
Status: ⬜ Pass / ⬜ Fail
```

#### Test 6.2: CPU Usage (Active)
```
Steps:
1. Actively interact (click, type, scroll) for 30 seconds
2. Check Task Manager

Expected: <15% CPU
Actual: ________%
Status: ⬜ Pass / ⬜ Fail
```

#### Test 6.3: Memory Usage
```
Steps:
1. Check Task Manager → WebPaper.exe memory

Expected: <500MB
Actual: ________MB
Status: ⬜ Pass / ⬜ Fail
```

#### Test 6.4: Input Latency
```
Steps:
1. Click rapidly on different buttons
2. Observe responsiveness

Expected: Feels instant (<50ms delay)
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

---

### Section 7: Edge Cases

#### Test 7.1: Rapid Mouse Movement
```
Steps:
1. Move mouse rapidly across screen
2. Check console for errors
3. Verify no lag

Expected: No errors, smooth movement
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 7.2: Rapid Typing
```
Steps:
1. Type very quickly in a text field
2. Check all characters appear

Expected: All characters captured
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 7.3: Multi-Key Combinations
```
Steps:
1. Try Ctrl+Shift+F
2. Try Ctrl+Alt+S
3. Try custom shortcuts

Expected: Complex shortcuts work
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

#### Test 7.4: Long Session (10 minutes)
```
Steps:
1. Use wallpaper normally for 10 minutes
2. Check for crashes or slowdowns

Expected: Stable, no degradation
Actual: _______________________
Status: ⬜ Pass / ⬜ Fail
```

---

## 🐛 Troubleshooting

### Problem: Input not working at all

**Symptoms:**
- Can't click anything
- Typing does nothing
- Console shows "Input hooks installed successfully"

**Solutions:**
1. **Check Console Output:**
   ```
   Look for "WebView2 Handle: 0x..." message
   If shows 0x00000000 → WebView handle not found
   ```

2. **Verify Hooks Installed:**
   ```
   Console should show:
   "Mouse Hook: 0x[nonzero]"
   "Keyboard Hook: 0x[nonzero]"
   ```

3. **Increase Wait Time:**
   ```csharp
   // In MainWindow.xaml.cs, line 178
   await Task.Delay(500); // Try 1000 or 2000
   ```

4. **Run as Administrator:**
   ```
   Some hooks may require elevated privileges
   Right-click WebPaper.exe → Run as Administrator
   ```

---

### Problem: Desktop icons not clickable

**Symptoms:**
- Clicking icon triggers webpage action
- Can't select or open desktop icons

**Solutions:**
1. **Check Hit-Testing:**
   ```
   Console should show icon class detection
   Look for "SysListView32" messages
   ```

2. **Verify IsClickOnWallpaper Logic:**
   ```csharp
   // Add debug output in InputManager.cs
   Console.WriteLine($"Hit window: {className}");
   ```

3. **Disable Input Temporarily:**
   ```csharp
   // In InputManager.cs
   _isEnabled = false; // Test if icons work
   ```

---

### Problem: High CPU usage

**Symptoms:**
- CPU >20% when idle
- Fan spinning up
- Laggy performance

**Solutions:**
1. **Check Event Rate:**
   ```
   Console shows "~XXX events/sec"
   If >500: Something is wrong
   ```

2. **Reduce Logging:**
   ```csharp
   // Comment out verbose console logs
   // Console.WriteLine($"Mouse {eventName}...");
   ```

3. **Limit Event Forwarding:**
   ```csharp
   // Only forward on wallpaper area (already implemented)
   // Don't forward MOUSEMOVE unless needed
   ```

---

### Problem: Some websites don't work

**Symptoms:**
- Specific sites show errors
- "Content can't be displayed"
- Input works on some sites but not others

**Explanations:**
1. **X-Frame-Options:** Some sites block embedding
2. **CORS:** Cross-origin restrictions
3. **JavaScript Detection:** Sites detect WebView2

**Workarounds:**
1. Try different URL
2. Use mobile version of site
3. Check WebView2 DevTools for errors

---

## 📊 Test Results Template

Copy this template and fill in your results:

```
=== WEBPAPER INPUT TEST RESULTS ===

Date: __________
Windows Version: __________
WebPaper Version: Week 2 Implementation

QUICK TESTS:
  ⬜ YouTube loads
  ⬜ Video click works
  ⬜ Typing works
  ⬜ Icons protected

MOUSE TESTS:
  ⬜ Left click
  ⬜ Right click
  ⬜ Scroll wheel
  ⬜ Hover effects

KEYBOARD TESTS:
  ⬜ Text entry
  ⬜ Backspace/Delete
  ⬜ Enter key
  ⬜ Ctrl+F
  ⬜ Tab navigation

DESKTOP PROTECTION:
  ⬜ Icon click
  ⬜ Icon double-click
  ⬜ Icon right-click
  ⬜ Icon drag

PERFORMANCE:
  CPU (Idle): ____%
  CPU (Active): ____%
  Memory: ____MB
  Latency: ____ms

COMPLEX TESTS:
  ⬜ YouTube playback
  ⬜ Twitter interaction
  ⬜ Google search
  ⬜ Form submission

EDGE CASES:
  ⬜ Rapid movement
  ⬜ Rapid typing
  ⬜ Multi-key combos
  ⬜ 10-minute session

OVERALL STATUS:
  ⬜ All Pass (Ready for Week 3!)
  ⬜ Most Pass (Minor issues)
  ⬜ Many Fail (Needs debugging)

NOTES:
_________________________________
_________________________________
_________________________________
```

---

## ✅ Success Criteria

**Minimum Acceptable Performance:**
- 90%+ of basic tests pass
- Desktop icons fully protected
- CPU <10% average
- No crashes in 10-minute test

**Excellent Performance:**
- 100% of tests pass
- CPU <5% idle
- Input latency <20ms
- Works on all tested websites

---

## 🎯 Recommended Test Websites

| Website | Purpose | Difficulty |
|---------|---------|-----------|
| google.com | Simple search | Easy |
| youtube.com | Video controls | Medium |
| twitter.com | Social feed | Medium |
| reddit.com | Scrolling, clicking | Medium |
| shadertoy.com | Visual effects | Hard |
| monkeytype.com | Typing test | Hard |
| earth.google.com | 3D navigation | Hard |

---

## 📝 Reporting Issues

If you find issues, report them with this information:

```
Issue: [Brief description]

Steps to Reproduce:
1.
2.
3.

Expected Behavior:
[What should happen]

Actual Behavior:
[What actually happens]

Environment:
- Windows Version: [11 23H2, etc.]
- WebView2 Version: [From Control Panel]
- URL Tested: [website]

Console Output:
[Copy relevant error messages]

Screenshots:
[If applicable]
```

---

**Ready to test?** Build and run WebPaper, then work through this guide systematically! 🚀
