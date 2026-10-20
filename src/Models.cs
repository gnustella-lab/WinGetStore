using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace WinGetStore
{
    public class PackageItem : INotifyPropertyChanged
    {
        private string _id = "";
        private string _name = "";
        private string _publisher = "";
        private string _version = "";
        private string _availableVersion = "";
        private string _source = "winget";
        private string _category = "Utilities";
        private string _description = "";
        private string _homepage = "";
        private string _license = "";
        private string _installerType = "";
        private string _releaseNotes = "";
        private string _releaseNotesUrl = "";
        private string _tags = "";
        private string _accentHex = "#0078D4";
        private ImageSource _iconSource;
        private bool _isInstalled;
        private bool _hasUpdate;
        private bool _isBusy;
        private string _statusText = "";
        private double _progressValue;

        public ImageSource IconSource
        {
            get { return _iconSource; }
            set { _iconSource = value; OnPropertyChanged("IconSource"); }
        }

        public string Id { get { return _id; } set { _id = value ?? ""; OnPropertyChanged("Id"); OnPropertyChanged("CliInstallCommand"); } }
        public string Name { get { return _name; } set { _name = value ?? ""; OnPropertyChanged("Name"); OnPropertyChanged("IconInitials"); } }
        public string Publisher { get { return _publisher; } set { _publisher = value ?? ""; OnPropertyChanged("Publisher"); OnPropertyChanged("DisplaySubtext"); } }
        public string Version { get { return _version; } set { _version = value ?? ""; OnPropertyChanged("Version"); OnPropertyChanged("VersionSummary"); } }
        public string AvailableVersion { get { return _availableVersion; } set { _availableVersion = value ?? ""; OnPropertyChanged("AvailableVersion"); OnPropertyChanged("VersionSummary"); } }
        public string Source { get { return _source; } set { _source = value ?? "winget"; OnPropertyChanged("Source"); } }
        public string Category { get { return _category; } set { _category = value ?? "Application"; OnPropertyChanged("Category"); OnPropertyChanged("DisplaySubtext"); } }
        public string Description { get { return _description; } set { _description = value ?? ""; OnPropertyChanged("Description"); } }
        public string Homepage { get { return _homepage; } set { _homepage = value ?? ""; OnPropertyChanged("Homepage"); } }
        public string License { get { return _license; } set { _license = value ?? ""; OnPropertyChanged("License"); } }
        public string InstallerType { get { return _installerType; } set { _installerType = value ?? ""; OnPropertyChanged("InstallerType"); } }
        public string ReleaseNotes { get { return _releaseNotes; } set { _releaseNotes = value ?? ""; OnPropertyChanged("ReleaseNotes"); } }
        public string ReleaseNotesUrl { get { return _releaseNotesUrl; } set { _releaseNotesUrl = value ?? ""; OnPropertyChanged("ReleaseNotesUrl"); } }
        public string Tags { get { return _tags; } set { _tags = value ?? ""; OnPropertyChanged("Tags"); } }
        public string AccentHex { get { return _accentHex; } set { _accentHex = value ?? "#0078D4"; OnPropertyChanged("AccentHex"); } }

        public bool IsInstalled
        {
            get { return _isInstalled; }
            set
            {
                _isInstalled = value;
                OnPropertyChanged("IsInstalled");
                OnPropertyChanged("PrimaryActionText");
            }
        }

        public bool HasUpdate
        {
            get { return _hasUpdate; }
            set
            {
                _hasUpdate = value;
                OnPropertyChanged("HasUpdate");
                OnPropertyChanged("PrimaryActionText");
                OnPropertyChanged("VersionSummary");
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                OnPropertyChanged("IsBusy");
                OnPropertyChanged("PrimaryActionText");
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            set { _statusText = value ?? ""; OnPropertyChanged("StatusText"); }
        }

        public double ProgressValue
        {
            get { return _progressValue; }
            set { _progressValue = value; OnPropertyChanged("ProgressValue"); }
        }

        public string IconInitials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_name)) return "WG";
                string clean = _name.Trim();
                string[] parts = clean.Split(new char[] { ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
                {
                    char c1 = char.ToUpperInvariant(parts[0][0]);
                    char c2 = char.ToUpperInvariant(parts[1][0]);
                    if (char.IsLetterOrDigit(c1) && char.IsLetterOrDigit(c2))
                        return new string(new char[] { c1, c2 });
                }
                return clean.Length >= 2 ? clean.Substring(0, 2).ToUpperInvariant() : clean.ToUpperInvariant();
            }
        }

        public string DisplaySubtext
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_publisher) && !string.IsNullOrWhiteSpace(_category))
                    return _publisher + " • " + _category;
                if (!string.IsNullOrWhiteSpace(_publisher))
                    return _publisher;
                return _id;
            }
        }

        public string PrimaryActionText
        {
            get
            {
                if (_isBusy) return string.IsNullOrWhiteSpace(_statusText) ? "Working..." : _statusText;
                if (_hasUpdate) return "Update";
                if (_isInstalled) return "Installed";
                return "Get";
            }
        }

        public string VersionSummary
        {
            get
            {
                if (_hasUpdate && !string.IsNullOrWhiteSpace(_availableVersion))
                    return _version + " → " + _availableVersion;
                return string.IsNullOrWhiteSpace(_version) ? "Latest version" : _version;
            }
        }

        public string CliInstallCommand
        {
            get { return "winget install --exact --id " + _id; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class InstallTaskItem : INotifyPropertyChanged
    {
        private string _status = "Queued";
        private double _progress = 15;
        private bool _isCompleted;
        private bool _isFailed;
        private string _lastLog = "";

        public string PackageId { get; set; }
        public string PackageName { get; set; }
        public string ActionType { get; set; }
        public DateTime StartedAt { get; set; }

        public string Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged("Status"); }
        }

        public double Progress
        {
            get { return _progress; }
            set { _progress = value; OnPropertyChanged("Progress"); }
        }

        public bool IsCompleted
        {
            get { return _isCompleted; }
            set { _isCompleted = value; OnPropertyChanged("IsCompleted"); }
        }

        public bool IsFailed
        {
            get { return _isFailed; }
            set { _isFailed = value; OnPropertyChanged("IsFailed"); }
        }

        public string LastLog
        {
            get { return _lastLog; }
            set { _lastLog = value; OnPropertyChanged("LastLog"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static class CuratedCatalog
    {
        public static readonly string[] Categories = new string[]
        {
            "All",
            "Essentials",
            "Browsers",
            "Development",
            "Multimedia",
            "Utilities",
            "Communication"
        };

        public static List<PackageItem> GetDefaultPackages()
        {
            return new List<PackageItem>
            {
                new PackageItem { Id = "7zip.7zip", Name = "7-Zip", Publisher = "Igor Pavlov", Version = "26.04", Category = "Essentials", AccentHex = "#2D7D9A", Description = "Lightweight, free, open-source file archiver with a high compression ratio supporting 7z, ZIP, RAR, TAR, and more.", License = "LGPL-2.1", Homepage = "https://7-zip.org/" },
                new PackageItem { Id = "Microsoft.PowerToys", Name = "PowerToys", Publisher = "Microsoft Corporation", Version = "0.89.0", Category = "Essentials", AccentHex = "#0078D4", Description = "Set of system utilities for Windows 10/11 power users: FancyZones, PowerToys Run, Color Picker, Image Resizer, and more.", License = "MIT", Homepage = "https://github.com/microsoft/PowerToys" },
                new PackageItem { Id = "voidtools.Everything", Name = "Everything", Publisher = "voidtools", Version = "1.4.1.1026", Category = "Essentials", AccentHex = "#D97724", Description = "Instant file and folder search engine for Windows powered by NTFS indexing with minimal memory footprint.", License = "MIT", Homepage = "https://www.voidtools.com/" },
                new PackageItem { Id = "Notepad++.Notepad++", Name = "Notepad++", Publisher = "Don Ho", Version = "8.7.7", Category = "Essentials", AccentHex = "#4E9A06", Description = "Fast, lightweight source code and text editor for Windows with syntax highlighting, multi-tab editing, and plugins.", License = "GPL-3.0", Homepage = "https://notepad-plus-plus.org/" },

                new PackageItem { Id = "Mozilla.Firefox", Name = "Mozilla Firefox", Publisher = "Mozilla", Version = "136.0", Category = "Browsers", AccentHex = "#E66000", Description = "Fast, privacy-focused, open-source web browser developed by the Mozilla Foundation.", License = "MPL-2.0", Homepage = "https://www.mozilla.org/firefox/" },
                new PackageItem { Id = "Brave.Brave", Name = "Brave Browser", Publisher = "Brave Software", Version = "1.76.74", Category = "Browsers", AccentHex = "#FB542B", Description = "Chromium-based web browser with built-in ad and tracker blocking out of the box.", License = "MPL-2.0", Homepage = "https://brave.com/" },
                new PackageItem { Id = "Google.Chrome", Name = "Google Chrome", Publisher = "Google LLC", Version = "134.0", Category = "Browsers", AccentHex = "#1A73E8", Description = "Fast, simple, and secure web browser built by Google for the modern web.", License = "Proprietary", Homepage = "https://www.google.com/chrome/" },
                new PackageItem { Id = "LibreWolf.LibreWolf", Name = "LibreWolf", Publisher = "LibreWolf Community", Version = "136.0", Category = "Browsers", AccentHex = "#00ACFF", Description = "Independent fork of Firefox focused on privacy, security, and user freedom with zero telemetry.", License = "MPL-2.0", Homepage = "https://librewolf.net/" },

                new PackageItem { Id = "Microsoft.VisualStudioCode", Name = "Visual Studio Code", Publisher = "Microsoft Corporation", Version = "1.98.0", Category = "Development", AccentHex = "#0066B8", Description = "Lightweight yet powerful source code editor with built-in Git support, debugging, and thousands of extensions.", License = "MIT", Homepage = "https://code.visualstudio.com/" },
                new PackageItem { Id = "Git.Git", Name = "Git", Publisher = "The Git Development Community", Version = "2.48.1", Category = "Development", AccentHex = "#F05032", Description = "Free and open-source distributed version control system including Git Bash and Git GUI for Windows.", License = "GPL-2.0", Homepage = "https://git-scm.com/" },
                new PackageItem { Id = "OpenJS.NodeJS.LTS", Name = "Node.js (LTS)", Publisher = "OpenJS Foundation", Version = "24.20.0", Category = "Development", AccentHex = "#339933", Description = "Cross-platform, open-source JavaScript runtime environment built on Chrome's V8 engine.", License = "MIT", Homepage = "https://nodejs.org/" },
                new PackageItem { Id = "Python.Python.3.12", Name = "Python 3.12", Publisher = "Python Software Foundation", Version = "3.12.9", Category = "Development", AccentHex = "#3776AB", Description = "Official Python 3.12 language interpreter, pip package manager, and standard library for Windows.", License = "PSF", Homepage = "https://www.python.org/" },
                new PackageItem { Id = "Microsoft.WindowsTerminal", Name = "Windows Terminal", Publisher = "Microsoft Corporation", Version = "1.22.10352.0", Category = "Development", AccentHex = "#3B3B3B", Description = "Modern, fast, and productive terminal application for PowerShell, Command Prompt, and WSL users.", License = "MIT", Homepage = "https://github.com/microsoft/terminal" },
                new PackageItem { Id = "Docker.DockerDesktop", Name = "Docker Desktop", Publisher = "Docker Inc.", Version = "4.39.0", Category = "Development", AccentHex = "#2496ED", Description = "Complete environment to build, share, and run containerized applications on Windows.", License = "Proprietary", Homepage = "https://www.docker.com/" },

                new PackageItem { Id = "VideoLAN.VLC", Name = "VLC media player", Publisher = "VideoLAN", Version = "3.0.21", Category = "Multimedia", AccentHex = "#FF8800", Description = "Free and open-source cross-platform multimedia player capable of playing virtually any video and audio format.", License = "GPL-2.0", Homepage = "https://www.videolan.org/vlc/" },
                new PackageItem { Id = "OBSProject.OBSStudio", Name = "OBS Studio", Publisher = "OBS Project", Version = "31.0.2", Category = "Multimedia", AccentHex = "#302E31", Description = "Free and open-source software for high-performance video recording and live streaming.", License = "GPL-2.0", Homepage = "https://obsproject.com/" },
                new PackageItem { Id = "HandBrake.HandBrake", Name = "HandBrake", Publisher = "The HandBrake Team", Version = "1.9.1", Category = "Multimedia", AccentHex = "#0088CC", Description = "Open-source video transcoder for converting video from nearly any format to modern, widely supported codecs.", License = "GPL-2.0", Homepage = "https://handbrake.fr/" },
                new PackageItem { Id = "GIMP.GIMP.3", Name = "GIMP", Publisher = "The GIMP Team", Version = "3.0.0", Category = "Multimedia", AccentHex = "#5C5543", Description = "GNU Image Manipulation Program for photo retouching, image composition, and digital authoring.", License = "GPL-3.0", Homepage = "https://www.gimp.org/" },
                new PackageItem { Id = "Audacity.Audacity", Name = "Audacity", Publisher = "Muse Group", Version = "3.7.1", Category = "Multimedia", AccentHex = "#0000CC", Description = "Free, easy-to-use, multi-track audio editor and recorder for Windows.", License = "GPL-3.0", Homepage = "https://www.audacityteam.org/" },

                new PackageItem { Id = "ShareX.ShareX", Name = "ShareX", Publisher = "ShareX Team", Version = "17.0.0", Category = "Utilities", AccentHex = "#1E88E5", Description = "Free and open-source screenshot capture, screen recorder, GIF tool, and productivity suite with zero ads.", License = "GPL-3.0", Homepage = "https://getsharex.com/" },
                new PackageItem { Id = "Obsidian.Obsidian", Name = "Obsidian", Publisher = "Obsidian", Version = "1.8.9", Category = "Utilities", AccentHex = "#7C3AED", Description = "Powerful personal knowledge base and note-taking application that works on top of local Markdown files.", License = "Freeware", Homepage = "https://obsidian.md/" },
                new PackageItem { Id = "qBittorrent.qBittorrent", Name = "qBittorrent", Publisher = "The qBittorrent Project", Version = "5.0.4", Category = "Utilities", AccentHex = "#2F67BA", Description = "Free, lightweight, open-source, and completely ad-free BitTorrent client.", License = "GPL-2.0", Homepage = "https://www.qbittorrent.org/" },
                new PackageItem { Id = "Bitwarden.Bitwarden", Name = "Bitwarden", Publisher = "Bitwarden Inc.", Version = "2025.2.0", Category = "Utilities", AccentHex = "#175DDC", Description = "Secure, free, and open-source password manager with end-to-end encryption.", License = "GPL-3.0", Homepage = "https://bitwarden.com/" },
                new PackageItem { Id = "Rufus.Rufus", Name = "Rufus", Publisher = "Pete Batard", Version = "4.6", Category = "Utilities", AccentHex = "#A32A29", Description = "Small, fast utility that helps format and create bootable USB flash drives from ISO images.", License = "GPL-3.0", Homepage = "https://rufus.ie/" },

                new PackageItem { Id = "Telegram.TelegramDesktop", Name = "Telegram Desktop", Publisher = "Telegram FZ-LLC", Version = "5.12.3", Category = "Communication", AccentHex = "#26A5E4", Description = "Fast, secure, and cloud-synced desktop messaging application for Windows.", License = "GPL-3.0", Homepage = "https://desktop.telegram.org/" },
                new PackageItem { Id = "Discord.Discord", Name = "Discord", Publisher = "Discord Inc.", Version = "1.0.9184", Category = "Communication", AccentHex = "#5865F2", Description = "Voice, video, and text communication service for communities, friends, and developers.", License = "Proprietary", Homepage = "https://discord.com/" },
                new PackageItem { Id = "Thunderbird.Thunderbird", Name = "Mozilla Thunderbird", Publisher = "Mozilla", Version = "128.8.0", Category = "Communication", AccentHex = "#0A84FF", Description = "Free and open-source desktop email, calendar, contacts, and RSS client by Mozilla.", License = "MPL-2.0", Homepage = "https://www.thunderbird.net/" }
            };
        }

        private static readonly string[] Palette = new string[]
        {
            "#0078D4", "#2D7D9A", "#E66000", "#4E9A06", "#7C3AED",
            "#1E88E5", "#D97724", "#5865F2", "#0088CC", "#339933"
        };

        public static string PickAccentForId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "#0078D4";
            int hash = Math.Abs(id.GetHashCode());
            return Palette[hash % Palette.Length];
        }
    }
}
