# WebPaper Icons

## Required Icon: app.ico

The `app.ico` file is required for:
1. **Application icon** - Shows in Windows Explorer and taskbar
2. **System tray icon** - Shows in system notification area

### Creating app.ico

You need to create an `app.ico` file with multiple sizes. You can use one of these methods:

#### Option 1: Online Icon Converter
1. Go to https://convertio.co/png-ico/ or https://www.icoconverter.com/
2. Upload `WebPaperLogo.png` (or any PNG/image you want to use)
3. Select output sizes: 16x16, 32x32, 48x48, 256x256
4. Download the generated `app.ico`
5. Place it in this Assets folder

#### Option 2: Using ImageMagick (Command Line)
```bash
magick convert WebPaperLogo.png -define icon:auto-resize=256,128,96,64,48,32,16 app.ico
```

#### Option 3: Using GIMP
1. Open `WebPaperLogo.png` in GIMP
2. Export as ICO: File → Export As → Select .ico format
3. Choose icon sizes (check 16x16, 32x32, 48x48)
4. Save as `app.ico`

#### Option 4: Placeholder (Quick Solution)
If you don't have an icon yet, you can temporarily copy any `.ico` file and rename it to `app.ico`. The app will work but will show a generic icon.

## How It Works

The `WebPaper.csproj` file is configured to:
- Use `app.ico` as the application icon (visible in Explorer)
- Copy all Assets to the output directory
- The system tray loads the icon from `Assets/app.ico` at runtime

If `app.ico` is not found, the app will fallback to the default Windows application icon (document/window icon).

## Other Assets

The other PNG files (`WebPaperLogo.png`, `Square44x44Logo.png`, etc.) are used for:
- Splash screen
- About dialog
- Store/MSIX packaging (if needed in future)

These are optional and won't cause errors if missing.
