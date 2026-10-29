using System.Net;
using System.Net.Sockets;
using LabManager.Core.Models;
using LabManager.Protocol;

namespace LabManager.Network.Tcp;

public class TcpCommandListener : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _isDisposed;

    public event Func<PacketHeader, byte[], Task>? OnCommandReceived;

    public bool IsListening => _cts is { IsCancellationRequested: false };

    public void Start(int listenPort = NetworkConfig.DefaultCommandPort)
    {
        if (IsListening) return;

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, listenPort);
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
        using (client)
        using (var stream = client.GetStream())
        {
            byte[] headerBuffer = new byte[PacketHeader.HeaderSize];

            while (!ct.IsCancellationRequested)
            {
                int bytesRead = await ReadExactAsync(stream, headerBuffer, PacketHeader.HeaderSize, ct);
                if (bytesRead < PacketHeader.HeaderSize)
                    break;

                if (!PacketHeader.TryParse(headerBuffer, out var header))
                    break;

                byte[] payloadBuffer = new byte[header.PayloadLength];
                if (header.PayloadLength > 0)
                {
                    int payloadRead = await ReadExactAsync(stream, payloadBuffer, header.PayloadLength, ct);
                    if (payloadRead < header.PayloadLength)
                        break;
                }

                if (OnCommandReceived != null)
                {
                    await OnCommandReceived.Invoke(header, payloadBuffer);
                }
            }
        }
    }

    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, count - total), ct);
            if (read == 0) return total;
            total += read;
        }
        return total;
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
