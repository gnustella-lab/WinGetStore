# WinGetStore 🛍️

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?logo=windows&logoColor=white)](https://github.com/gnustella-lab/WinGetStore)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.x%20(WPF)-512BD4?logo=dotnet&logoColor=white)](https://github.com/gnustella-lab/WinGetStore)
[![Backend](https://img.shields.io/badge/Backend-WinGet%20CLI-0078D4?logo=powershell&logoColor=white)](https://github.com/microsoft/winget-cli)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Zero SDK Required](https://img.shields.io/badge/Build-Zero%20SDK%20Required-brightgreen)](build.ps1)

A native Windows 10/11 desktop app that faithfully reconstructs the **Microsoft Store** user experience powered entirely by the official **Windows Package Manager (`winget`)** — **with zero advertisements, zero promotional bloat, and no Microsoft account required.**

---

## ✨ Features

- **Genuine Microsoft Store Look & Feel**:
  - Reconstructed from original Microsoft Store UWP binary assets (`Microsoft.WindowsStore_22608.1401.7.0_x64__8wekyb3d8bbwe`).
  - Native typography powered by official Store font glyphs (`StoreIcons.ttf` / `StorMDL2.ttf`).
  - Pixel-perfect Light and Dark themes extracted directly from `Dark.xbf` and `Light.xbf`.
- **Zero Ads & Zero Telemetry**:
  - Completely stripped of promotional hero banners, Game Pass upsells, Microsoft 365 ads, sponsored tiles, and AIHub banners.
  - No background telemetry, no behavioral profiling, and no tracking scripts.
- **100% Local & Account-Free**:
  - No Microsoft account, Xbox authentication, or sign-in prompts.
  - All package queries and installations run locally through the system `winget.exe`.
- **Essential Navigation**:
  - **Home**: Curated catalog of essential developer tools, browsers, multimedia apps, and utilities, plus instant search across the entire WinGet repository.
  - **Library**: Browse and filter all applications installed on your PC detected by `winget list`.
  - **Downloads & Updates**: One-click updates for outdated packages with live session operation history.
  - **Settings**: Toggle themes (Dark/Light), installation modes (Silent vs. Interactive), and package installation scopes (User, Machine, Auto).
- **Multi-Tier App Icon Engine**:
  - Ships with high-resolution pre-cached icons for popular apps.
  - Automatically extracts native 32-bit executable icons from the Windows Registry for installed programs.
  - Dynamically downloads and caches publisher icons for searched packages.
- **Win32 Window Chrome & Fullscreen Support**:
  - Uses native Win32 `WM_GETMINMAXINFO` hooks so borderless fullscreen maximizes precisely to your monitor's work area without overflowing or hiding the Windows taskbar.
  - Smooth titlebar double-click, `F11` fullscreen toggle, and drag-to-restore.
- **Zero-SDK Build Requirement**:
  - Compiles on any clean Windows 10/11 installation using the pre-installed .NET Framework 4.x `csc.exe` compiler. No Visual Studio or .NET SDK installation required!

---

## 📸 Screenshots & UI Layout

| Home (Curated Catalog & Search) | Product Detail Page (PDP) |
|---|---|
| Responsive grid inspired by `CompactProductCard.xbf` and `SearchPageV2Layout.xbf`, categorized with interactive pill filters. | Hero banner, detailed manifest descriptions, version tags, CLI copy command, and live installation log. |

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 (version 1809 or later) or Windows 11.
- [Windows Package Manager (`winget.exe`)](https://learn.microsoft.com/en-us/windows/package-manager/winget/) (pre-installed on modern Windows 10 and 11).

### Download Pre-Built Executable
You can run the portable executable directly without installation:
1. Download `WinGetStore.exe` from the latest [Releases](https://github.com/gnustella-lab/WinGetStore/releases).
2. Ensure the `Assets` and `Fonts` folders reside in the same directory as the executable.
3. Launch `WinGetStore.exe`.

---

## 🛠️ Building from Source

No Visual Studio, SDK, or external package manager is required. WinGetStore compiles in seconds using the built-in C# compiler included with Windows:

```powershell
# Clone the repository
git clone https://github.com/gnustella-lab/WinGetStore.git
cd WinGetStore

# Compile with the native Windows compiler
.\build.ps1

# Run the UI smoke test to verify binary integrity
.\WinGetStore.exe --ui-smoke-test

# Launch the app
.\WinGetStore.exe
```

### Build Script Details
`build.ps1` invokes `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` with references to:
- `PresentationFramework.dll`
- `PresentationCore.dll`
- `WindowsBase.dll`
- `System.Xaml.dll`
- `System.Drawing.dll`

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| **Ctrl + F** | Focus and select the top search box |
| **Enter** (in search) | Execute WinGet package search |
| **Esc** | Navigate back from details or clear active search |
| **F11** | Toggle Maximized / Normal window state |
| **Double-click Titlebar** | Maximize or Restore window |

---

## 📂 Project Structure

```
WinGetStore/
├── Assets/
│   ├── AppIcons/          # High-resolution application PNG icons
│   ├── AppTiles/          # App logos and Windows .ico assets
│   └── JumpListIcons/     # Shell navigation icons (Home, Library, Updates)
├── Fonts/
│   ├── StoreIcons.ttf     # Extracted Store Fluent Icons font
│   ├── StorMDL2.ttf       # Official MDL2 asset font
│   └── StorMDL2c.ttf      # Compact MDL2 asset font
├── docs/
│   └── ARCHITECTURE.md    # Technical architecture & reverse-engineering notes
├── src/
│   ├── IconResolver.cs    # 4-tier caching & Registry/Web icon extraction
│   ├── MainWindow.cs      # Full WPF UI shell, Win32 message hooks, PDP views
│   ├── Models.cs          # PackageItem, InstallTaskItem, CuratedCatalog
│   ├── Program.cs         # DPI-aware entry point, self-tests, and CLI validation
│   ├── StoreTheme.cs      # Color palettes from Dark.xbf & Light.xbf
│   └── WinGetService.cs   # Asynchronous winget.exe process & table parser
├── build.ps1              # Zero-SDK native build script
├── LICENSE                # MIT License
└── README.md              # Project documentation
```

---

## 🔒 Privacy & Architecture Notes

For an in-depth breakdown of how the official Microsoft Store UWP binaries were analyzed, how `.xbf` resources were reverse-engineered, and how borderless window chrome was implemented, see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

- **No Remote Telemetry**: Zero data is transmitted to Microsoft, advertising networks, or third-party servers.
- **Direct WinGet Interaction**: All network communication occurs through the official WinGet client querying official public package repositories.

---

## 📜 License

This project is licensed under the [MIT License](LICENSE).
All product names, logos, and brands are property of their respective owners.
