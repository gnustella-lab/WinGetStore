using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shell;

namespace WinGetStore
{
    public class MainWindow : Window
    {
        private readonly WinGetService _winget = new WinGetService();
        private StorePalette _palette;
        private bool _isDarkTheme = true;
        private bool _silentInstall = true;
        private string _installScope = "auto";

        private string _activeTab = "home";
        private string _selectedCategory = "All";
        private bool _isShowingSearchResults;
        private string _lastSearchQuery = "";
        private PackageItem _currentPdpItem;

        private readonly List<PackageItem> _curatedItems;
        private List<PackageItem> _searchResults = new List<PackageItem>();
        private List<PackageItem> _installedItems = new List<PackageItem>();
        private List<PackageItem> _updateItems = new List<PackageItem>();
        private readonly ObservableCollection<InstallTaskItem> _activeTasks = new ObservableCollection<InstallTaskItem>();
        private readonly List<string> _consoleLines = new List<string>();
        private string _wingetVersionText = "Detecting...";
        private string _sourcesSummaryText = "Loading WinGet package sources...";

        // Root layout references
        private WindowChrome _windowChrome;
        private Grid _rootGrid;
        private Border _contentHostBorder;
        private Grid _contentHostGrid;
        private TextBox _searchBox;
        private Button _backButton;
        private TextBlock _maxRestoreGlyphText;
        private TextBlock _statusBadgeText;
        private StackPanel _navTopStack;
        private StackPanel _navBottomStack;
        private TextBox _libraryFilterBox;
        private TextBlock _liveConsoleBlock;

        public MainWindow()
        {
            _curatedItems = CuratedCatalog.GetDefaultPackages();
            foreach (var item in _curatedItems)
            {
                IconResolver.ResolveIconForPackage(item);
            }

            _palette = StoreTheme.GetPalette(_isDarkTheme);

            Rect workArea = SystemParameters.WorkArea;
            Title = "Microsoft Store (WinGet)";
            Width = Math.Min(1180, Math.Round(workArea.Width * 0.88));
            Height = Math.Min(650, Math.Round(workArea.Height * 0.88));
            MinWidth = 760;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

            _windowChrome = new WindowChrome
            {
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(8),
                GlassFrameThickness = new Thickness(1),
                ResizeBorderThickness = new Thickness(6),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, _windowChrome);

            var logo = StoreTheme.LoadAssetImage(@"Assets\AppTiles\StoreLogo64.png");
            if (logo != null) Icon = logo;

            SourceInitialized += OnWindowSourceInitialized;
            StateChanged += OnWindowStateChanged;
            PreviewKeyDown += OnWindowKeyDown;

            RebuildShell();
            Loaded += async (s, e) => await InitializeStoreDataAsync();
        }

        private void OnWindowSourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null)
            {
                source.AddHook(WindowProc);
            }
            WindowState = WindowState.Maximized;
            ApplyWindowStateVisuals();
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            ApplyWindowStateVisuals();
        }

        private void ApplyWindowStateVisuals()
        {
            bool isMax = WindowState == WindowState.Maximized;
            if (_windowChrome != null)
            {
                _windowChrome.CornerRadius = isMax ? new CornerRadius(0) : new CornerRadius(8);
                _windowChrome.ResizeBorderThickness = isMax ? new Thickness(0) : new Thickness(6);
            }
            if (_maxRestoreGlyphText != null)
            {
                _maxRestoreGlyphText.Text = isMax ? "\uE923" : "\uE922";
            }
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    RECT rcWorkArea = monitorInfo.rcWork;
                    RECT rcMonitorArea = monitorInfo.rcMonitor;
                    mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                    mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                    mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                    mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
                }
            }
            Marshal.StructureToPtr(mmi, lParam, true);
        }

        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (_searchBox != null)
                {
                    _searchBox.Focus();
                    _searchBox.SelectAll();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.F11)
            {
                ToggleMaximize();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _currentPdpItem != null)
            {
                NavigateBack();
                e.Handled = true;
            }
        }

        private async Task InitializeStoreDataAsync()
        {
            SetHeaderStatus("Syncing with local WinGet...");
            _wingetVersionText = await _winget.GetWinGetVersionAsync();
            await RefreshInstalledAndUpdatesAsync();
            _sourcesSummaryText = await _winget.GetSourcesSummaryAsync();
            SetHeaderStatus("WinGet " + _wingetVersionText + " • No Account");
            RenderActiveView();
        }

        private async Task RefreshInstalledAndUpdatesAsync()
        {
            var installedTask = _winget.GetInstalledPackagesAsync();
            var updatesTask = _winget.GetUpgradablePackagesAsync();
            await Task.WhenAll(installedTask, updatesTask);

            _installedItems = installedTask.Result;
            _updateItems = updatesTask.Result;

            SyncPackageStates(_curatedItems);
            SyncPackageStates(_searchResults);
            if (_currentPdpItem != null)
                SyncSinglePackageState(_currentPdpItem);

            UpdateNavRailBadges();
        }

        private void SyncPackageStates(IEnumerable<PackageItem> list)
        {
            if (list == null) return;
            foreach (var item in list)
                SyncSinglePackageState(item);
        }

        private void SyncSinglePackageState(PackageItem item)
        {
            if (item == null) return;
            var installedMatch = _installedItems.FirstOrDefault(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            var updateMatch = _updateItems.FirstOrDefault(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));

            if (updateMatch != null)
            {
                item.IsInstalled = true;
                item.HasUpdate = true;
                if (!string.IsNullOrWhiteSpace(updateMatch.Version)) item.Version = updateMatch.Version;
                item.AvailableVersion = updateMatch.AvailableVersion;
            }
            else if (installedMatch != null)
            {
                item.IsInstalled = true;
                item.HasUpdate = false;
                if (!string.IsNullOrWhiteSpace(installedMatch.Version)) item.Version = installedMatch.Version;
                item.AvailableVersion = "";
            }
            else
            {
                item.IsInstalled = false;
                item.HasUpdate = false;
                item.AvailableVersion = "";
            }
        }

        private void RebuildShell()
        {
            _palette = StoreTheme.GetPalette(_isDarkTheme);
            Background = _palette.WindowBackground;

            _rootGrid = new Grid
            {
                Background = _palette.WindowBackground
            };
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Row 0: TitleBar + Search Box (Zero Microsoft Account / MeControl!)
            UIElement titleBar = BuildTitleBar();
            Grid.SetRow(titleBar, 0);
            _rootGrid.Children.Add(titleBar);

            // Row 1: Left Navigation Rail (72px) + Main Rounded Content Surface
            Grid bodyGrid = new Grid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(bodyGrid, 1);

            UIElement navRail = BuildNavigationRail();
            Grid.SetColumn(navRail, 0);
            bodyGrid.Children.Add(navRail);

            _contentHostBorder = new Border
            {
                Background = _palette.ContentSurfaceBackground,
                BorderBrush = _palette.ContentSurfaceBorder,
                BorderThickness = new Thickness(1, 1, 0, 0),
                CornerRadius = new CornerRadius(8, 0, 0, 0),
                ClipToBounds = true
            };
            _contentHostGrid = new Grid();
            _contentHostBorder.Child = _contentHostGrid;
            Grid.SetColumn(_contentHostBorder, 1);
            bodyGrid.Children.Add(_contentHostBorder);

            _rootGrid.Children.Add(bodyGrid);
            Content = _rootGrid;

            RenderActiveView();
        }

        private UIElement BuildTitleBar()
        {
            Grid header = new Grid
            {
                Background = _palette.WindowBackground
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Point dragStartPoint = new Point();
            bool isDraggingHeader = false;

            header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    isDraggingHeader = false;
                    ToggleMaximize();
                    e.Handled = true;
                }
                else if (e.ButtonState == MouseButtonState.Pressed)
                {
                    if (WindowState == WindowState.Normal)
                    {
                        try { DragMove(); } catch { }
                    }
                    else
                    {
                        dragStartPoint = e.GetPosition(this);
                        isDraggingHeader = true;
                    }
                }
            };

            header.MouseMove += (s, e) =>
            {
                if (isDraggingHeader && e.LeftButton == MouseButtonState.Pressed && WindowState == WindowState.Maximized)
                {
                    Point currentPos = e.GetPosition(this);
                    if (Math.Abs(currentPos.X - dragStartPoint.X) > 4 || Math.Abs(currentPos.Y - dragStartPoint.Y) > 4)
                    {
                        isDraggingHeader = false;
                        Point screenPos = PointToScreen(currentPos);
                        double percentX = currentPos.X / Math.Max(1.0, ActualWidth);
                        WindowState = WindowState.Normal;
                        Left = screenPos.X - (Width * percentX);
                        Top = screenPos.Y - 20;
                        try { DragMove(); } catch { }
                    }
                }
            };

            header.MouseLeftButtonUp += (s, e) =>
            {
                isDraggingHeader = false;
            };

            // Left: Back button + Store icon + Title
            StackPanel leftPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 16, 0)
            };

            _backButton = CreateIconButton(StoreTheme.GlyphBack, "Back (Esc)", 34, 30);
            _backButton.Margin = new Thickness(0, 0, 6, 0);
            _backButton.Visibility = (_currentPdpItem != null || _isShowingSearchResults) ? Visibility.Visible : Visibility.Collapsed;
            _backButton.Click += (s, e) => NavigateBack();
            leftPanel.Children.Add(_backButton);

            var logoImg = StoreTheme.LoadAssetImage(@"Assets\AppTiles\StoreLogo.png");
            if (logoImg != null)
            {
                Image img = new Image
                {
                    Source = logoImg,
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(6, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                leftPanel.Children.Add(img);
            }

            TextBlock titleText = new TextBlock
            {
                Text = "Microsoft Store",
                Foreground = _palette.TextPrimary,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftPanel.Children.Add(titleText);

            Border cleanPill = new Border
            {
                Background = _palette.SubtleSurfaceBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "Native WinGet",
                    Foreground = _palette.AccentTextForeground,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold
                }
            };
            leftPanel.Children.Add(cleanPill);

            Grid.SetColumn(leftPanel, 0);
            header.Children.Add(leftPanel);

            // Center: Search Bar (SearchPageV2Layout style)
            Border searchBorder = new Border
            {
                Background = _palette.SearchBoxBackground,
                BorderBrush = _palette.SearchBoxBorder,
                BorderThickness = new Thickness(1, 1, 1, 1.5),
                CornerRadius = new CornerRadius(6),
                MaxWidth = 480,
                Height = 32,
                Margin = new Thickness(16, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid searchInner = new Grid();
            searchInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid inputOverlayGrid = new Grid();
            TextBlock placeholder = new TextBlock
            {
                Text = "Search apps, developer tools, or package IDs in WinGet (Ctrl+F)",
                Foreground = _palette.TextTertiary,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 8, 0),
                IsHitTestVisible = false
            };

            _searchBox = new TextBox
            {
                Text = _lastSearchQuery,
                Background = Brushes.Transparent,
                Foreground = _palette.TextPrimary,
                CaretBrush = _palette.AccentTextForeground,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(10, 0, 6, 0)
            };

            placeholder.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

            _searchBox.GotFocus += (s, e) =>
            {
                searchBorder.BorderBrush = _palette.AccentTextForeground;
            };
            _searchBox.LostFocus += (s, e) =>
            {
                searchBorder.BorderBrush = _palette.SearchBoxBorder;
            };
            _searchBox.TextChanged += (s, e) =>
            {
                placeholder.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                if (string.IsNullOrWhiteSpace(_searchBox.Text) && _isShowingSearchResults)
                {
                    _isShowingSearchResults = false;
                    _lastSearchQuery = "";
                    UpdateBackButtonVisibility();
                    if (_activeTab == "home" && _currentPdpItem == null)
                        RenderActiveView();
                }
            };
            _searchBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    await ExecuteWinGetSearchAsync(_searchBox.Text);
                }
            };

            inputOverlayGrid.Children.Add(placeholder);
            inputOverlayGrid.Children.Add(_searchBox);
            Grid.SetColumn(inputOverlayGrid, 0);
            searchInner.Children.Add(inputOverlayGrid);

            Button clearBtn = CreateIconButton(StoreTheme.GlyphCancel, "Clear search", 28, 28);
            clearBtn.FontSize = 10;
            clearBtn.Click += (s, e) =>
            {
                _searchBox.Text = "";
                _isShowingSearchResults = false;
                _lastSearchQuery = "";
                UpdateBackButtonVisibility();
                if (_activeTab == "home" && _currentPdpItem == null)
                    RenderActiveView();
            };
            Grid.SetColumn(clearBtn, 1);
            searchInner.Children.Add(clearBtn);

            Button searchBtn = CreateIconButton(StoreTheme.GlyphSearch, "Search in WinGet (Enter)", 32, 28);
            searchBtn.Click += async (s, e) => await ExecuteWinGetSearchAsync(_searchBox.Text);
            Grid.SetColumn(searchBtn, 2);
            searchInner.Children.Add(searchBtn);

            searchBorder.Child = searchInner;
            Grid.SetColumn(searchBorder, 1);
            header.Children.Add(searchBorder);

            // Right: Status indicator (Zero Account / Login button!) + Window Chrome Controls
            StackPanel rightPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            Border statusPill = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(8, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusBadgeText = new TextBlock
            {
                Text = "WinGet " + _wingetVersionText + " • No Account",
                Foreground = _palette.TextTertiary,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusPill.Child = _statusBadgeText;
            rightPanel.Children.Add(statusPill);

            Button minBtn = CreateWindowCaptionButton("\uE921", "Minimize", false);
            minBtn.Click += (s, e) => WindowState = WindowState.Minimized;
            rightPanel.Children.Add(minBtn);

            string maxGlyph = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            Button maxBtn = CreateWindowCaptionButton(maxGlyph, "Maximize / Restore (F11)", false);
            Border maxBorder = maxBtn.Content as Border;
            if (maxBorder != null)
                _maxRestoreGlyphText = maxBorder.Child as TextBlock;
            maxBtn.Click += (s, e) => ToggleMaximize();
            rightPanel.Children.Add(maxBtn);

            Button closeBtn = CreateWindowCaptionButton("\uE8BB", "Close", true);
            closeBtn.Click += (s, e) => Close();
            rightPanel.Children.Add(closeBtn);

            Grid.SetColumn(rightPanel, 2);
            header.Children.Add(rightPanel);

            return header;
        }

        private UIElement BuildNavigationRail()
        {
            Grid railGrid = new Grid
            {
                Background = _palette.WindowBackground
            };
            railGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            railGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _navTopStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4, 6, 4, 0)
            };

            _navBottomStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4, 0, 4, 10)
            };

            Grid.SetRow(_navTopStack, 0);
            Grid.SetRow(_navBottomStack, 1);
            railGrid.Children.Add(_navTopStack);
            railGrid.Children.Add(_navBottomStack);

            UpdateNavRailBadges();
            return railGrid;
        }

        private void UpdateNavRailBadges()
        {
            if (_navTopStack == null || _navBottomStack == null) return;
            _navTopStack.Children.Clear();
            _navBottomStack.Children.Clear();

            string themeSuffix = _isDarkTheme ? "dark" : "light";

            _navTopStack.Children.Add(CreateNavRailButton(
                "home",
                "Home",
                @"Assets\JumpListIcons\home.theme-" + themeSuffix + ".png",
                StoreTheme.GlyphHome,
                0,
                false));

            _navTopStack.Children.Add(CreateNavRailButton(
                "library",
                "Library",
                @"Assets\JumpListIcons\myLibrary.theme-" + themeSuffix + ".png",
                StoreTheme.GlyphLibrary,
                _installedItems.Count,
                false));

            int updatesBadge = _updateItems.Count + _activeTasks.Count(t => !t.IsCompleted);
            _navTopStack.Children.Add(CreateNavRailButton(
                "updates",
                "Updates",
                @"Assets\JumpListIcons\downloadsAndUpdates.theme-" + themeSuffix + ".png",
                StoreTheme.GlyphUpdates,
                updatesBadge,
                _updateItems.Count > 0));

            _navBottomStack.Children.Add(CreateNavRailButton(
                "settings",
                "Settings",
                null,
                StoreTheme.GlyphSettings,
                0,
                false));
        }

        private UIElement CreateNavRailButton(string tabKey, string label, string assetRelPath, string fallbackGlyph, int badgeCount, bool highlightBadge)
        {
            bool isSelected = (_activeTab == tabKey && _currentPdpItem == null);

            Border outer = new Border
            {
                Height = 58,
                Margin = new Thickness(2, 2, 2, 2),
                CornerRadius = new CornerRadius(6),
                Background = isSelected ? _palette.CardBackground : Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            outer.MouseEnter += (s, e) =>
            {
                if (!(_activeTab == tabKey && _currentPdpItem == null))
                    outer.Background = _palette.SubtleSurfaceBackground;
            };
            outer.MouseLeave += (s, e) =>
            {
                if (!(_activeTab == tabKey && _currentPdpItem == null))
                    outer.Background = Brushes.Transparent;
            };

            Grid grid = new Grid();

            // Left active selection pill (from Microsoft Store NavigationResourceDictionary.xaml)
            if (isSelected)
            {
                Border activeBar = new Border
                {
                    Width = 3,
                    Height = 20,
                    CornerRadius = new CornerRadius(1.5),
                    Background = _palette.AccentTextForeground,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                grid.Children.Add(activeBar);
            }

            StackPanel centerStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid iconHolder = new Grid
            {
                Width = 28,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var bmp = !string.IsNullOrEmpty(assetRelPath) ? StoreTheme.LoadAssetImage(assetRelPath) : null;
            if (bmp != null)
            {
                Image img = new Image
                {
                    Source = bmp,
                    Width = 20,
                    Height = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = isSelected ? 1.0 : 0.82
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                iconHolder.Children.Add(img);
            }
            else
            {
                TextBlock glyphText = new TextBlock
                {
                    Text = fallbackGlyph,
                    FontFamily = StoreTheme.GetIconFont(),
                    FontSize = 17,
                    Foreground = isSelected ? _palette.AccentTextForeground : _palette.TextSecondary,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconHolder.Children.Add(glyphText);
            }

            if (badgeCount > 0)
            {
                Border badge = new Border
                {
                    Background = highlightBadge ? _palette.AccentBrush : _palette.CardBorder,
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(4, 0, 4, 0),
                    MinWidth = 14,
                    Height = 14,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -3, -6, 0),
                    Child = new TextBlock
                    {
                        Text = badgeCount > 99 ? "99+" : badgeCount.ToString(),
                        Foreground = Brushes.White,
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                iconHolder.Children.Add(badge);
            }

            centerStack.Children.Add(iconHolder);

            TextBlock lbl = new TextBlock
            {
                Text = label,
                Foreground = isSelected ? _palette.TextPrimary : _palette.TextSecondary,
                FontSize = 10.5,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            centerStack.Children.Add(lbl);

            grid.Children.Add(centerStack);
            outer.Child = grid;

            outer.MouseLeftButtonUp += (s, e) =>
            {
                _currentPdpItem = null;
                _activeTab = tabKey;
                UpdateBackButtonVisibility();
                UpdateNavRailBadges();
                RenderActiveView();
            };

            return outer;
        }

        private void NavigateBack()
        {
            if (_currentPdpItem != null)
            {
                _currentPdpItem = null;
                UpdateBackButtonVisibility();
                UpdateNavRailBadges();
                RenderActiveView();
                return;
            }

            if (_isShowingSearchResults)
            {
                _isShowingSearchResults = false;
                _lastSearchQuery = "";
                if (_searchBox != null) _searchBox.Text = "";
                UpdateBackButtonVisibility();
                RenderActiveView();
            }
        }

        private void UpdateBackButtonVisibility()
        {
            if (_backButton == null) return;
            _backButton.Visibility = (_currentPdpItem != null || _isShowingSearchResults)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async Task ExecuteWinGetSearchAsync(string query)
        {
            string trimmed = (query ?? "").Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                _isShowingSearchResults = false;
                _lastSearchQuery = "";
                UpdateBackButtonVisibility();
                RenderActiveView();
                return;
            }

            _currentPdpItem = null;
            _activeTab = "home";
            _isShowingSearchResults = true;
            _lastSearchQuery = trimmed;
            UpdateBackButtonVisibility();
            UpdateNavRailBadges();

            SetHeaderStatus("Searching '" + trimmed + "' in WinGet...");
            ShowLoadingOverlay("Querying the official WinGet repository for \"" + trimmed + "\"...");

            var found = await _winget.SearchPackagesAsync(trimmed);
            SyncPackageStates(found);
            _searchResults = found;

            SetHeaderStatus(found.Count + " results in WinGet • No Account");
            RenderActiveView();
        }

        private void ShowLoadingOverlay(string message)
        {
            _contentHostGrid.Children.Clear();
            StackPanel sp = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ProgressBar pb = new ProgressBar
            {
                IsIndeterminate = true,
                Width = 220,
                Height = 4,
                Foreground = _palette.AccentTextForeground,
                Background = _palette.CardBorder,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 14)
            };
            TextBlock tb = new TextBlock
            {
                Text = message,
                Foreground = _palette.TextSecondary,
                FontSize = 13.5,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sp.Children.Add(pb);
            sp.Children.Add(tb);
            _contentHostGrid.Children.Add(sp);
        }

        private void RenderActiveView()
        {
            if (_contentHostGrid == null) return;
            _contentHostGrid.Children.Clear();

            if (_currentPdpItem != null)
            {
                _contentHostGrid.Children.Add(BuildPdpView(_currentPdpItem));
                return;
            }

            if (_activeTab == "home")
                _contentHostGrid.Children.Add(BuildHomeAndSearchView());
            else if (_activeTab == "library")
                _contentHostGrid.Children.Add(BuildLibraryView());
            else if (_activeTab == "updates")
                _contentHostGrid.Children.Add(BuildUpdatesView());
            else if (_activeTab == "settings")
                _contentHostGrid.Children.Add(BuildSettingsView());
        }

        // =========================================================================
        // 1. HOME & SEARCH (Clean Microsoft Store Browse / SearchPageV2Layout)
        // =========================================================================
        private UIElement BuildHomeAndSearchView()
        {
            ScrollViewer sv = CreateSmoothScrollViewer();
            StackPanel page = new StackPanel
            {
                Margin = new Thickness(32, 24, 32, 36)
            };

            // Top Header + Category Pills (PillControl.xbf / Segmented.xaml)
            Grid headerRow = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel();
            TextBlock pageTitle = new TextBlock
            {
                Text = _isShowingSearchResults
                    ? "Search results for \"" + _lastSearchQuery + "\""
                    : "Application Catalog",
                Foreground = _palette.TextPrimary,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold
            };
            titleStack.Children.Add(pageTitle);

            TextBlock pageSub = new TextBlock
            {
                Text = _isShowingSearchResults
                    ? _searchResults.Count + " packages found directly in the official WinGet repository (click a card for details or Get to install)."
                    : "Original Microsoft Store UI reconstructed natively on Windows 10 — zero ads, zero bloat, and no Microsoft account required.",
                Foreground = _palette.TextSecondary,
                FontSize = 12.5,
                Margin = new Thickness(0, 4, 0, 0)
            };
            titleStack.Children.Add(pageSub);
            Grid.SetColumn(titleStack, 0);
            headerRow.Children.Add(titleStack);

            if (_isShowingSearchResults)
            {
                Button backToCatalogBtn = CreateSecondaryButton(StoreTheme.GlyphBack, "Back to Essential Catalog");
                backToCatalogBtn.VerticalAlignment = VerticalAlignment.Center;
                backToCatalogBtn.Click += (s, e) => NavigateBack();
                Grid.SetColumn(backToCatalogBtn, 1);
                headerRow.Children.Add(backToCatalogBtn);
            }
            page.Children.Add(headerRow);

            // Category Filter Pills when browsing curated catalog
            if (!_isShowingSearchResults)
            {
                WrapPanel pillBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 20) };
                foreach (string cat in CuratedCatalog.Categories)
                {
                    string currentCat = cat;
                    bool isSelected = string.Equals(_selectedCategory, currentCat, StringComparison.OrdinalIgnoreCase);

                    Border pill = new Border
                    {
                        Background = isSelected ? _palette.AccentBrush : _palette.CardBackground,
                        BorderBrush = isSelected ? _palette.AccentBrush : _palette.CardBorder,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(16),
                        Padding = new Thickness(14, 6, 14, 6),
                        Margin = new Thickness(0, 0, 8, 8),
                        Cursor = Cursors.Hand,
                        Child = new TextBlock
                        {
                            Text = currentCat,
                            Foreground = isSelected ? Brushes.White : _palette.TextPrimary,
                            FontSize = 12.5,
                            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal
                        }
                    };

                    pill.MouseLeftButtonUp += (s, e) =>
                    {
                        _selectedCategory = currentCat;
                        RenderActiveView();
                    };
                    pillBar.Children.Add(pill);
                }
                page.Children.Add(pillBar);
            }

            List<PackageItem> visiblePackages;
            if (_isShowingSearchResults)
            {
                visiblePackages = _searchResults;
            }
            else if (_selectedCategory == "All")
            {
                visiblePackages = _curatedItems;
            }
            else
            {
                visiblePackages = _curatedItems
                    .Where(p => string.Equals(p.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (visiblePackages.Count == 0)
            {
                page.Children.Add(BuildEmptyStateCard(
                    "No packages found",
                    "Try searching for another application name or package ID in the top search bar (e.g., vscode, 7zip, vlc, git, firefox)."));
            }
            else
            {
                // Responsive Grid of Product Cards (CompactProductCard.xbf + FlexProductTileWithInlineInstall.xbf)
                WrapPanel cardsWrap = new WrapPanel
                {
                    Orientation = Orientation.Horizontal
                };

                foreach (var pkg in visiblePackages)
                {
                    cardsWrap.Children.Add(CreateStoreProductCard(pkg));
                }

                page.Children.Add(cardsWrap);
            }

            sv.Content = page;
            return sv;
        }

        private UIElement CreateStoreProductCard(PackageItem pkg)
        {
            Border card = new Border
            {
                Width = 345,
                Height = 148,
                Background = _palette.CardBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 14, 14),
                Padding = new Thickness(16, 14, 16, 14),
                Cursor = Cursors.Hand
            };

            card.MouseEnter += (s, e) => card.Background = _palette.CardHoverBackground;
            card.MouseLeave += (s, e) => card.Background = _palette.CardBackground;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top row: Icon Tile (48x48) + Title/Publisher + Inline Install CTA Button
            Grid topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border appIconTile = CreateAppIconTile(pkg, 48, 15);
            Grid.SetColumn(appIconTile, 0);
            topRow.Children.Add(appIconTile);

            StackPanel metaStack = new StackPanel
            {
                Margin = new Thickness(12, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock titleBlock = new TextBlock
            {
                Text = pkg.Name,
                Foreground = _palette.TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            metaStack.Children.Add(titleBlock);

            TextBlock subBlock = new TextBlock
            {
                Text = pkg.DisplaySubtext,
                Foreground = _palette.TextSecondary,
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            };
            metaStack.Children.Add(subBlock);

            Grid.SetColumn(metaStack, 1);
            topRow.Children.Add(metaStack);

            Button inlineActionBtn = CreateInlineCardActionButton(pkg);
            Grid.SetColumn(inlineActionBtn, 2);
            topRow.Children.Add(inlineActionBtn);

            Grid.SetRow(topRow, 0);
            root.Children.Add(topRow);

            // Middle row: Short description (ProductDescription from FlexProductTileWithInlineInstall.xbf)
            TextBlock descBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(pkg.Description)
                    ? "Official WinGet package (" + pkg.Id + ")"
                    : pkg.Description,
                Foreground = _palette.TextSecondary,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 34,
                Margin = new Thickness(0, 10, 0, 6),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(descBlock, 1);
            root.Children.Add(descBlock);

            // Bottom row: Package ID + Version pill + InlineInstallProgressBarControl
            Grid bottomRow = new Grid();
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock idText = new TextBlock
            {
                Text = pkg.Id,
                Foreground = _palette.TextTertiary,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(idText, 0);
            bottomRow.Children.Add(idText);

            TextBlock verText = new TextBlock
            {
                Text = pkg.VersionSummary,
                Foreground = pkg.HasUpdate ? _palette.UpdateBadgeForeground : _palette.TextTertiary,
                FontSize = 11,
                FontWeight = pkg.HasUpdate ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(verText, 1);
            bottomRow.Children.Add(verText);

            Grid.SetRow(bottomRow, 2);
            root.Children.Add(bottomRow);

            card.Child = root;

            card.MouseLeftButtonUp += async (s, e) =>
            {
                await OpenProductDetailPageAsync(pkg);
            };

            return card;
        }

        private Button CreateInlineCardActionButton(PackageItem pkg)
        {
            bool isInstalledOnly = pkg.IsInstalled && !pkg.HasUpdate && !pkg.IsBusy;
            Brush bg = pkg.IsBusy
                ? _palette.SubtleSurfaceBackground
                : (pkg.HasUpdate
                    ? _palette.AccentBrush
                    : (isInstalledOnly ? _palette.InstalledBadgeBackground : _palette.SubtleSurfaceBackground));

            Brush fg = pkg.IsBusy
                ? _palette.TextSecondary
                : (pkg.HasUpdate
                    ? Brushes.White
                    : (isInstalledOnly ? _palette.InstalledBadgeForeground : _palette.AccentTextForeground));

            Border pill = new Border
            {
                Background = bg,
                BorderBrush = isInstalledOnly ? _palette.InstalledBadgeBackground : _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 5, 12, 5),
                Child = new TextBlock
                {
                    Text = pkg.PrimaryActionText,
                    Foreground = fg,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold
                }
            };

            Button btn = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top,
                Content = pill
            };
            btn.Template = CreateRawButtonTemplate();

            btn.Click += async (s, e) =>
            {
                e.Handled = true;
                if (pkg.IsBusy) return;
                if (pkg.HasUpdate)
                    await StartPackageOperationAsync(pkg, "upgrade");
                else if (!pkg.IsInstalled)
                    await StartPackageOperationAsync(pkg, "install");
                else
                    await OpenProductDetailPageAsync(pkg);
            };

            return btn;
        }

        // =========================================================================
        // 2. PRODUCT DETAIL PAGE (PDP — ImmersiveProductIdentity.xbf)
        // =========================================================================
        private async Task OpenProductDetailPageAsync(PackageItem pkg)
        {
            _currentPdpItem = pkg;
            UpdateBackButtonVisibility();
            UpdateNavRailBadges();
            RenderActiveView();

            SetHeaderStatus("Loading manifest metadata for " + pkg.Name + " via winget show...");
            await _winget.EnrichPackageDetailsAsync(pkg);
            IconResolver.ForceRefreshIconFromHomepage(pkg);
            SetHeaderStatus("WinGet " + _wingetVersionText + " • No Account");

            if (_currentPdpItem == pkg)
                RenderActiveView();
        }

        private UIElement BuildPdpView(PackageItem pkg)
        {
            ScrollViewer sv = CreateSmoothScrollViewer();
            StackPanel page = new StackPanel
            {
                Margin = new Thickness(36, 24, 36, 40)
            };

            // Top Hero Identity Card (ImmersiveProductIdentity.xbf / BuyboxContainer)
            Border heroCard = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(28, 26, 28, 26),
                Margin = new Thickness(0, 0, 0, 20)
            };

            Grid heroGrid = new Grid();
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border bigTile = CreateAppIconTile(pkg, 84, 26);
            bigTile.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(bigTile, 0);
            heroGrid.Children.Add(bigTile);

            StackPanel identityInfo = new StackPanel
            {
                Margin = new Thickness(22, 0, 22, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock appTitle = new TextBlock
            {
                Text = pkg.Name,
                Foreground = _palette.TextPrimary,
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            identityInfo.Children.Add(appTitle);

            // Publisher + Category + Installed status (RatingsAndGenresPanel from ImmersiveProductIdentity.xbf)
            WrapPanel metaRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 10) };

            TextBlock pubText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(pkg.Publisher) ? pkg.Id : pkg.Publisher,
                Foreground = _palette.AccentTextForeground,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            metaRow.Children.Add(pubText);

            metaRow.Children.Add(CreateSmallBadge(pkg.Category, _palette.SubtleSurfaceBackground, _palette.TextSecondary));
            metaRow.Children.Add(CreateSmallBadge("Version: " + pkg.VersionSummary, _palette.SubtleSurfaceBackground, _palette.TextSecondary));

            if (pkg.IsInstalled)
            {
                metaRow.Children.Add(CreateSmallBadge(
                    pkg.HasUpdate ? "Update Available" : "Installed on this PC",
                    pkg.HasUpdate ? _palette.UpdateBadgeBackground : _palette.InstalledBadgeBackground,
                    pkg.HasUpdate ? _palette.UpdateBadgeForeground : _palette.InstalledBadgeForeground));
            }
            identityInfo.Children.Add(metaRow);

            // CLI Command Copy Box
            Border cliBox = new Border
            {
                Background = _palette.SubtleSurfaceBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 8, 6),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            StackPanel cliRow = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock cliText = new TextBlock
            {
                Text = pkg.CliInstallCommand,
                Foreground = _palette.TextSecondary,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            cliRow.Children.Add(cliText);

            Button copyCmdBtn = CreateSecondaryButton(StoreTheme.GlyphCopy, "Copy");
            copyCmdBtn.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(pkg.CliInstallCommand);
                    SetHeaderStatus("CLI command copied to clipboard!");
                }
                catch { }
            };
            cliRow.Children.Add(copyCmdBtn);
            cliBox.Child = cliRow;
            identityInfo.Children.Add(cliBox);

            if (pkg.IsBusy)
            {
                StackPanel progressPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
                ProgressBar pb = new ProgressBar
                {
                    IsIndeterminate = true,
                    Height = 5,
                    Foreground = _palette.AccentTextForeground,
                    Background = _palette.CardBorder,
                    BorderThickness = new Thickness(0)
                };
                TextBlock st = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(pkg.StatusText) ? "Executing WinGet operation..." : pkg.StatusText,
                    Foreground = _palette.AccentTextForeground,
                    FontSize = 12,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                progressPanel.Children.Add(pb);
                progressPanel.Children.Add(st);
                identityInfo.Children.Add(progressPanel);
            }

            Grid.SetColumn(identityInfo, 1);
            heroGrid.Children.Add(identityInfo);

            // Right CTA Column (PdpCtaButton.xbf + PdpInstallControl.xbf)
            StackPanel ctaColumn = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 175
            };

            if (!pkg.IsInstalled || pkg.HasUpdate)
            {
                string ctaLabel = pkg.IsBusy
                    ? "Working..."
                    : (pkg.HasUpdate ? "Update Now" : "Install via WinGet");
                Button mainCta = CreatePrimaryAccentButton(StoreTheme.GlyphDownload, ctaLabel);
                mainCta.Height = 40;
                mainCta.IsEnabled = !pkg.IsBusy;
                mainCta.Click += async (s, e) =>
                {
                    await StartPackageOperationAsync(pkg, pkg.HasUpdate ? "upgrade" : "install");
                };
                ctaColumn.Children.Add(mainCta);
            }
            else
            {
                Button reinstallBtn = CreateSecondaryButton(StoreTheme.GlyphCheckMark, "Installed (" + pkg.Version + ")");
                reinstallBtn.Height = 38;
                reinstallBtn.IsEnabled = false;
                ctaColumn.Children.Add(reinstallBtn);
            }

            if (pkg.IsInstalled)
            {
                Button uninstallBtn = CreateSecondaryButton(StoreTheme.GlyphDelete, "Uninstall Package");
                uninstallBtn.Height = 36;
                uninstallBtn.Margin = new Thickness(0, 8, 0, 0);
                uninstallBtn.IsEnabled = !pkg.IsBusy;
                uninstallBtn.Click += async (s, e) =>
                {
                    await StartPackageOperationAsync(pkg, "uninstall");
                };
                ctaColumn.Children.Add(uninstallBtn);
            }

            if (!string.IsNullOrWhiteSpace(pkg.Homepage))
            {
                Button webBtn = CreateSecondaryButton(StoreTheme.GlyphOpenInNewWindow, "Official Website");
                webBtn.Height = 34;
                webBtn.Margin = new Thickness(0, 8, 0, 0);
                webBtn.Click += (s, e) => OpenExternalUrl(pkg.Homepage);
                ctaColumn.Children.Add(webBtn);
            }

            Grid.SetColumn(ctaColumn, 2);
            heroGrid.Children.Add(ctaColumn);

            heroCard.Child = heroGrid;
            page.Children.Add(heroCard);

            // Two-column Details Layout (ResponsivePageLayout.xbf: FlexibleProductDescription + InfoPane)
            Grid detailsGrid = new Grid();
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.65, GridUnitType.Star) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel leftCol = new StackPanel();

            // Description Card
            Border descCard = CreateSectionCard("Description",
                string.IsNullOrWhiteSpace(pkg.Description)
                    ? "Loading detailed description directly from the WinGet package manifest..."
                    : pkg.Description);
            leftCol.Children.Add(descCard);

            // Release Notes Card (VersionNotes.xbf)
            if (!string.IsNullOrWhiteSpace(pkg.ReleaseNotes) || !string.IsNullOrWhiteSpace(pkg.ReleaseNotesUrl))
            {
                string rnText = !string.IsNullOrWhiteSpace(pkg.ReleaseNotes)
                    ? pkg.ReleaseNotes
                    : "Release notes available at: " + pkg.ReleaseNotesUrl;
                Border rnCard = CreateSectionCard("Release Notes (" + pkg.VersionSummary + ")", rnText);
                leftCol.Children.Add(rnCard);
            }

            // Live WinGet Console Output Card
            Border consoleCard = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 16)
            };
            StackPanel consoleStack = new StackPanel();
            consoleStack.Children.Add(new TextBlock
            {
                Text = "Live WinGet Output",
                Foreground = _palette.TextPrimary,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            Border logBox = new Border
            {
                Background = _palette.WindowBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10)
            };
            _liveConsoleBlock = new TextBlock
            {
                Text = _consoleLines.Count == 0
                    ? "Ready. No active package operations running."
                    : string.Join(Environment.NewLine, _consoleLines.Skip(Math.Max(0, _consoleLines.Count - 14))),
                Foreground = _palette.TextSecondary,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            logBox.Child = _liveConsoleBlock;
            consoleStack.Children.Add(logBox);
            consoleCard.Child = consoleStack;
            leftCol.Children.Add(consoleCard);

            Grid.SetColumn(leftCol, 0);
            detailsGrid.Children.Add(leftCol);

            // Right Column: InfoPane.xbf / SystemRequirements.xbf
            Border infoCard = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 16, 20, 16),
                VerticalAlignment = VerticalAlignment.Top
            };
            StackPanel infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock
            {
                Text = "Additional Information",
                Foreground = _palette.TextPrimary,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 14)
            });

            infoStack.Children.Add(CreateMetadataRow("Package ID", pkg.Id));
            infoStack.Children.Add(CreateMetadataRow("Publisher", string.IsNullOrWhiteSpace(pkg.Publisher) ? "—" : pkg.Publisher));
            infoStack.Children.Add(CreateMetadataRow("Version", pkg.VersionSummary));
            infoStack.Children.Add(CreateMetadataRow("License", string.IsNullOrWhiteSpace(pkg.License) ? "See manifest" : pkg.License));
            infoStack.Children.Add(CreateMetadataRow("Installer Type", string.IsNullOrWhiteSpace(pkg.InstallerType) ? "WinGet Default" : pkg.InstallerType));
            infoStack.Children.Add(CreateMetadataRow("Repository (Source)", string.IsNullOrWhiteSpace(pkg.Source) ? "winget" : pkg.Source));
            infoStack.Children.Add(CreateMetadataRow("Microsoft Account", "Not Required (100% Local)"));

            if (!string.IsNullOrWhiteSpace(pkg.Tags))
            {
                infoStack.Children.Add(CreateMetadataRow("Tags", pkg.Tags));
            }

            infoCard.Child = infoStack;
            Grid.SetColumn(infoCard, 2);
            detailsGrid.Children.Add(infoCard);

            page.Children.Add(detailsGrid);
            sv.Content = page;
            return sv;
        }

        // =========================================================================
        // 3. LIBRARY (MyLibraryV2Page — Locally Installed Packages)
        // =========================================================================
        private UIElement BuildLibraryView()
        {
            ScrollViewer sv = CreateSmoothScrollViewer();
            StackPanel page = new StackPanel
            {
                Margin = new Thickness(32, 24, 32, 36)
            };

            Grid headerRow = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = "Library (" + _installedItems.Count + ")",
                Foreground = _palette.TextPrimary,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "All applications and packages installed on your Windows 10 machine detected locally via winget list.",
                Foreground = _palette.TextSecondary,
                FontSize = 12.5,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(titleStack, 0);
            headerRow.Children.Add(titleStack);

            Button refreshBtn = CreateSecondaryButton(StoreTheme.GlyphRefresh, "Refresh List");
            refreshBtn.VerticalAlignment = VerticalAlignment.Center;
            refreshBtn.Click += async (s, e) =>
            {
                ShowLoadingOverlay("Reading installed packages via winget list...");
                await RefreshInstalledAndUpdatesAsync();
                RenderActiveView();
            };
            Grid.SetColumn(refreshBtn, 1);
            headerRow.Children.Add(refreshBtn);
            page.Children.Add(headerRow);

            // Local filter box
            StackPanel listContainer = new StackPanel();
            Border filterBorder = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Height = 36,
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(12, 0, 12, 0)
            };

            _libraryFilterBox = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = _palette.TextPrimary,
                CaretBrush = _palette.AccentTextForeground,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = "Filter installed programs..."
            };
            _libraryFilterBox.TextChanged += (s, e) => PopulateInstalledRows(listContainer, _libraryFilterBox.Text);
            filterBorder.Child = _libraryFilterBox;
            page.Children.Add(filterBorder);

            PopulateInstalledRows(listContainer, "");
            page.Children.Add(listContainer);

            sv.Content = page;
            return sv;
        }

        private void PopulateInstalledRows(StackPanel container, string filterText)
        {
            container.Children.Clear();
            string q = (filterText ?? "").Trim();

            var filtered = string.IsNullOrWhiteSpace(q)
                ? _installedItems.Take(120).ToList()
                : _installedItems.Where(x =>
                    x.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    x.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).Take(120).ToList();

            if (filtered.Count == 0)
            {
                container.Children.Add(BuildEmptyStateCard(
                    "No installed packages match your filter",
                    "Try clearing the filter box or click Refresh List."));
                return;
            }

            foreach (var item in filtered)
            {
                container.Children.Add(CreateHorizontalPackageRow(item, false));
            }
        }

        // =========================================================================
        // 4. DOWNLOADS & UPDATES (DownloadsPage — winget upgrade + Queue)
        // =========================================================================
        private UIElement BuildUpdatesView()
        {
            ScrollViewer sv = CreateSmoothScrollViewer();
            StackPanel page = new StackPanel
            {
                Margin = new Thickness(32, 24, 32, 36)
            };

            Grid headerRow = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = "Downloads and updates",
                Foreground = _palette.TextPrimary,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = _updateItems.Count > 0
                    ? _updateItems.Count + " package updates available via WinGet."
                    : "All of your WinGet-managed packages are up to date.",
                Foreground = _palette.TextSecondary,
                FontSize = 12.5,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(titleStack, 0);
            headerRow.Children.Add(titleStack);

            StackPanel actionsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button checkBtn = CreateSecondaryButton(StoreTheme.GlyphRefresh, "Check for updates");
            checkBtn.Margin = new Thickness(0, 0, 10, 0);
            checkBtn.Click += async (s, e) =>
            {
                ShowLoadingOverlay("Checking for available updates via winget upgrade...");
                await RefreshInstalledAndUpdatesAsync();
                RenderActiveView();
            };
            actionsStack.Children.Add(checkBtn);

            if (_updateItems.Count > 0)
            {
                Button updateAllBtn = CreatePrimaryAccentButton(StoreTheme.GlyphDownload, "Update all (" + _updateItems.Count + ")");
                updateAllBtn.Click += async (s, e) =>
                {
                    var snapshot = _updateItems.ToList();
                    foreach (var upItem in snapshot)
                    {
                        await StartPackageOperationAsync(upItem, "upgrade");
                    }
                };
                actionsStack.Children.Add(updateAllBtn);
            }

            Grid.SetColumn(actionsStack, 1);
            headerRow.Children.Add(actionsStack);
            page.Children.Add(headerRow);

            // Active / Recent Queue Section (MultiProductInstallProgressBarControl.xbf)
            if (_activeTasks.Count > 0)
            {
                page.Children.Add(new TextBlock
                {
                    Text = "Session operation queue",
                    Foreground = _palette.TextPrimary,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                foreach (var task in _activeTasks)
                {
                    Border taskCard = new Border
                    {
                        Background = _palette.CardBackground,
                        BorderBrush = _palette.CardBorder,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16, 12, 16, 12),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    StackPanel tStack = new StackPanel();
                    Grid tTop = new Grid();
                    tTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    tTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    tTop.Children.Add(new TextBlock
                    {
                        Text = task.ActionType + ": " + task.PackageName + " (" + task.PackageId + ")",
                        Foreground = _palette.TextPrimary,
                        FontSize = 13.5,
                        FontWeight = FontWeights.SemiBold
                    });

                    TextBlock tStatus = new TextBlock
                    {
                        Text = task.Status,
                        Foreground = task.IsFailed
                            ? _palette.DangerBrush
                            : (task.IsCompleted ? _palette.InstalledBadgeForeground : _palette.AccentTextForeground),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold
                    };
                    Grid.SetColumn(tStatus, 1);
                    tTop.Children.Add(tStatus);
                    tStack.Children.Add(tTop);

                    if (!task.IsCompleted)
                    {
                        tStack.Children.Add(new ProgressBar
                        {
                            IsIndeterminate = true,
                            Height = 4,
                            Foreground = _palette.AccentTextForeground,
                            Background = _palette.CardBorder,
                            BorderThickness = new Thickness(0),
                            Margin = new Thickness(0, 8, 0, 6)
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(task.LastLog))
                    {
                        tStack.Children.Add(new TextBlock
                        {
                            Text = task.LastLog,
                            Foreground = _palette.TextTertiary,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 11,
                            Margin = new Thickness(0, 4, 0, 0)
                        });
                    }

                    taskCard.Child = tStack;
                    page.Children.Add(taskCard);
                }
            }

            // Upgradable Packages List
            page.Children.Add(new TextBlock
            {
                Text = "Pending updates (" + _updateItems.Count + ")",
                Foreground = _palette.TextPrimary,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 12)
            });

            if (_updateItems.Count == 0)
            {
                page.Children.Add(BuildEmptyStateCard(
                    "Everything is up to date!",
                    "No pending package updates were reported by winget upgrade."));
            }
            else
            {
                foreach (var item in _updateItems)
                {
                    page.Children.Add(CreateHorizontalPackageRow(item, true));
                }
            }

            sv.Content = page;
            return sv;
        }

        private UIElement CreateHorizontalPackageRow(PackageItem item, bool isUpdateRow)
        {
            Border row = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };

            row.MouseEnter += (s, e) => row.Background = _palette.CardHoverBackground;
            row.MouseLeave += (s, e) => row.Background = _palette.CardBackground;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border icon = CreateAppIconTile(item, 42, 13);
            Grid.SetColumn(icon, 0);
            g.Children.Add(icon);

            StackPanel mid = new StackPanel
            {
                Margin = new Thickness(14, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            mid.Children.Add(new TextBlock
            {
                Text = item.Name,
                Foreground = _palette.TextPrimary,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            mid.Children.Add(new TextBlock
            {
                Text = item.Id + " • Source: " + (string.IsNullOrWhiteSpace(item.Source) ? "winget" : item.Source),
                Foreground = _palette.TextTertiary,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(mid, 1);
            g.Children.Add(mid);

            Border verBadge = CreateSmallBadge(
                item.VersionSummary,
                isUpdateRow ? _palette.UpdateBadgeBackground : _palette.SubtleSurfaceBackground,
                isUpdateRow ? _palette.UpdateBadgeForeground : _palette.TextSecondary);
            verBadge.VerticalAlignment = VerticalAlignment.Center;
            verBadge.Margin = new Thickness(0, 0, 14, 0);
            Grid.SetColumn(verBadge, 2);
            g.Children.Add(verBadge);

            StackPanel btns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isUpdateRow)
            {
                Button upBtn = CreatePrimaryAccentButton(StoreTheme.GlyphDownload, item.IsBusy ? "Updating..." : "Update");
                upBtn.IsEnabled = !item.IsBusy;
                upBtn.Click += async (s, e) =>
                {
                    e.Handled = true;
                    await StartPackageOperationAsync(item, "upgrade");
                };
                btns.Children.Add(upBtn);
            }
            else
            {
                Button uninstBtn = CreateSecondaryButton(StoreTheme.GlyphDelete, item.IsBusy ? "Removing..." : "Uninstall");
                uninstBtn.IsEnabled = !item.IsBusy;
                uninstBtn.Click += async (s, e) =>
                {
                    e.Handled = true;
                    await StartPackageOperationAsync(item, "uninstall");
                };
                btns.Children.Add(uninstBtn);
            }

            Grid.SetColumn(btns, 3);
            g.Children.Add(btns);

            row.Child = g;
            row.MouseLeftButtonUp += async (s, e) => await OpenProductDetailPageAsync(item);
            return row;
        }

        // =========================================================================
        // 5. SETTINGS (SettingsPage — SettingsExpander.xaml style)
        // =========================================================================
        private UIElement BuildSettingsView()
        {
            ScrollViewer sv = CreateSmoothScrollViewer();
            StackPanel page = new StackPanel
            {
                Margin = new Thickness(32, 24, 32, 36),
                MaxWidth = 860,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            page.Children.Add(new TextBlock
            {
                Text = "Settings",
                Foreground = _palette.TextPrimary,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            page.Children.Add(new TextBlock
            {
                Text = "Customize WinGet execution behavior and the visual theme extracted from the original Microsoft Store.",
                Foreground = _palette.TextSecondary,
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 0, 20)
            });

            // 1. Theme Switcher (Dark.xbf / Light.xbf)
            Button themeBtn = CreateSecondaryButton(StoreTheme.GlyphInfo, _isDarkTheme ? "Switch to Light Theme (Light.xbf)" : "Switch to Dark Theme (Dark.xbf)");
            themeBtn.Click += (s, e) =>
            {
                _isDarkTheme = !_isDarkTheme;
                RebuildShell();
            };
            page.Children.Add(CreateSettingRowCard(
                "Interface Theme (WinStore.Resources\\Themes)",
                "Uses the exact color tokens extracted from Dark.xbf and Light.xbf in the official Microsoft Store package.",
                themeBtn));

            // 2. Install Mode (--silent vs --interactive)
            Button modeBtn = CreateSecondaryButton(StoreTheme.GlyphSettings, _silentInstall ? "Silent (--silent)" : "Interactive (--interactive)");
            modeBtn.Click += (s, e) =>
            {
                _silentInstall = !_silentInstall;
                RenderActiveView();
            };
            page.Children.Add(CreateSettingRowCard(
                "WinGet Installation Mode",
                "In silent mode, packages are installed seamlessly in the background just like the Microsoft Store, without installer popups.",
                modeBtn));

            // 3. Install Scope (auto / user / machine)
            Button scopeBtn = CreateSecondaryButton(StoreTheme.GlyphShield, "Scope: " + _installScope.ToUpperInvariant());
            scopeBtn.Click += (s, e) =>
            {
                if (_installScope == "auto") _installScope = "user";
                else if (_installScope == "user") _installScope = "machine";
                else _installScope = "auto";
                RenderActiveView();
            };
            page.Children.Add(CreateSettingRowCard(
                "Default Package Scope",
                "Select whether WinGet should prefer per-user (user), system-wide (machine), or automatic (auto) package installation.",
                scopeBtn));

            // 4. Privacy & No Microsoft Account Verification Card
            page.Children.Add(CreateSectionCard(
                "Privacy, Zero Ads & No Microsoft Account",
                "• Account Control Stripped: MeControl / PersonPicture / XboxAuthManager completely removed — 100% local execution.\n" +
                "• Ad & Upsell Modules Eliminated: TileAdsBadge.xbf, PromotionalBar.xbf, UpsellModule.xbf, GamePassHeroUnitModule.xbf, Microsoft365HeroUnitModule.xbf, and AIHub stripped.\n" +
                "• Original Store Assets Reconstructed via REA:\n" +
                "  - Base package: Microsoft.WindowsStore_22608.1401.7.0_x64__8wekyb3d8bbwe\n" +
                "  - Native icon fonts: StoreIcons.ttf (Store Fluent Icons) and StorMDL2.ttf\n" +
                "  - Mapped deployment metadata: Microsoft.Management.Deployment.winmd"));

            // 5. Active WinGet Sources
            page.Children.Add(CreateSectionCard(
                "Active WinGet Sources (" + _wingetVersionText + ")",
                string.IsNullOrWhiteSpace(_sourcesSummaryText) ? "winget (https://cdn.winget.microsoft.com/cache)" : _sourcesSummaryText));

            sv.Content = page;
            return sv;
        }

        // =========================================================================
        // WINGET PACKAGE OPERATIONS (Install, Upgrade, Uninstall)
        // =========================================================================
        private async Task StartPackageOperationAsync(PackageItem pkg, string action)
        {
            if (pkg == null || pkg.IsBusy) return;

            string actionLabel = action == "install" ? "Install" : (action == "upgrade" ? "Update" : "Uninstall");
            pkg.IsBusy = true;
            pkg.StatusText = action == "install" ? "Installing..." : (action == "upgrade" ? "Updating..." : "Removing...");

            var taskItem = new InstallTaskItem
            {
                PackageId = pkg.Id,
                PackageName = pkg.Name,
                ActionType = actionLabel,
                StartedAt = DateTime.Now,
                Status = "Running winget " + action + "..."
            };
            _activeTasks.Insert(0, taskItem);
            UpdateNavRailBadges();
            RenderActiveView();

            SetHeaderStatus(actionLabel + "ing " + pkg.Name + " via WinGet...");

            bool ok = await _winget.RunPackageActionAsync(
                action,
                pkg.Id,
                _silentInstall,
                _installScope,
                line =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        taskItem.LastLog = line;
                        pkg.StatusText = line.Length > 22 ? line.Substring(0, 22) + "..." : line;
                        _consoleLines.Add("[" + pkg.Id + "] " + line);
                        if (_liveConsoleBlock != null)
                        {
                            _liveConsoleBlock.Text = string.Join(
                                Environment.NewLine,
                                _consoleLines.Skip(Math.Max(0, _consoleLines.Count - 14)));
                        }
                    }));
                });

            pkg.IsBusy = false;
            pkg.StatusText = "";
            taskItem.IsCompleted = true;
            taskItem.IsFailed = !ok;
            taskItem.Status = ok ? "Completed successfully" : "Failed or canceled";

            if (ok)
            {
                if (action == "install" || action == "upgrade")
                {
                    pkg.IsInstalled = true;
                    pkg.HasUpdate = false;
                    if (!string.IsNullOrWhiteSpace(pkg.AvailableVersion))
                    {
                        pkg.Version = pkg.AvailableVersion;
                        pkg.AvailableVersion = "";
                    }
                }
                else if (action == "uninstall")
                {
                    pkg.IsInstalled = false;
                    pkg.HasUpdate = false;
                }
            }

            await RefreshInstalledAndUpdatesAsync();
            SetHeaderStatus(ok
                ? pkg.Name + ": " + actionLabel + " completed!"
                : pkg.Name + ": operation finished with warning.");
            RenderActiveView();
        }

        // =========================================================================
        // FLUENT / MICROSOFT STORE VISUAL HELPERS
        // =========================================================================
        private Border CreateAppIconTile(PackageItem pkg, double size, double fontSize)
        {
            IconResolver.ResolveIconForPackage(pkg);

            double pad = Math.Max(4, Math.Round(size * 0.12));
            bool hasImage = pkg.IconSource != null;

            TextBlock initialsBlock = new TextBlock
            {
                Text = pkg.IconInitials,
                Foreground = Brushes.White,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible
            };

            Image iconImage = new Image
            {
                Source = pkg.IconSource,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(pad),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed
            };
            RenderOptions.SetBitmapScalingMode(iconImage, BitmapScalingMode.HighQuality);

            Grid host = new Grid();
            host.Children.Add(initialsBlock);
            host.Children.Add(iconImage);

            Border tileBorder = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(Math.Round(size * 0.22)),
                Background = hasImage ? _palette.SubtleSurfaceBackground : StoreTheme.BrushFromHex(pkg.AccentHex),
                BorderBrush = hasImage ? _palette.CardBorder : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Child = host
            };

            System.ComponentModel.PropertyChangedEventHandler handler = (s, e) =>
            {
                if (e.PropertyName == "IconSource")
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        bool nowHasImg = pkg.IconSource != null;
                        iconImage.Source = pkg.IconSource;
                        iconImage.Visibility = nowHasImg ? Visibility.Visible : Visibility.Collapsed;
                        initialsBlock.Visibility = nowHasImg ? Visibility.Collapsed : Visibility.Visible;
                        tileBorder.Background = nowHasImg ? _palette.SubtleSurfaceBackground : StoreTheme.BrushFromHex(pkg.AccentHex);
                        tileBorder.BorderBrush = nowHasImg ? _palette.CardBorder : Brushes.Transparent;
                    }));
                }
            };
            pkg.PropertyChanged += handler;
            tileBorder.Unloaded += (s, e) => pkg.PropertyChanged -= handler;

            return tileBorder;
        }

        private Border CreateSmallBadge(string text, Brush bg, Brush fg)
        {
            return new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 8, 4),
                Child = new TextBlock
            {
                    Text = text,
                    Foreground = fg,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold
                }
            };
        }

        private Border CreateSectionCard(string title, string body)
        {
            Border card = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 16)
            };
            StackPanel sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = _palette.TextPrimary,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            sp.Children.Add(new TextBlock
            {
                Text = body,
                Foreground = _palette.TextSecondary,
                FontSize = 13,
                LineHeight = 20,
                TextWrapping = TextWrapping.Wrap
            });
            card.Child = sp;
            return card;
        }

        private Border CreateSettingRowCard(string title, string subtitle, UIElement rightControl)
        {
            Border card = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = _palette.TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            });
            left.Children.Add(new TextBlock
            {
                Text = subtitle,
                Foreground = _palette.TextSecondary,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            if (rightControl != null)
            {
                Grid.SetColumn(rightControl, 1);
                g.Children.Add(rightControl);
            }

            card.Child = g;
            return card;
        }

        private UIElement CreateMetadataRow(string label, string value)
        {
            StackPanel sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = _palette.TextTertiary,
                FontSize = 11.5
            });
            sp.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = _palette.TextPrimary,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            return sp;
        }

        private UIElement BuildEmptyStateCard(string title, string subtitle)
        {
            Border card = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(32, 36, 32, 36),
                Margin = new Thickness(0, 8, 0, 8)
            };
            StackPanel sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = _palette.TextPrimary,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = subtitle,
                Foreground = _palette.TextSecondary,
                FontSize = 12.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            });
            card.Child = sp;
            return card;
        }

        private Button CreatePrimaryAccentButton(string glyph, string text)
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            if (!string.IsNullOrEmpty(glyph))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = glyph,
                    FontFamily = StoreTheme.GetIconFont(),
                    Foreground = Brushes.White,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });
            }
            sp.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            Border b = new Border
            {
                Background = _palette.AccentBrush,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 7, 16, 7),
                Child = sp
            };

            Button btn = new Button
            {
                Content = b,
                Cursor = Cursors.Hand,
                Template = CreateRawButtonTemplate()
            };
            btn.MouseEnter += (s, e) => b.Background = _palette.AccentHoverBrush;
            btn.MouseLeave += (s, e) => b.Background = _palette.AccentBrush;
            return btn;
        }

        private Button CreateSecondaryButton(string glyph, string text)
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            if (!string.IsNullOrEmpty(glyph))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = glyph,
                    FontFamily = StoreTheme.GetIconFont(),
                    Foreground = _palette.TextPrimary,
                    FontSize = 12.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 7, 0)
                });
            }
            sp.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = _palette.TextPrimary,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            Border b = new Border
            {
                Background = _palette.SubtleSurfaceBackground,
                BorderBrush = _palette.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Child = sp
            };

            Button btn = new Button
            {
                Content = b,
                Cursor = Cursors.Hand,
                Template = CreateRawButtonTemplate()
            };
            btn.MouseEnter += (s, e) => b.Background = _palette.CardHoverBackground;
            btn.MouseLeave += (s, e) => b.Background = _palette.SubtleSurfaceBackground;
            return btn;
        }

        private Button CreateIconButton(string glyph, string tooltip, double width, double height)
        {
            Border b = new Border
            {
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(5),
                Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = glyph,
                    FontFamily = StoreTheme.GetIconFont(),
                    Foreground = _palette.TextSecondary,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Button btn = new Button
            {
                Content = b,
                ToolTip = tooltip,
                Cursor = Cursors.Hand,
                Template = CreateRawButtonTemplate()
            };
            btn.MouseEnter += (s, e) => b.Background = _palette.CardHoverBackground;
            btn.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            return btn;
        }

        private Button CreateWindowCaptionButton(string glyph, string tooltip, bool isClose)
        {
            TextBlock tb = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Foreground = _palette.TextSecondary,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border b = new Border
            {
                Width = 46,
                Height = 48,
                Background = Brushes.Transparent,
                Child = tb
            };
            Button btn = new Button
            {
                Content = b,
                ToolTip = tooltip,
                Template = CreateRawButtonTemplate()
            };
            btn.MouseEnter += (s, e) =>
            {
                b.Background = isClose ? StoreTheme.BrushFromHex("#E81123") : _palette.CardHoverBackground;
                if (isClose) tb.Foreground = Brushes.White;
            };
            btn.MouseLeave += (s, e) =>
            {
                b.Background = Brushes.Transparent;
                tb.Foreground = _palette.TextSecondary;
            };
            return btn;
        }

        private ControlTemplate CreateRawButtonTemplate()
        {
            ControlTemplate ct = new ControlTemplate(typeof(Button));
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch);
            ct.VisualTree = presenter;
            return ct;
        }

        private ScrollViewer CreateSmoothScrollViewer()
        {
            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0)
            };
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void SetHeaderStatus(string text)
        {
            if (_statusBadgeText != null)
                _statusBadgeText.Text = text;
        }

        private static void OpenExternalUrl(string url)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(url) &&
                    (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
            }
            catch { }
        }
    }
}
