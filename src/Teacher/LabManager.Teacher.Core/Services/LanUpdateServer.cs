using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using LabManager.Core.Models;

namespace LabManager.Teacher.Core.Services;

public class LanUpdateServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private string? _filePath;
    private bool _isDisposed;

    public bool IsRunning => _listener != null && _cts is { IsCancellationRequested: false };
    public int Port { get; private set; }

    public event Action<string>? OnClientDownloadStarted;
    public event Action<string>? OnClientDownloadCompleted;

    public void Start(string filePath, int port = NetworkConfig.DefaultUpdateHttpPort)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy tệp cài đặt để phát hành.", filePath);

        Stop();

        _filePath = filePath;
        Port = port;
        _cts = new CancellationTokenSource();

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Start();

        _ = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Bỏ qua lỗi kết nối lẻ
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

        using (client)
        using (var networkStream = client.GetStream())
        {
            try
            {
                // Đọc HTTP GET request tối đa 4KB
                byte[] requestBuffer = new byte[4096];
                int read = await networkStream.ReadAsync(requestBuffer.AsMemory(0, requestBuffer.Length), ct);
                if (read <= 0 || string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                    return;

                string requestText = Encoding.ASCII.GetString(requestBuffer, 0, read);
                if (!requestText.StartsWith("GET", StringComparison.OrdinalIgnoreCase))
                    return;

                OnClientDownloadStarted?.Invoke(clientIp);

                var fileInfo = new FileInfo(_filePath);
                long fileLength = fileInfo.Length;

                string header = $"HTTP/1.1 200 OK\r\n" +
                                $"Content-Type: application/octet-stream\r\n" +
                                $"Content-Disposition: attachment; filename=\"{Path.GetFileName(_filePath)}\"\r\n" +
                                $"Content-Length: {fileLength}\r\n" +
                                $"Connection: close\r\n\r\n";

                byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                await networkStream.WriteAsync(headerBytes.AsMemory(0, headerBytes.Length), ct);

                // Truyền file nhị phân bằng FileStream theo khối 64KB
                await using (var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
                {
                    await fileStream.CopyToAsync(networkStream, 65536, ct);
                }

                await networkStream.FlushAsync(ct);
                OnClientDownloadCompleted?.Invoke(clientIp);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                // Đã hủy server
            }
            catch
            {
                // Kết nối ngắt quãng từ phía client
            }
        }
    }

    public static async Task<string> ComputeFileSha256Async(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _cts?.Dispose();
    }
}
