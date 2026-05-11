using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lux;

internal static class Upgrader
{
    private const string GithubReleasesLatestUrl = "https://api.github.com/repos/LuaLux/lux/releases/latest";
    private const string UserAgent = "lux-cli";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan NotifyTimeout = TimeSpan.FromSeconds(2);

    public static async Task<int> RunUpgradeAsync(string[] args)
    {
        var force = args.Contains("--force") || args.Contains("-f");

        Console.WriteLine("Checking for updates...");
        GitHubRelease? release;
        try
        {
            release = await FetchLatestReleaseAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Failed to query GitHub releases: {ex.Message}");
            return 1;
        }

        if (release == null || string.IsNullOrEmpty(release.TagName))
        {
            await Console.Error.WriteLineAsync("No releases found on GitHub.");
            return 1;
        }

        var current = Program.GetLuxVersion();
        var latest = release.TagName.TrimStart('v');

        WriteCache(latest);

        if (!force && CompareSemver(current, latest) >= 0)
        {
            Console.WriteLine($"lux is already at the latest version ({current}).");
            return 0;
        }

        var assetName = PickAssetForPlatform();
        if (assetName == null)
        {
            await Console.Error.WriteLineAsync(
                $"No prebuilt release asset available for {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}). Build from source instead.");
            return 1;
        }

        var asset = release.Assets?.FirstOrDefault(a => a.Name == assetName);
        if (asset == null)
        {
            await Console.Error.WriteLineAsync($"Release '{release.TagName}' has no asset named '{assetName}'.");
            return 1;
        }

        Console.WriteLine($"Current: {current}");
        Console.WriteLine($"Latest:  {latest}");
        Console.WriteLine($"Downloading {assetName}...");

        var tempArchive = Path.Combine(Path.GetTempPath(), $"lux-upgrade-{Guid.NewGuid():N}{(assetName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ? ".tar.gz" : Path.GetExtension(assetName))}");
        var tempDir = Path.Combine(Path.GetTempPath(), $"lux-upgrade-{Guid.NewGuid():N}");
        try
        {
            await DownloadAsync(asset.BrowserDownloadUrl, tempArchive);

            Console.WriteLine("Extracting...");
            Directory.CreateDirectory(tempDir);
            await ExtractAsync(tempArchive, tempDir);

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var binaryName = isWindows ? "lux.exe" : "lux";
            var newBinary = FindBinary(tempDir, binaryName);
            if (newBinary == null)
            {
                await Console.Error.WriteLineAsync($"Extracted archive does not contain '{binaryName}'.");
                return 1;
            }

            var currentPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentPath))
            {
                await Console.Error.WriteLineAsync("Could not determine the current executable path.");
                return 1;
            }

            Console.WriteLine($"Installing to {currentPath}...");
            try
            {
                ReplaceBinary(currentPath, newBinary);
            }
            catch (UnauthorizedAccessException)
            {
                var hint = isWindows
                    ? "Try again from an Administrator shell."
                    : "Try: sudo lux upgrade  (or reinstall lux to a user-writable path).";
                await Console.Error.WriteLineAsync($"Permission denied writing to {currentPath}. {hint}");
                return 1;
            }

            Console.WriteLine($"Updated lux: {current} -> {latest}");
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Upgrade failed: {ex.Message}");
            return 1;
        }
        finally
        {
            try { if (File.Exists(tempArchive)) File.Delete(tempArchive); } catch { }
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    public static async Task<int> RunCheckAsync()
    {
        var current = Program.GetLuxVersion();
        Console.WriteLine($"Current: {current}");

        GitHubRelease? release;
        try
        {
            release = await FetchLatestReleaseAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Failed to query GitHub releases: {ex.Message}");
            return 1;
        }

        if (release == null || string.IsNullOrEmpty(release.TagName))
        {
            await Console.Error.WriteLineAsync("No releases found.");
            return 1;
        }

        var latest = release.TagName.TrimStart('v');
        WriteCache(latest);

        Console.WriteLine($"Latest:  {latest}");
        if (CompareSemver(current, latest) >= 0)
        {
            Console.WriteLine("lux is up to date.");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("A newer version is available. Run `lux upgrade` to install it.");
        return 0;
    }

    public static async Task MaybeShowUpdateNoticeAsync()
    {
        try
        {
            var cache = ReadCache();
            var current = Program.GetLuxVersion();

            if (cache != null && DateTimeOffset.UtcNow - cache.CheckedAt < CacheTtl)
            {
                EmitNotice(current, cache.LatestVersion);
                return;
            }

            using var cts = new CancellationTokenSource(NotifyTimeout);
            try
            {
                var release = await FetchLatestReleaseAsync(cts.Token);
                if (release == null || string.IsNullOrEmpty(release.TagName)) return;
                var latest = release.TagName.TrimStart('v');
                WriteCache(latest);
                EmitNotice(current, latest);
            }
            catch
            {
                // Offline / rate-limited — stay silent.
            }
        }
        catch
        {
            // Never let the notice break the calling command.
        }
    }

    public static void CleanupStaleBackup()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var current = Environment.ProcessPath;
        if (string.IsNullOrEmpty(current)) return;
        var old = current + ".old";
        if (!File.Exists(old)) return;
        try { File.Delete(old); } catch { /* still locked from a sibling invocation; retry next time */ }
    }

    private static void EmitNotice(string current, string latest)
    {
        if (CompareSemver(current, latest) >= 0) return;
        Console.WriteLine();
        Console.WriteLine($"A new version of lux is available: {current} -> {latest}");
        Console.WriteLine("Run `lux upgrade` to update.");
    }

    private static async Task<GitHubRelease?> FetchLatestReleaseAsync(CancellationToken ct)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var resp = await http.GetAsync(GithubReleasesLatestUrl, ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: ct);
    }

    private static async Task DownloadAsync(string url, string destPath)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        await using var fs = File.Create(destPath);
        await resp.Content.CopyToAsync(fs);
    }

    private static async Task ExtractAsync(string archivePath, string destDir)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destDir, overwriteFiles: true);
            return;
        }
        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            await using var fs = File.OpenRead(archivePath);
            await using var gz = new GZipStream(fs, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(gz, destDir, overwriteFiles: true);
            return;
        }
        throw new NotSupportedException($"Unsupported archive format: {archivePath}");
    }

    private static string? FindBinary(string root, string name)
    {
        var direct = Path.Combine(root, name);
        if (File.Exists(direct)) return direct;
        foreach (var f in Directory.EnumerateFiles(root, name, SearchOption.AllDirectories))
            return f;
        return null;
    }

    private static string? PickAssetForPlatform()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };
        if (arch == null) return null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"lux-linux-{arch}.tar.gz";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return arch == "arm64" ? "lux-osx-arm64.tar.gz" : null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"lux-win-{arch}.zip";
        return null;
    }

    private static void ReplaceBinary(string currentPath, string newBinary)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows locks running .exe files: overwrite/delete are forbidden,
            // but rename of the running .exe is allowed. Move current out of the
            // way, drop the new binary in. The .old file is removed on the next
            // lux invocation (see CleanupStaleBackup).
            var oldPath = currentPath + ".old";
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { }
            }
            File.Move(currentPath, oldPath);
            try
            {
                File.Move(newBinary, currentPath);
            }
            catch
            {
                try { File.Move(oldPath, currentPath); } catch { }
                throw;
            }
        }
        else
        {
            // Unix: replacing a running binary is safe — the kernel keeps the
            // old inode mapped for the running process while the path now
            // points at the new file.
            File.Move(newBinary, currentPath, overwrite: true);
            var perms = File.GetUnixFileMode(currentPath);
            File.SetUnixFileMode(currentPath, perms
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherExecute);
        }
    }

    private static int CompareSemver(string a, string b)
    {
        var ad = a.IndexOf('-');
        var bd = b.IndexOf('-');
        var aCore = ad >= 0 ? a[..ad] : a;
        var bCore = bd >= 0 ? b[..bd] : b;

        if (!System.Version.TryParse(aCore, out var av) || !System.Version.TryParse(bCore, out var bv))
            return string.CompareOrdinal(a, b);

        var cmp = av.CompareTo(bv);
        if (cmp != 0) return cmp;

        var aHasPre = ad >= 0;
        var bHasPre = bd >= 0;
        if (aHasPre && !bHasPre) return -1;
        if (!aHasPre && bHasPre) return 1;
        if (!aHasPre && !bHasPre) return 0;
        return string.CompareOrdinal(a[(ad + 1)..], b[(bd + 1)..]);
    }

    private static string GetCachePath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(baseDir)) baseDir = Path.GetTempPath();
        return Path.Combine(baseDir, "lux", "update-check.json");
    }

    private static UpdateCheckCache? ReadCache()
    {
        try
        {
            var path = GetCachePath();
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            return JsonSerializer.Deserialize<UpdateCheckCache>(fs);
        }
        catch { return null; }
    }

    private static void WriteCache(string latestVersion)
    {
        try
        {
            var path = GetCachePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new UpdateCheckCache(DateTimeOffset.UtcNow, latestVersion));
            File.WriteAllText(path, json);
        }
        catch { /* best-effort */ }
    }

    private sealed record UpdateCheckCache(
        [property: JsonPropertyName("checkedAt")] DateTimeOffset CheckedAt,
        [property: JsonPropertyName("latestVersion")] string LatestVersion);

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("assets")] List<GitHubAsset>? Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
