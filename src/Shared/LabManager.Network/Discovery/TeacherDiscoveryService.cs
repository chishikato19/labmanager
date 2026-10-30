using System.Net;
using System.Net.NetworkInformation;
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
    private string _currentTeacherName = "Teacher";
    private string _currentRoomName = "Phòng máy";
    private bool _isDisposed;

    public int ScreenIntervalSeconds { get; set; } = 3;

    public event Action<DiscoveryResponsePacket, string>? OnStudentDiscovered;

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public void Start(string teacherName = "Teacher", string roomName = "Phòng máy", int broadcastIntervalMs = 3000)
    {
        if (IsRunning) return;

        _currentTeacherName = teacherName;
        _currentRoomName = roomName;
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

    public static List<IPAddress> GetAllBroadcastAddresses()
    {
        var broadcastList = new HashSet<IPAddress> { IPAddress.Broadcast };
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var ipProps = nic.GetIPProperties();
                foreach (var u in ipProps.UnicastAddresses)
                {
                    if (u.Address.AddressFamily == AddressFamily.InterNetwork && u.IPv4Mask != null)
                    {
                        byte[] ipBytes = u.Address.GetAddressBytes();
                        byte[] maskBytes = u.IPv4Mask.GetAddressBytes();
                        if (ipBytes.Length == 4 && maskBytes.Length == 4)
                        {
                            byte[] broadcastBytes = new byte[4];
                            for (int i = 0; i < 4; i++)
                            {
                                broadcastBytes[i] = (byte)(ipBytes[i] | (~maskBytes[i] & 0xFF));
                            }
                            broadcastList.Add(new IPAddress(broadcastBytes));
                        }
                    }
                }
            }
        }
        catch { }
        return broadcastList.ToList();
    }

    public static List<IPAddress> GetLocalSubnetHostIps()
    {
        var result = new List<IPAddress>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var ipProps = nic.GetIPProperties();
                foreach (var u in ipProps.UnicastAddresses)
                {
                    if (u.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(u.Address) &&
                        u.IPv4Mask != null)
                    {
                        byte[] ipBytes = u.Address.GetAddressBytes();
                        byte[] maskBytes = u.IPv4Mask.GetAddressBytes();
                        if (ipBytes.Length == 4 && maskBytes.Length == 4)
                        {
                            uint ipVal = (uint)((ipBytes[0] << 24) | (ipBytes[1] << 16) | (ipBytes[2] << 8) | ipBytes[3]);
                            uint maskVal = (uint)((maskBytes[0] << 24) | (maskBytes[1] << 16) | (maskBytes[2] << 8) | maskBytes[3]);
                            uint networkVal = ipVal & maskVal;
                            uint broadcastVal = networkVal | ~maskVal;

                            uint hostCount = broadcastVal - networkVal - 1;
                            if (hostCount > 0 && hostCount <= 512)
                            {
                                for (uint h = networkVal + 1; h < broadcastVal; h++)
                                {
                                    byte[] hBytes = new byte[]
                                    {
                                        (byte)((h >> 24) & 0xFF),
                                        (byte)((h >> 16) & 0xFF),
                                        (byte)((h >> 8) & 0xFF),
                                        (byte)(h & 0xFF)
                                    };
                                    result.Add(new IPAddress(hBytes));
                                }
                            }
                            else if (hostCount > 512)
                            {
                                byte b0 = ipBytes[0];
                                byte b1 = ipBytes[1];
                                byte b2 = ipBytes[2];
                                for (int i = 1; i <= 254; i++)
                                {
                                    result.Add(new IPAddress(new byte[] { b0, b1, b2, (byte)i }));
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return result.Distinct().ToList();
    }

    public async Task BroadcastBeaconNowAsync(string? teacherName = null, string? roomName = null)
    {
        if (!IsRunning || _udpClient == null) return;

        try
        {
            var beacon = new DiscoveryBeaconPacket
            {
                TeacherName = teacherName ?? _currentTeacherName,
                RoomName = roomName ?? _currentRoomName,
                TeacherPort = _localListeningPort,
                ScreenIntervalSeconds = ScreenIntervalSeconds,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            byte[] packetBytes = PacketSerializer.Serialize(CommandType.DiscoveryBeacon, beacon);
            var broadcastAddresses = GetAllBroadcastAddresses();
            foreach (var addr in broadcastAddresses)
            {
                try
                {
                    await _udpClient.SendAsync(packetBytes, packetBytes.Length, new IPEndPoint(addr, NetworkConfig.DefaultDiscoveryPort));
                }
                catch { }
            }
        }
        catch
        {
            // Bỏ qua lỗi tạm thời khi phát beacon tức thì
        }
    }

    public async Task<bool> SendBeaconToIpAsync(IPAddress targetIp, int port = NetworkConfig.DefaultDiscoveryPort)
    {
        if (!IsRunning || _udpClient == null) return false;

        try
        {
            var beacon = new DiscoveryBeaconPacket
            {
                TeacherName = _currentTeacherName,
                RoomName = _currentRoomName,
                TeacherPort = _localListeningPort,
                ScreenIntervalSeconds = ScreenIntervalSeconds,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            byte[] packetBytes = PacketSerializer.Serialize(CommandType.DiscoveryBeacon, beacon);
            int sent = await _udpClient.SendAsync(packetBytes, packetBytes.Length, new IPEndPoint(targetIp, port));
            return sent > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task ScanIpsAsync(IEnumerable<IPAddress> ips, CancellationToken ct = default, Action<int, int>? onProgress = null)
    {
        if (!IsRunning || _udpClient == null) return;

        var ipList = ips.Distinct().ToList();
        if (ipList.Count == 0) return;

        var beacon = new DiscoveryBeaconPacket
        {
            TeacherName = _currentTeacherName,
            RoomName = _currentRoomName,
            TeacherPort = _localListeningPort,
            ScreenIntervalSeconds = ScreenIntervalSeconds,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        byte[] packetBytes = PacketSerializer.Serialize(CommandType.DiscoveryBeacon, beacon);

        // Phát 2 đợt để đảm bảo không bị rớt gói UDP trên mạng LAN / Wi-Fi
        for (int pass = 0; pass < 2; pass++)
        {
            if (ct.IsCancellationRequested) break;
            int sent = 0;
            foreach (var chunk in ipList.Chunk(25))
            {
                if (ct.IsCancellationRequested) break;
                var tasks = chunk.Select(ip =>
                {
                    try
                    {
                        return _udpClient.SendAsync(packetBytes, packetBytes.Length, new IPEndPoint(ip, NetworkConfig.DefaultDiscoveryPort));
                    }
                    catch
                    {
                        return Task.FromResult(0);
                    }
                });

                await Task.WhenAll(tasks);
                sent += chunk.Length;
                if (pass == 0)
                {
                    onProgress?.Invoke(sent, ipList.Count);
                }
                await Task.Delay(15, ct);
            }
            await Task.Delay(200, ct);
        }
    }

    private async Task BroadcastBeaconLoopAsync(string teacherName, string roomName, int intervalMs, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var beacon = new DiscoveryBeaconPacket
                {
                    TeacherName = teacherName,
                    RoomName = roomName,
                    TeacherPort = _localListeningPort,
                    ScreenIntervalSeconds = ScreenIntervalSeconds,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                byte[] packetBytes = PacketSerializer.Serialize(CommandType.DiscoveryBeacon, beacon);
                if (_udpClient != null)
                {
                    var broadcastAddresses = GetAllBroadcastAddresses();
                    foreach (var addr in broadcastAddresses)
                    {
                        try
                        {
                            await _udpClient.SendAsync(packetBytes, packetBytes.Length, new IPEndPoint(addr, NetworkConfig.DefaultDiscoveryPort));
                        }
                        catch { }
                    }
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
