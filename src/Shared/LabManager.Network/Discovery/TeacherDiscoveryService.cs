using System.Net;
using System.Net.Sockets;
using LabManager.Core.Enums;
using LabManager.Core.Models;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;

namespace LabManager.Network.Discovery;

public class TeacherDiscoveryService : IDisposable
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private int _localListeningPort;
    private bool _isDisposed;

    public event Action<DiscoveryResponsePacket, string>? OnStudentDiscovered;

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public void Start(string teacherName = "Teacher", string roomName = "Phòng máy", int broadcastIntervalMs = 3000)
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0))
        {
            EnableBroadcast = true
        };
        _localListeningPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint!).Port;

        // Bắt đầu luồng lắng nghe phản hồi từ học sinh
        _ = ListenForResponsesAsync(_cts.Token);

        // Bắt đầu luồng phát beacon định kỳ
        _ = BroadcastBeaconLoopAsync(teacherName, roomName, broadcastIntervalMs, _cts.Token);
    }

    private async Task BroadcastBeaconLoopAsync(string teacherName, string roomName, int intervalMs, CancellationToken ct)
    {
        var targetEndpoint = new IPEndPoint(IPAddress.Broadcast, NetworkConfig.DefaultDiscoveryPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var beacon = new DiscoveryBeaconPacket
                {
                    TeacherName = teacherName,
                    RoomName = roomName,
                    TeacherPort = _localListeningPort,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                byte[] packetBytes = PacketSerializer.Serialize(CommandType.DiscoveryBeacon, beacon);
                if (_udpClient != null)
                {
                    await _udpClient.SendAsync(packetBytes, packetBytes.Length, targetEndpoint);
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Tiếp tục vòng lặp nếu lỗi mạng tạm thời
            }

            try
            {
                await Task.Delay(intervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ListenForResponsesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(ct);
                if (result.Buffer.Length < PacketHeader.HeaderSize)
                    continue;

                if (!PacketSerializer.TryDeserializeHeader(result.Buffer, out var header))
                    continue;

                if (header.Type == CommandType.DiscoveryResponse)
                {
                    var response = PacketSerializer.DeserializePayload<DiscoveryResponsePacket>(
                        result.Buffer, PacketHeader.HeaderSize, header.PayloadLength);
                    if (response != null)
                    {
                        string studentIp = result.RemoteEndPoint.Address.ToString();
                        OnStudentDiscovered?.Invoke(response, studentIp);
                    }
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Bỏ qua lỗi nhận lẻ tẻ để không dừng lắng nghe
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Dispose();
        _udpClient = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _cts?.Dispose();
    }
}
