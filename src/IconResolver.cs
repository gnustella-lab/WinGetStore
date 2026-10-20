using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WinGetStore
{
    public static class IconResolver
    {
        private static readonly ConcurrentDictionary<string, ImageSource> _memoryCache =
            new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, bool> _inFlight =
            new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> _registryDisplayIcons;
        private static readonly object _regLock = new object();

        static IconResolver()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
            catch { }
        }

        public static string IconsCacheDir
        {
            get
            {
                string dir = Path.Combine(StoreTheme.BaseDir, @"Assets\AppIcons");
                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { }
                }
                return dir;
            }
        }

        public static void ResolveIconForPackage(PackageItem pkg)
        {
            if (pkg == null || string.IsNullOrWhiteSpace(pkg.Id)) return;

            if (pkg.IconSource != null) return;

            ImageSource cached;
            if (_memoryCache.TryGetValue(pkg.Id, out cached) && cached != null)
            {
                pkg.IconSource = cached;
                return;
            }

            // 1. Check disk cache Assets\AppIcons\<id>.png
            string safeFileName = SanitizeFileName(pkg.Id) + ".png";
            string localPath = Path.Combine(IconsCacheDir, safeFileName);
            if (File.Exists(localPath))
            {
                var diskImg = LoadBitmapFromFile(localPath);
                if (diskImg != null)
                {
                    _memoryCache[pkg.Id] = diskImg;
                    pkg.IconSource = diskImg;
                    return;
                }
            }

            // 2. Check Windows Uninstall Registry for local installed icon (.exe / .ico)
            var regIcon = TryExtractInstalledIcon(pkg.Id, pkg.Name);
            if (regIcon != null)
            {
                _memoryCache[pkg.Id] = regIcon;
                pkg.IconSource = regIcon;
                return;
            }

            // 3. Download asynchronously in background and notify UI when ready
            if (!_inFlight.TryAdd(pkg.Id, true)) return;

            Task.Run(() =>
            {
                try
                {
                    byte[] bytes = DownloadBestIconBytes(pkg.Id, pkg.Publisher, pkg.Homepage);
                    if (bytes != null && bytes.Length > 200)
                    {
                        try { File.WriteAllBytes(localPath, bytes); } catch { }

                        Application app = Application.Current;
                        if (app != null)
                        {
                            app.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var bmp = LoadBitmapFromBytes(bytes);
                                if (bmp != null)
                                {
                                    _memoryCache[pkg.Id] = bmp;
                                    pkg.IconSource = bmp;
                                }
                            }));
                        }
                    }
                }
                catch { }
                finally
                {
                    bool removed;
                    _inFlight.TryRemove(pkg.Id, out removed);
                }
            });
        }

        public static void ForceRefreshIconFromHomepage(PackageItem pkg)
        {
            if (pkg == null || string.IsNullOrWhiteSpace(pkg.Id) || string.IsNullOrWhiteSpace(pkg.Homepage))
                return;
            if (pkg.IconSource != null) return;

            Task.Run(() =>
            {
                try
                {
                    string faviconUrl = "https://t2.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=" +
                                        Uri.EscapeDataString(pkg.Homepage) + "&size=128";
                    byte[] bytes = DownloadUrlBytes(faviconUrl);
                    if (bytes != null && bytes.Length > 200)
                    {
                        string localPath = Path.Combine(IconsCacheDir, SanitizeFileName(pkg.Id) + ".png");
                        try { File.WriteAllBytes(localPath, bytes); } catch { }

                        Application app = Application.Current;
                        if (app != null)
                        {
                            app.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var bmp = LoadBitmapFromBytes(bytes);
                                if (bmp != null)
                                {
                                    _memoryCache[pkg.Id] = bmp;
                                    pkg.IconSource = bmp;
                                }
                            }));
                        }
                    }
                }
                catch { }
            });
        }

        private static byte[] DownloadBestIconBytes(string id, string publisher, string homepage)
        {
            List<string> candidateUrls = new List<string>();

            if (!string.IsNullOrWhiteSpace(homepage) && homepage.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                candidateUrls.Add("https://t2.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=" +
                                  Uri.EscapeDataString(homepage) + "&size=128");
            }

            string[] parts = (id ?? "").Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string pub = parts[0].ToLowerInvariant();
                string prod = parts[1].ToLowerInvariant();

                // Try official domain guesses via Google High-Res Favicon V2
                candidateUrls.Add("https://t2.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=https://www." + prod + ".org&size=128");
                candidateUrls.Add("https://t2.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=https://www." + prod + ".com&size=128");
                candidateUrls.Add("https://t2.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=https://www." + pub + ".com&size=128");
                candidateUrls.Add("https://github.com/" + pub + ".png?size=128");
            }

            foreach (string url in candidateUrls)
            {
                byte[] data = DownloadUrlBytes(url);
                if (data != null && data.Length > 350)
                    return data;
            }

            return null;
        }

        private static byte[] DownloadUrlBytes(string url)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                req.Timeout = 5000;
                req.ReadWriteTimeout = 5000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    if (s == null) return null;
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource TryExtractInstalledIcon(string id, string name)
        {
            try
            {
                EnsureRegistryIndex();
                string iconPath = null;

                if (!string.IsNullOrWhiteSpace(id) && _registryDisplayIcons.TryGetValue(id, out iconPath))
                {
                    var src = ExtractWindowsIcon(iconPath);
                    if (src != null) return src;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (_registryDisplayIcons.TryGetValue(name, out iconPath))
                    {
                        var src = ExtractWindowsIcon(iconPath);
                        if (src != null) return src;
                    }

                    foreach (var kv in _registryDisplayIcons)
                    {
                        if (kv.Key.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var src = ExtractWindowsIcon(kv.Value);
                            if (src != null) return src;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static void EnsureRegistryIndex()
        {
            lock (_regLock)
            {
                if (_registryDisplayIcons != null) return;
                _registryDisplayIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                ScanUninstallKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                ScanUninstallKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
                ScanUninstallKey(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            }
        }

        private static void ScanUninstallKey(RegistryKey root, string subKeyPath)
        {
            try
            {
                using (RegistryKey baseKey = root.OpenSubKey(subKeyPath))
                {
                    if (baseKey == null) return;
                    foreach (string subName in baseKey.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey itemKey = baseKey.OpenSubKey(subName))
                            {
                                if (itemKey == null) continue;
                                string displayName = itemKey.GetValue("DisplayName") as string;
                                string displayIcon = itemKey.GetValue("DisplayIcon") as string;
                                if (string.IsNullOrWhiteSpace(displayIcon)) continue;

                                string cleanedPath = CleanIconPath(displayIcon);
                                if (string.IsNullOrWhiteSpace(cleanedPath) || !File.Exists(cleanedPath)) continue;

                                _registryDisplayIcons[subName] = cleanedPath;
                                if (!string.IsNullOrWhiteSpace(displayName))
                                    _registryDisplayIcons[displayName.Trim()] = cleanedPath;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static string CleanIconPath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim();
            if (s.StartsWith("\""))
            {
                int endQuote = s.IndexOf('"', 1);
                if (endQuote > 1)
                    s = s.Substring(1, endQuote - 1);
            }
            else
            {
                int comma = s.LastIndexOf(',');
                if (comma > 0)
                    s = s.Substring(0, comma);
            }
            return s.Trim().Trim('"');
        }

        private static ImageSource ExtractWindowsIcon(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
                using (Icon ico = Icon.ExtractAssociatedIcon(filePath))
                {
                    if (ico == null) return null;
                    BitmapSource bmp = Imaging.CreateBitmapSourceFromHIcon(
                        ico.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage LoadBitmapFromFile(string fullPath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                return LoadBitmapFromBytes(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage LoadBitmapFromBytes(byte[] bytes)
        {
            try
            {
                if (bytes == null || bytes.Length == 0) return null;
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string SanitizeFileName(string id)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string res = id ?? "pkg";
            foreach (char c in invalid)
                res = res.Replace(c, '_');
            return res;
        }
    }
}
