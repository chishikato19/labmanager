using System.Diagnostics;
using System.Security.Cryptography;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class AppUpdateHandler
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    private int _isUpdating;

    public async Task<bool> HandleUpdateAsync(AppUpdatePacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.DownloadUrl))
            return false;

        // Đảm bảo chỉ có một tiến trình cập nhật chạy tại một thời điểm
        if (Interlocked.CompareExchange(ref _isUpdating, 1, 0) != 0)
            return false;

        try
        {
            string tempDir = Path.GetTempPath();
            string tempInstallerPath = Path.Combine(tempDir, "LabManager_Student_Update.exe");

            // 1. Tải tệp cài đặt từ máy giáo viên qua mạng LAN
            using (var response = await HttpClient.GetAsync(packet.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                await using (var remoteStream = await response.Content.ReadAsStreamAsync())
                await using (var localFileStream = new FileStream(tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                {
                    await remoteStream.CopyToAsync(localFileStream);
                }
            }

            // 2. Xác thực tính toàn vẹn (SHA256) nếu có
            if (!string.IsNullOrWhiteSpace(packet.Sha256))
            {
                await using var verifyStream = File.OpenRead(tempInstallerPath);
                byte[] hashBytes = await SHA256.HashDataAsync(verifyStream);
                string computedHash = Convert.ToHexString(hashBytes);

                if (!string.Equals(computedHash, packet.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(tempInstallerPath); } catch { }
                    Interlocked.Exchange(ref _isUpdating, 0);
                    return false;
                }
            }

            // 3. Khởi chạy bộ cài đặt Inno Setup ngầm hoàn toàn
            // /VERYSILENT: Không hiện UI
            // /SUPPRESSMSGBOXES: Tự động nhấn Yes/OK cho mọi cảnh báo
            // /FORCECLOSEAPPLICATIONS: Đóng app nếu đang mở
            var psi = new ProcessStartInfo
            {
                FileName = tempInstallerPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /FORCECLOSEAPPLICATIONS",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process.Start(psi);

            // 4. Cho bộ cài đặt 1 giây để nạp vào bộ nhớ trước khi App học sinh tự thoát
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                Environment.Exit(0);
            });

            return true;
        }
        catch
        {
            Interlocked.Exchange(ref _isUpdating, 0);
            return false;
        }
    }
}
