using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabManager.Teacher.Core.Services;

public class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; set; } = new();

    [JsonIgnore]
    public string VersionClean => TagName.TrimStart('v', 'V');
}

public class GitHubReleaseService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    static GitHubReleaseService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Clear();
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LabManager-Teacher", "1.0"));
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public static async Task<GitHubRelease?> GetLatestReleaseAsync(string owner = "chishikato19", string repo = "labmanager", CancellationToken ct = default)
    {
        try
        {
            string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await HttpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GitHubReleaseService] Lỗi kiểm tra release: {ex.Message}");
            return null;
        }
    }

    public static GitHubReleaseAsset? FindTeacherAsset(GitHubRelease release)
    {
        return release.Assets.FirstOrDefault(a =>
            a.Name.Contains("Teacher", StringComparison.OrdinalIgnoreCase) &&
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    public static GitHubReleaseAsset? FindStudentAsset(GitHubRelease release)
    {
        return release.Assets.FirstOrDefault(a =>
            a.Name.Contains("Student", StringComparison.OrdinalIgnoreCase) &&
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsNewerVersion(string latestTag, string currentVersion)
    {
        string v1Str = latestTag.TrimStart('v', 'V').Trim();
        string v2Str = currentVersion.TrimStart('v', 'V').Trim();

        if (Version.TryParse(v1Str, out var v1) && Version.TryParse(v2Str, out var v2))
        {
            return v1 > v2;
        }

        return !string.Equals(v1Str, v2Str, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<string> DownloadAssetAsync(
        GitHubReleaseAsset asset,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        string directory = Path.GetDirectoryName(destinationPath) ?? "";
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var response = await HttpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? asset.Size;
        long totalBytesRead = 0;

        await using var remoteStream = await response.Content.ReadAsStreamAsync(ct);
        await using var localFileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);

        byte[] buffer = new byte[65536];
        int bytesRead;

        while ((bytesRead = await remoteStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await localFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalBytesRead += bytesRead;

            if (totalBytes > 0 && progress != null)
            {
                double percentage = (double)totalBytesRead / totalBytes * 100.0;
                progress.Report(percentage);
            }
        }

        progress?.Report(100.0);
        return destinationPath;
    }
}
