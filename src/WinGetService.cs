using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinGetStore
{
    public class WinGetService
    {
        public async Task<string> GetWinGetVersionAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    string output = RunWinGetRaw("--version");
                    foreach (string line in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase) || char.IsDigit(trimmed[0]))
                            return trimmed;
                    }
                    return "Active";
                }
                catch
                {
                    return "Not found";
                }
            });
        }

        public async Task<List<PackageItem>> SearchPackagesAsync(string query)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(query))
                    return new List<PackageItem>();

                string safeQuery = query.Replace("\"", "");
                string output = RunWinGetRaw("search \"" + safeQuery + "\" --source winget --accept-source-agreements -n 60");
                return ParseTableOutput(output, false);
            });
        }

        public async Task<List<PackageItem>> GetInstalledPackagesAsync()
        {
            return await Task.Run(() =>
            {
                string output = RunWinGetRaw("list --accept-source-agreements");
                var items = ParseTableOutput(output, false);
                foreach (var item in items)
                {
                    item.IsInstalled = true;
                    item.Category = string.IsNullOrWhiteSpace(item.Source) ? "Locally Installed" : "Source: " + item.Source;
                }
                return items;
            });
        }

        public async Task<List<PackageItem>> GetUpgradablePackagesAsync()
        {
            return await Task.Run(() =>
            {
                string output = RunWinGetRaw("upgrade --accept-source-agreements");
                var items = ParseTableOutput(output, true);
                foreach (var item in items)
                {
                    item.IsInstalled = true;
                    item.HasUpdate = !string.IsNullOrWhiteSpace(item.AvailableVersion);
                    item.Category = "Update Available";
                }
                return items;
            });
        }

        public async Task EnrichPackageDetailsAsync(PackageItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;

            await Task.Run(() =>
            {
                string safeId = item.Id.Replace("\"", "");
                string output = RunWinGetRaw("show --exact --id \"" + safeId + "\" --accept-source-agreements");
                if (string.IsNullOrWhiteSpace(output)) return;

                string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bool inTags = false;
                bool inReleaseNotes = false;
                List<string> tags = new List<string>();
                StringBuilder releaseNotesBuilder = new StringBuilder();

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Replace('\u00A0', ' ');
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    if (inTags)
                    {
                        if (line.StartsWith("  ") && !trimmed.Contains(":"))
                        {
                            tags.Add(trimmed);
                            continue;
                        }
                        inTags = false;
                    }

                    if (inReleaseNotes)
                    {
                        if (line.StartsWith("  ") && !trimmed.StartsWith("URL", StringComparison.OrdinalIgnoreCase))
                        {
                            releaseNotesBuilder.AppendLine(trimmed);
                            continue;
                        }
                        inReleaseNotes = false;
                    }

                    if (trimmed.StartsWith("Marcas:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Tags:", StringComparison.OrdinalIgnoreCase))
                    {
                        inTags = true;
                        continue;
                    }

                    if (trimmed.StartsWith("Notas de Versão:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Release Notes:", StringComparison.OrdinalIgnoreCase))
                    {
                        string after = ExtractAfterColon(trimmed);
                        if (!string.IsNullOrWhiteSpace(after))
                            releaseNotesBuilder.AppendLine(after);
                        inReleaseNotes = true;
                        continue;
                    }

                    if (trimmed.StartsWith("Notas de Versão -", StringComparison.OrdinalIgnoreCase))
                    {
                        string after = trimmed.Substring("Notas de Versão -".Length).Trim();
                        if (!string.IsNullOrWhiteSpace(after))
                            releaseNotesBuilder.AppendLine(after);
                        inReleaseNotes = true;
                        continue;
                    }

                    int colonIdx = trimmed.IndexOf(':');
                    if (colonIdx <= 0) continue;

                    string key = trimmed.Substring(0, colonIdx).Trim().ToLowerInvariant();
                    string val = trimmed.Substring(colonIdx + 1).Trim();
                    if (string.IsNullOrWhiteSpace(val)) continue;

                    if (key == "fornecedor" || key == "publisher" || key == "autor" || key == "author")
                    {
                        if (string.IsNullOrWhiteSpace(item.Publisher) || key == "fornecedor" || key == "publisher")
                            item.Publisher = val;
                    }
                    else if (key == "versão" || key == "version")
                    {
                        if (string.IsNullOrWhiteSpace(item.Version))
                            item.Version = val;
                    }
                    else if (key == "descrição" || key == "description")
                    {
                        item.Description = val;
                    }
                    else if (key == "página inicial" || key == "homepage" || key == "url do fornecedor" || key == "publisher url")
                    {
                        if (string.IsNullOrWhiteSpace(item.Homepage) || key == "página inicial" || key == "homepage")
                            item.Homepage = val;
                    }
                    else if (key == "licença" || key == "license")
                    {
                        item.License = val;
                    }
                    else if (key == "tipo de instalador" || key == "installer type")
                    {
                        item.InstallerType = val;
                    }
                    else if (key == "url de notas de versão" || key == "release notes url")
                    {
                        item.ReleaseNotesUrl = val;
                    }
                }

                if (tags.Count > 0)
                    item.Tags = string.Join(", ", tags);
                if (releaseNotesBuilder.Length > 0)
                    item.ReleaseNotes = releaseNotesBuilder.ToString().Trim();
            });
        }

        public async Task<string> GetSourcesSummaryAsync()
        {
            return await Task.Run(() =>
            {
                string output = RunWinGetRaw("source list");
                StringBuilder sb = new StringBuilder();
                foreach (string line in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length <= 2) continue;
                    if (trimmed.StartsWith("-") && trimmed.Length < 5) continue;
                    sb.AppendLine(trimmed);
                }
                return sb.ToString().Trim();
            });
        }

        public async Task<bool> RunPackageActionAsync(
            string action,
            string packageId,
            bool silentMode,
            string installScope,
            Action<string> onProgressLine)
        {
            return await Task.Run(() =>
            {
                string safeId = packageId.Replace("\"", "");
                StringBuilder args = new StringBuilder();

                if (action == "install")
                {
                    args.Append("install --exact --id \"").Append(safeId).Append("\" --accept-package-agreements --accept-source-agreements");
                    if (silentMode) args.Append(" --silent");
                    else args.Append(" --interactive");
                    if (!string.IsNullOrWhiteSpace(installScope) && installScope != "auto")
                        args.Append(" --scope ").Append(installScope);
                }
                else if (action == "upgrade")
                {
                    args.Append("upgrade --exact --id \"").Append(safeId).Append("\" --accept-package-agreements --accept-source-agreements");
                    if (silentMode) args.Append(" --silent");
                }
                else if (action == "uninstall")
                {
                    args.Append("uninstall --exact --id \"").Append(safeId).Append("\" --accept-source-agreements");
                    if (silentMode) args.Append(" --silent");
                }
                else
                {
                    return false;
                }

                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "winget.exe",
                        Arguments = args.ToString(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (Process proc = new Process { StartInfo = psi })
                    {
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrWhiteSpace(e.Data))
                            {
                                string cleaned = CleanProgressLine(e.Data);
                                if (!string.IsNullOrWhiteSpace(cleaned) && onProgressLine != null)
                                    onProgressLine(cleaned);
                            }
                        };
                        proc.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrWhiteSpace(e.Data))
                            {
                                string cleaned = CleanProgressLine(e.Data);
                                if (!string.IsNullOrWhiteSpace(cleaned) && onProgressLine != null)
                                    onProgressLine(cleaned);
                            }
                        };

                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        proc.WaitForExit();
                        return proc.ExitCode == 0;
                    }
                }
                catch (Exception ex)
                {
                    if (onProgressLine != null)
                        onProgressLine("Error running winget: " + ex.Message);
                    return false;
                }
            });
        }

        private static string ExtractAfterColon(string text)
        {
            int idx = text.IndexOf(':');
            return idx >= 0 && idx + 1 < text.Length ? text.Substring(idx + 1).Trim() : "";
        }

        private static string CleanProgressLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return "";
            string trimmed = line.Replace('\u00A0', ' ').Trim();
            if (trimmed.Length <= 3 && (trimmed == "-" || trimmed == "\\" || trimmed == "|" || trimmed == "/"))
                return "";
            return trimmed;
        }

        private static string RunWinGetRaw(string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "winget.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process proc = Process.Start(psi))
            {
                string stdout = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(25000);
                return stdout ?? "";
            }
        }

        public static List<PackageItem> ParseTableOutput(string output, bool requireAvailableVersion)
        {
            List<PackageItem> results = new List<PackageItem>();
            if (string.IsNullOrWhiteSpace(output)) return results;

            string[] rawLines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int dashLineIdx = -1;
            for (int i = 1; i < rawLines.Length; i++)
            {
                string t = rawLines[i].Trim();
                if (t.StartsWith("---------", StringComparison.Ordinal))
                {
                    dashLineIdx = i;
                    break;
                }
            }

            if (dashLineIdx <= 0) return results;

            string headerLine = rawLines[dashLineIdx - 1];
            // Find column positions in headerLine
            MatchCollection headerMatches = Regex.Matches(headerLine, @"\S+");
            if (headerMatches.Count < 2) return results;

            List<int> colStarts = new List<int>();
            List<string> colNames = new List<string>();
            foreach (Match m in headerMatches)
            {
                colStarts.Add(m.Index);
                colNames.Add(m.Value.ToLowerInvariant());
            }

            int idColIdx = -1;
            int versionColIdx = -1;
            int availableColIdx = -1;
            int sourceColIdx = -1;

            for (int c = 0; c < colNames.Count; c++)
            {
                string cn = colNames[c];
                if (cn == "id") idColIdx = c;
                else if (cn.StartsWith("vers") || cn == "version") versionColIdx = c;
                else if (cn.StartsWith("dispon") || cn == "available") availableColIdx = c;
                else if (cn.StartsWith("orig") || cn == "source") sourceColIdx = c;
            }

            if (idColIdx <= 0) idColIdx = 1;
            if (versionColIdx <= 0 && colStarts.Count > 2) versionColIdx = 2;

            int idStart = colStarts[idColIdx];
            int idEnd = (idColIdx + 1 < colStarts.Count) ? colStarts[idColIdx + 1] : headerLine.Length;

            for (int i = dashLineIdx + 1; i < rawLines.Length; i++)
            {
                string line = rawLines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                string trimmed = line.Trim();
                if (trimmed.Length < 4) continue;
                if (trimmed.EndsWith("atualizações disponíveis.", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.EndsWith("upgrades available.", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("O seguinte", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("The following", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (line.Length <= idStart) continue;

                string name = SafeSlice(line, 0, idStart).Trim();
                string id = SafeSlice(line, idStart, idEnd).Trim();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) continue;
                if (id.Contains(" ") && !id.Contains(".")) continue;

                string version = "";
                string available = "";
                string source = "winget";

                if (versionColIdx >= 0 && versionColIdx < colStarts.Count)
                {
                    int vStart = colStarts[versionColIdx];
                    int vEnd = (versionColIdx + 1 < colStarts.Count) ? colStarts[versionColIdx + 1] : line.Length;
                    version = SafeSlice(line, vStart, vEnd).Trim();
                }

                if (availableColIdx >= 0 && availableColIdx < colStarts.Count)
                {
                    int aStart = colStarts[availableColIdx];
                    int aEnd = (availableColIdx + 1 < colStarts.Count) ? colStarts[availableColIdx + 1] : line.Length;
                    available = SafeSlice(line, aStart, aEnd).Trim();
                }

                if (sourceColIdx >= 0 && sourceColIdx < colStarts.Count)
                {
                    int sStart = colStarts[sourceColIdx];
                    int sEnd = (sourceColIdx + 1 < colStarts.Count) ? colStarts[sourceColIdx + 1] : line.Length;
                    source = SafeSlice(line, sStart, sEnd).Trim();
                }

                // Fallback token extraction if long package name shifted columns
                if (string.IsNullOrWhiteSpace(id) || id.IndexOf(' ') >= 0)
                {
                    string[] parts = Regex.Split(trimmed, @"\s{2,}");
                    if (parts.Length >= 3)
                    {
                        name = parts[0].Trim();
                        id = parts[1].Trim();
                        version = parts[2].Trim();
                        if (parts.Length >= 4 && availableColIdx >= 0) available = parts[3].Trim();
                        if (parts.Length >= 5 && sourceColIdx >= 0) source = parts[4].Trim();
                    }
                }

                if (requireAvailableVersion && string.IsNullOrWhiteSpace(available))
                    continue;

                string publisherGuess = id.Contains(".") ? id.Split('.')[0] : "WinGet Community";
                results.Add(new PackageItem
                {
                    Name = name,
                    Id = id,
                    Publisher = publisherGuess,
                    Version = version,
                    AvailableVersion = available,
                    Source = string.IsNullOrWhiteSpace(source) ? "winget" : source,
                    AccentHex = CuratedCatalog.PickAccentForId(id),
                    Description = "Package officially distributed via the " + (string.IsNullOrWhiteSpace(source) ? "winget" : source) + " repository (" + id + "). Click to inspect full package manifest metadata directly from WinGet."
                });
            }

            return results;
        }

        private static string SafeSlice(string str, int start, int end)
        {
            if (string.IsNullOrEmpty(str) || start >= str.Length) return "";
            int len = Math.Min(end, str.Length) - start;
            if (len <= 0) return "";
            return str.Substring(start, len);
        }
    }
}
