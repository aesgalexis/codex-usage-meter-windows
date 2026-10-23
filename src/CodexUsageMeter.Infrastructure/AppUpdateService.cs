using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexUsageMeter.Infrastructure;

public sealed record AppRelease(Version Version, Uri InstallerUrl, string Sha256);

public sealed class AppUpdateService(HttpClient client)
{
    public const string InstallerName = "CodexUsageMeter-Setup-win-x64.exe";
    private const string DownloadPrefix = "https://github.com/aesgalexis/codex-usage-meter-windows/releases/download/";
    private const string LatestReleaseUrl = "https://api.github.com/repos/aesgalexis/codex-usage-meter-windows/releases/latest";

    public async Task<AppRelease?> CheckAsync(Version installedVersion, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CodexUsageMeter", installedVersion.ToString()));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<ReleaseResponse>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Missing release metadata.");
        if (release.Draft || release.Prerelease) return null;
        var tag = release.Tag ?? "";
        if (!Version.TryParse(tag.StartsWith('v') ? tag[1..] : tag, out var version) || version.Build < 0 || version.Revision >= 0)
            throw new InvalidDataException("Invalid stable release version.");
        var installed = new Version(installedVersion.Major, installedVersion.Minor, Math.Max(0, installedVersion.Build));
        if (version <= installed) return null;
        var asset = release.Assets?.SingleOrDefault(asset => asset.Name == InstallerName)
            ?? throw new InvalidDataException("The release has no Windows installer.");
        var expectedUrl = DownloadPrefix + tag + "/" + InstallerName;
        if (asset.Url != expectedUrl || !Uri.TryCreate(asset.Url, UriKind.Absolute, out var url))
            throw new InvalidDataException("Invalid installer URL.");
        var digest = asset.Digest;
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
            digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
            throw new InvalidDataException("Missing SHA-256 checksum.");
        return new AppRelease(version, url, digest[7..]);
    }

    public async Task DownloadInstallerAsync(AppRelease release, string destination, CancellationToken cancellationToken = default)
    {
        if (release.InstallerUrl.AbsoluteUri != DownloadPrefix + "v" + release.Version.ToString(3) + "/" + InstallerName &&
            release.InstallerUrl.AbsoluteUri != DownloadPrefix + release.Version.ToString(3) + "/" + InstallerName)
            throw new InvalidDataException("Invalid installer URL.");
        // CreateNew prevents overwriting an existing file; only a fully verified download may be launched.
        await using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true);
        try
        {
            using var response = await client.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(file, cancellationToken);
            file.Position = 0;
            var hash = await SHA256.HashDataAsync(file, cancellationToken);
            if (!Convert.ToHexString(hash).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Installer checksum mismatch.");
        }
        catch
        {
            await file.DisposeAsync();
            File.Delete(destination);
            throw;
        }
    }

    private sealed class ReleaseResponse
    {
        [JsonPropertyName("tag_name")] public string? Tag { get; init; }
        [JsonPropertyName("draft")] public bool Draft { get; init; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
        [JsonPropertyName("assets")] public AssetResponse[]? Assets { get; init; }
    }

    private sealed class AssetResponse
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("browser_download_url")] public string? Url { get; init; }
        [JsonPropertyName("digest")] public string? Digest { get; init; }
    }
}
