<p align="center">
  <img src="docs/logo.png" width="112" alt="LangPop lollipop logo">
</p>

<h1 align="center">LangPop</h1>
<p align="center"><em>A little pop of "which language am I typing in?"</em></p>

A tiny Windows 11 tray app. When you change the input language (Win+Space, Alt+Shift) or press Shift to toggle
Chinese/English inside an IME such as Microsoft Pinyin, a translucent popup fades in next to the text caret (or next to the mouse if there's no caret), then fades out.
It doesn't depend on the taskbar, so it still works with auto-hide on or in full-screen apps.

![Demo: switching EN → 中 with Win+Space, then toggling 中/英 with Shift while typing](docs/demo.gif)

| State | Popup |
|---|---|
| English layout | **EN** English |
| Chinese IME, Chinese mode | **中** 中文 (accent bar) |
| Chinese IME, English mode (Shift) | **英** 英文模式 |
| Caps Lock on | caption gets `· CAPS` |

## Download
Get `LangPop.exe` (or the zip) from the [latest release](https://github.com/seconwoo/langpop/releases/latest).
There's no installer: put it anywhere and run it. It works on Windows 10 and 11 with nothing else to install.

The exe isn't code-signed, so on first run SmartScreen may say "Windows protected your PC".
Click **More info**, then **Run anyway**.

## Build
Needs no SDK. It uses the C# compiler that ships with Windows (.NET Framework 4.x):

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

This produces `bin\LangPop.exe`.

## Use
- Run `LangPop.exe`. Only one copy runs at a time.
- Tray icon: left-click shows the current state. The right-click menu has:
  - **Theme**: Auto (follows Windows light/dark), Dark, Light, Glass, Frost, Midnight, Accent (your Windows accent color), Sakura, Terminal.
    - **Compact size** (at the bottom of the Theme menu) shrinks the popup to a small glyph-only badge in any theme.
      An underline marks native input mode (e.g. 中), and ⇪ appears when Caps Lock is on.
  - **Background opacity**: 15% / 30% / 45% (default) / 60% / 80%. Lower is more see-through. Text gets a soft halo so it stays readable.
  - **Start with Windows**, **Exit**.
- Picking a theme or opacity previews it right away. Settings are saved in `HKCU\Software\LangPop`.

### Themes
All themes at the default 45% background opacity, in standard and compact size:

![Theme gallery](docs/themes.png)

## Notes
- Detection combines the foreground thread's keyboard layout with the IME open/conversion mode
  (`WM_IME_CONTROL`). It polls every 120 ms and also checks again right after Shift/Ctrl/Alt/Win/Space/CapsLock is released.
- Windows that run as administrator can't be queried from a normal process. To cover those, run the app elevated.
- If you switch to a window that uses a different language, no popup is shown, so Alt-Tab doesn't trigger one.

## Logo
The lollipop is drawn in code (`tools/Lollipop.cs`). `tools\make-icon.ps1` renders it to the multi-size
`assets\langpop.ico`, which `build.ps1` embeds in the exe, and to `docs/logo.png`.

## Regenerating the images
`docs/demo.gif` and `docs/themes.png` are drawn by the app's own popup renderer, composited onto a mock editor,
using the same fade and slide timing as the real app. To rebuild them (needs Python with Pillow):

```
powershell -ExecutionPolicy Bypass -File tools\make-demo.ps1
```

## Releasing
Push a version tag. GitHub Actions ([release.yml](.github/workflows/release.yml)) builds on Windows, stamps the
version into the exe, and publishes a release with the zip, the standalone exe and SHA256 checksums:

```
git tag v1.2.3
git push origin v1.2.3
```

To build the same package locally, run `powershell -ExecutionPolicy Bypass -File package.ps1 -Version 1.2.3`.
The output goes to `dist\`.

## License
[Apache License 2.0](LICENSE)
