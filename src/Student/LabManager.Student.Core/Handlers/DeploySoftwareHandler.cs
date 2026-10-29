using System.Diagnostics;
using System.Security.Cryptography;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class DeploySoftwareHandler
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(15)
    };

    private int _isDeploying;

    public event Func<DeploySoftwareStatusPacket, Task>? OnStatusChanged;

    public async Task HandleDeployAsync(DeploySoftwarePacket packet, string machineId)
    {
        if (string.IsNullOrWhiteSpace(packet.DownloadUrl))
        {
            await ReportStatusAsync(machineId, "Failed", 0, -1, "Đường dẫn tải gói cài đặt không hợp lệ.");
            return;
        }

        if (Interlocked.CompareExchange(ref _isDeploying, 1, 0) != 0)
        {
            await ReportStatusAsync(machineId, "Failed", 0, -1, "Một tiến trình cài đặt phần mềm khác đang chạy.");
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "LabManager_Deploy");
        string localInstallerPath = Path.Combine(tempDir, string.IsNullOrWhiteSpace(packet.FileName) ? "setup.tmp" : packet.FileName);

        try
        {
            Directory.CreateDirectory(tempDir);

            // 1. Báo cáo bắt đầu tải về
            await ReportStatusAsync(machineId, "Downloading", 10, 0, "Đang tải tệp cài đặt...");

            // 2. Tải tệp cài đặt từ máy giáo viên qua HTTP LAN
            using (var response = await HttpClient.GetAsync(packet.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                await using (var remoteStream = await response.Content.ReadAsStreamAsync())
                await using (var localFileStream = new FileStream(localInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                {
                    await remoteStream.CopyToAsync(localFileStream);
                }
            }

            // 3. Xác thực mã băm SHA256 nếu có
            if (!string.IsNullOrWhiteSpace(packet.Sha256))
            {
                await using var verifyStream = File.OpenRead(localInstallerPath);
                byte[] hashBytes = await SHA256.HashDataAsync(verifyStream);
                string computedHash = Convert.ToHexString(hashBytes);

                if (!string.Equals(computedHash, packet.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(localInstallerPath);
                    await ReportStatusAsync(machineId, "Failed", 0, -2, "Mã băm SHA256 không khớp, tệp bị hỏng.");
                    return;
                }
            }

            // 4. Báo cáo chuyển sang giai đoạn cài đặt ngầm
            await ReportStatusAsync(machineId, "Installing", 50, 0, "Đang thực thi cài đặt ngầm...");

            // 5. Chuẩn bị ProcessStartInfo
            bool isMsi = packet.IsMsi || localInstallerPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
            var psi = new ProcessStartInfo();

            if (isMsi)
            {
                psi.FileName = "msiexec.exe";
                string msiArgs = string.IsNullOrWhiteSpace(packet.Arguments) ? "/quiet /norestart" : packet.Arguments;
                psi.Arguments = $"/i \"{localInstallerPath}\" {msiArgs}";
            }
            else
            {
                psi.FileName = localInstallerPath;
                psi.Arguments = packet.Arguments ?? string.Empty;
            }

            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            using var process = Process.Start(psi);
            if (process == null)
            {
                await ReportStatusAsync(machineId, "Failed", 0, -3, "Không thể khởi chạy tiến trình cài đặt.");
                return;
            }

            await process.WaitForExitAsync();

            int exitCode = process.ExitCode;

            // 6. Kiểm tra mã thoát
            // 0: Thành công
            // 3010: Cài đặt thành công, yêu cầu khởi động lại máy để hoàn tất cấu hình
            if (exitCode == 0 || exitCode == 3010)
            {
                string msg = exitCode == 3010 ? "Cài đặt thành công (Yêu cầu khởi động lại để hoàn tất)." : "Cài đặt thành công.";
                await ReportStatusAsync(machineId, "Completed", 100, exitCode, msg);
            }
            else
            {
                await ReportStatusAsync(machineId, "Failed", 0, exitCode, $"Cài đặt thất bại (Mã lỗi: {exitCode}).");
            }
        }
        catch (Exception ex)
        {
            await ReportStatusAsync(machineId, "Failed", 0, -99, $"Lỗi cài đặt: {ex.Message}");
        }
        finally
        {
            TryDeleteFile(localInstallerPath);
            Interlocked.Exchange(ref _isDeploying, 0);
        }
    }

    private async Task ReportStatusAsync(string machineId, string stage, int progress, int exitCode, string message)
    {
        if (OnStatusChanged != null)
        {
            var statusPacket = new DeploySoftwareStatusPacket
            {
                MachineId = machineId,
                MachineName = Environment.MachineName,
                Stage = stage,
                ProgressPercentage = progress,
                ExitCode = exitCode,
                Message = message
            };

            await OnStatusChanged.Invoke(statusPacket);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Bỏ qua lỗi xóa tệp tạm nếu đang bị giữ
        }
    }
}
