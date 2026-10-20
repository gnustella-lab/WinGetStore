# WinGetStore

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?logo=windows&logoColor=white)](https://github.com/gnustella-lab/WinGetStore)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.x%20(WPF)-512BD4?logo=dotnet&logoColor=white)](https://github.com/gnustella-lab/WinGetStore)
[![Backend](https://img.shields.io/badge/Backend-WinGet%20CLI-0078D4?logo=powershell&logoColor=white)](https://github.com/microsoft/winget-cli)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Zero SDK Required](https://img.shields.io/badge/Build-Zero%20SDK%20Required-brightgreen)](build.ps1)

The official Microsoft Store has become cluttered. It pushes sponsored banners, Game Pass upsells, Microsoft 365 ads, and prompts for a Microsoft account before letting you do basic things. Yet Windows already ships with `winget`, a reliable command-line package manager that pulls from curated open repositories.

WinGetStore connects the two. It takes the original Microsoft Store interface, pairs it directly with your local `winget` installation, and drops all the ads, tracking, and account checks. You get a fast, native desktop app with the original look, dark and light palettes, and instant updates.

---

## Features

- **Store interface, minus the noise**: Rebuilt using assets and XBF layouts extracted from the official Microsoft Store package (`Microsoft.WindowsStore_22608.1401.7.0`). Uses the original `StoreIcons.ttf` and `StorMDL2.ttf` icon fonts.
- **No ads, no tracking, no account**: All promotional modules (`TileAdsBadge`, `PromotionalBar`, `UpsellModule`, `GamePassHeroUnit`, `AIHub`) and account authentication controls (`MeControl`, `PersonPicture`) were removed. Everything runs locally on your PC.
- **Direct WinGet backend**: Searches, installs, upgrades, and uninstalls call the local `winget.exe`. You get real-time terminal output in the app during installs.
- **Home, Library, and Updates**:
  - **Home**: Curated catalog of common tools (dev tools, browsers, utilities) plus instant search across all WinGet repositories.
  - **Library**: Lists every program installed on your machine through `winget list` with quick search filtering.
  - **Updates**: Detects outdated packages with `winget upgrade` and lets you update individually or all at once.
  - **Settings**: Choose between Dark and Light mode, toggle silent background installs (`--silent`), and set default package scope (user, machine, auto).
- **Working app icons**: Comes with cached icons for popular apps, pulls native icons directly from installed `.exe` files registered in Windows, and downloads publisher icons in the background when you search new packages.
- **Proper fullscreen behavior**: Standard borderless WPF windows often spill over taskbars or adjacent monitors when maximized. WinGetStore hooks `WM_GETMINMAXINFO` to respect the active monitor work area down to the pixel. Supports `F11`, double-click on titlebar, and drag-to-restore.
- **Builds without an SDK**: Compiles out of the box using Windows 10/11 built-in .NET Framework compiler (`csc.exe`). You do not need to install Visual Studio or any .NET SDK to build the binary.

---

## Quick start

### Requirements
- Windows 10 (version 1809 or higher) or Windows 11
- `winget.exe` (included by default on modern Windows)

### Pre-built binary
1. Grab `WinGetStore.exe` from the [Releases](https://github.com/gnustella-lab/WinGetStore/releases) page.
2. Keep the `Assets` and `Fonts` folders in the same directory as the executable.
3. Run `WinGetStore.exe`.

---

## Building from source

You do not need Visual Studio or the dotnet CLI. Open PowerShell in the project directory and run:

```powershell
# Clone the repository
git clone https://github.com/gnustella-lab/WinGetStore.git
cd WinGetStore

# Compile using Windows built-in csc.exe compiler
.\build.ps1

# Run self-tests
.\WinGetStore.exe --ui-smoke-test

# Launch
.\WinGetStore.exe
```

The build script compiles `src\*.cs` using `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and references standard WPF and drawing assemblies.

---

## Shortcuts

| Key | Action |
|---|---|
| **Ctrl + F** | Jump to search input |
| **Enter** | Search WinGet catalog |
| **Esc** | Go back or clear search |
| **F11** | Toggle fullscreen / normal window |
| **Double-click titlebar** | Maximize or restore |

---

## Project structure

```
WinGetStore/
├── Assets/
│   ├── AppIcons/          # PNG icons for curated applications
│   ├── AppTiles/          # Application logo and .ico files
│   └── JumpListIcons/     # Sidebar navigation icons
├── Fonts/
│   ├── StoreIcons.ttf     # Fluent store icon font
│   ├── StorMDL2.ttf       # Standard MDL2 assets
│   └── StorMDL2c.ttf      # Compact MDL2 assets
├── docs/
│   └── ARCHITECTURE.md    # Technical details and reverse-engineering findings
├── src/
│   ├── IconResolver.cs    # Multi-tier icon loader (memory, disk, registry, web)
│   ├── MainWindow.cs      # Main window shell, title bar, navigation, and PDP views
│   ├── Models.cs          # Data models, catalog entries, and property bindings
│   ├── Program.cs         # Entry point, DPI setup, and smoke test checks
│   ├── StoreTheme.cs      # Dark and Light palettes from Dark.xbf / Light.xbf
│   └── WinGetService.cs   # Asynchronous winget.exe wrapper and table parser
├── build.ps1              # Build script using Windows csc.exe
├── LICENSE                # MIT License
└── README.md              # Project documentation
```

---

## Technical notes and privacy

For a full breakdown of the reverse-engineering work, XBF disassembly findings, Win32 window management, and icon extraction pipelines, see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

WinGetStore sends no telemetry, error reports, or usage data to anyone. Network requests are limited to `winget` querying its configured package sources, plus downloading app icons when you search for new packages.

---

## License

MIT License. See [LICENSE](LICENSE) for details.
All product names, logos, and trademarks belong to their respective owners.
