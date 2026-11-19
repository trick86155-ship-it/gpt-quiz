using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

public sealed class QuizRelease {
    public string tag_name { get; set; }
    public bool draft { get; set; }
    public bool prerelease { get; set; }
    public QuizReleaseAsset[] assets { get; set; }
}
public sealed class QuizReleaseAsset {
    public string name { get; set; }
    public string browser_download_url { get; set; }
    public long size { get; set; }
}

public sealed class QuizUpdater {
    public const string Repository = "trick86155-ship-it/gpt-quiz";
    private const string ReleasePrefix = "https://github.com/" + Repository + "/releases/download/";
    public string CurrentVersion { get; private set; }
    public string LatestVersion { get; private set; }
    public string Error { get; private set; }
    public bool Available { get; private set; }
    public bool Checking { get; private set; }
    public bool Downloading { get; private set; }
    public bool Ready { get; private set; }
    public int Progress { get; private set; }
    public string StagePath { get; private set; }
    public string Hash { get; private set; }
    private QuizReleaseAsset executable, checksum;
    public Task Operation { get; private set; }

    public QuizUpdater(string version) {
        Version.Parse(version);
        CurrentVersion = version;
    }

    private static HttpClient Client() {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LunaQuiz-Updater/1.0");
        return client;
    }

    public void AcceptRelease(string json) {
        Available = false;
        var release = new JavaScriptSerializer().Deserialize<QuizRelease>(json);
        Version version;
        if (release == null || release.draft || release.prerelease ||
            String.IsNullOrEmpty(release.tag_name) ||
            !Version.TryParse(release.tag_name.TrimStart('v'), out version)) return;
        LatestVersion = version.ToString();
        executable = checksum = null;
        foreach (var asset in release.assets ?? new QuizReleaseAsset[0]) {
            if (asset.name == "LunaQuiz.exe") executable = asset;
            if (asset.name == "LunaQuiz.exe.sha256") checksum = asset;
        }
        if (executable == null || checksum == null ||
            !ValidAsset(executable) || !ValidAsset(checksum)) return;
        Available = version > Version.Parse(CurrentVersion);
    }

    private static bool ValidAsset(QuizReleaseAsset asset) {
        return asset.size > 0 && asset.size <= 100 * 1024 * 1024 &&
            asset.browser_download_url != null &&
            asset.browser_download_url.StartsWith(ReleasePrefix, StringComparison.Ordinal) &&
            asset.browser_download_url.EndsWith("/" + asset.name, StringComparison.Ordinal);
    }

    public void Check() {
        if (Checking || Downloading) return;
        Error = null;
        Checking = true;
        Operation = Task.Run(async () => {
            try {
                using (var client = Client())
                using (var response = await client.GetAsync("https://api.github.com/repos/" + Repository + "/releases/latest")) {
                    if (response.StatusCode == HttpStatusCode.NotFound) { Available = false; return; }
                    response.EnsureSuccessStatusCode();
                    AcceptRelease(await response.Content.ReadAsStringAsync());
                }
            } catch (Exception error) { Available = false; Error = error.Message; }
            finally { Checking = false; }
        });
    }

    public static string FileHash(string path) {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    public void Download(string target) {
        if (!Available || Downloading || Checking) return;
        Error = null;
        Ready = false;
        Progress = 0;
        Downloading = true;
        StagePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),
            ".LunaQuiz-update-" + Guid.NewGuid().ToString("N") + ".download");
        Operation = Task.Run(async () => {
            try {
                using (var client = Client()) {
                    string text = await client.GetStringAsync(checksum.browser_download_url);
                    Hash = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
                    if (!System.Text.RegularExpressions.Regex.IsMatch(Hash, "^[a-f0-9]{64}$"))
                        throw new InvalidDataException("업데이트 체크섬이 올바르지 않습니다.");
                    using (var response = await client.GetAsync(executable.browser_download_url, HttpCompletionOption.ResponseHeadersRead)) {
                        response.EnsureSuccessStatusCode();
                        using (var input = await response.Content.ReadAsStreamAsync())
                        using (var output = new FileStream(StagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                            var buffer = new byte[65536];
                            long received = 0;
                            int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                                received += count;
                                if (received > executable.size) throw new InvalidDataException("업데이트 파일 크기가 다릅니다.");
                                await output.WriteAsync(buffer, 0, count);
                                Progress = (int)(received * 94 / executable.size);
                            }
                            if (received != executable.size) throw new InvalidDataException("업데이트 다운로드가 완료되지 않았습니다.");
                        }
                    }
                }
                Progress = 96;
                if (FileHash(StagePath) != Hash) throw new InvalidDataException("업데이트 파일 검증에 실패했습니다.");
                var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(StagePath).FileVersion;
                if (!SameVersion(version, LatestVersion))
                    throw new InvalidDataException("업데이트 버전이 릴리스와 다릅니다.");
                Progress = 100;
                Ready = true;
            } catch (Exception error) {
                Error = error.Message;
                try { File.Delete(StagePath); } catch (IOException) {} catch (UnauthorizedAccessException) {}
            } finally { Downloading = false; }
        });
    }

    public static bool SameVersion(string a, string b) {
        var left = Version.Parse(a);
        var right = Version.Parse(b);
        return left.Major == right.Major && left.Minor == right.Minor && left.Build == right.Build &&
            Math.Max(0, left.Revision) == Math.Max(0, right.Revision);
    }
}
