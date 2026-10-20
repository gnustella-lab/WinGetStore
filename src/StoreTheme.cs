using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinGetStore
{
    public class StorePalette
    {
        public bool IsDark;
        public Brush WindowBackground;
        public Brush ContentSurfaceBackground;
        public Brush ContentSurfaceBorder;
        public Brush CardBackground;
        public Brush CardHoverBackground;
        public Brush CardBorder;
        public Brush SubtleSurfaceBackground;
        public Brush TextPrimary;
        public Brush TextSecondary;
        public Brush TextTertiary;
        public Brush AccentBrush;
        public Brush AccentHoverBrush;
        public Brush AccentTextForeground;
        public Brush DividerBrush;
        public Brush InstalledBadgeBackground;
        public Brush InstalledBadgeForeground;
        public Brush UpdateBadgeBackground;
        public Brush UpdateBadgeForeground;
        public Brush DangerBrush;
        public Brush SearchBoxBackground;
        public Brush SearchBoxBorder;
    }

    public static class StoreTheme
    {
        // Exact Private Use Area glyphs extracted via REA from WinStore.Resources\Themes\Icons.xbf
        public const string GlyphBack = "\uE72B";
        public const string GlyphSearch = "\uE721";
        public const string GlyphRefresh = "\uE72C";
        public const string GlyphDownload = "\uE896";
        public const string GlyphCheckMark = "\uE73E";
        public const string GlyphDelete = "\uE74D";
        public const string GlyphOpenInNewWindow = "\uE8A7";
        public const string GlyphCopy = "\uE8C8";
        public const string GlyphInfo = "\uE946";
        public const string GlyphCancel = "\uE711";
        public const string GlyphSettings = "\uE713";
        public const string GlyphLibrary = "\uE8F1";
        public const string GlyphHome = "\uE80F";
        public const string GlyphUpdates = "\uE895";
        public const string GlyphStarFill = "\uE735";
        public const string GlyphChevronRight = "\uE76C";
        public const string GlyphFilter = "\uE71C";
        public const string GlyphShield = "\uEA18";

        private static FontFamily _iconFontFamily;

        public static string BaseDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static FontFamily GetIconFont()
        {
            if (_iconFontFamily != null) return _iconFontFamily;
            try
            {
                string fontDir = Path.Combine(BaseDir, "Fonts");
                if (Directory.Exists(fontDir))
                {
                    Uri fontUri = new Uri(fontDir + Path.DirectorySeparatorChar, UriKind.Absolute);
                    _iconFontFamily = new FontFamily(fontUri, "./#Store Fluent Icons, ./#Segoe MDL2 Assets, Segoe MDL2 Assets");
                    return _iconFontFamily;
                }
            }
            catch
            {
            }
            _iconFontFamily = new FontFamily("Segoe MDL2 Assets");
            return _iconFontFamily;
        }

        public static BitmapImage LoadAssetImage(string relativePath)
        {
            try
            {
                string fullPath = Path.Combine(BaseDir, relativePath);
                if (!File.Exists(fullPath)) return null;
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public static Brush BrushFromHex(string hex)
        {
            try
            {
                Color c = (Color)ColorConverter.ConvertFromString(hex);
                SolidColorBrush b = new SolidColorBrush(c);
                b.Freeze();
                return b;
            }
            catch
            {
                return Brushes.DodgerBlue;
            }
        }

        // Extracted from WinStore.Resources\Themes\Dark.xbf and Light.xbf
        public static StorePalette GetPalette(bool isDark)
        {
            if (isDark)
            {
                return new StorePalette
                {
                    IsDark = true,
                    WindowBackground = BrushFromHex("#1A1A1A"),
                    ContentSurfaceBackground = BrushFromHex("#212121"),
                    ContentSurfaceBorder = BrushFromHex("#2E2E2E"),
                    CardBackground = BrushFromHex("#2B2B2B"),
                    CardHoverBackground = BrushFromHex("#333333"),
                    CardBorder = BrushFromHex("#383838"),
                    SubtleSurfaceBackground = BrushFromHex("#262626"),
                    TextPrimary = BrushFromHex("#FFFFFF"),
                    TextSecondary = BrushFromHex("#C6C6C6"),
                    TextTertiary = BrushFromHex("#8E8E8E"),
                    AccentBrush = BrushFromHex("#0078D4"),
                    AccentHoverBrush = BrushFromHex("#1A86D9"),
                    AccentTextForeground = BrushFromHex("#60CDFF"),
                    DividerBrush = BrushFromHex("#323232"),
                    InstalledBadgeBackground = BrushFromHex("#1E3A2B"),
                    InstalledBadgeForeground = BrushFromHex("#6CCB5F"),
                    UpdateBadgeBackground = BrushFromHex("#3B2E14"),
                    UpdateBadgeForeground = BrushFromHex("#FCE100"),
                    DangerBrush = BrushFromHex("#D83B01"),
                    SearchBoxBackground = BrushFromHex("#282828"),
                    SearchBoxBorder = BrushFromHex("#3D3D3D")
                };
            }
            else
            {
                return new StorePalette
                {
                    IsDark = false,
                    WindowBackground = BrushFromHex("#F0F2F5"),
                    ContentSurfaceBackground = BrushFromHex("#F9F9FB"),
                    ContentSurfaceBorder = BrushFromHex("#E1E4E8"),
                    CardBackground = BrushFromHex("#FFFFFF"),
                    CardHoverBackground = BrushFromHex("#F3F4F6"),
                    CardBorder = BrushFromHex("#E4E6EB"),
                    SubtleSurfaceBackground = BrushFromHex("#EFEFF2"),
                    TextPrimary = BrushFromHex("#1B1B1B"),
                    TextSecondary = BrushFromHex("#5A5A5A"),
                    TextTertiary = BrushFromHex("#767676"),
                    AccentBrush = BrushFromHex("#0067C0"),
                    AccentHoverBrush = BrushFromHex("#005BA8"),
                    AccentTextForeground = BrushFromHex("#005FB8"),
                    DividerBrush = BrushFromHex("#E5E5E5"),
                    InstalledBadgeBackground = BrushFromHex("#DFF6DD"),
                    InstalledBadgeForeground = BrushFromHex("#0F7B0F"),
                    UpdateBadgeBackground = BrushFromHex("#FFF4CE"),
                    UpdateBadgeForeground = BrushFromHex("#9D5D00"),
                    DangerBrush = BrushFromHex("#C50F1F"),
                    SearchBoxBackground = BrushFromHex("#FFFFFF"),
                    SearchBoxBorder = BrushFromHex("#D1D5DB")
                };
            }
        }
    }
}
