using System.Net;
using System.Net.Sockets;
using LabManager.Core.Enums;
using LabManager.Core.Models;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;

namespace LabManager.Network.Discovery;

public class StudentDiscoveryListener : IDisposable
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private bool _isDisposed;

    public Func<DiscoveryResponsePacket>? ResponsePacketFactory { get; set; }
    public event Action<DiscoveryBeaconPacket, string>? OnTeacherBeaconReceived;
    public event Action<RemoteInputPacket>? OnRemoteInputReceived;
    public event Action<RemoteSessionPacket>? OnRemoteSessionReceived;

    public bool IsListening => _cts is { IsCancellationRequested: false };

    public void Start(int listenPort = NetworkConfig.DefaultDiscoveryPort)
    {
        if (IsListening) return;

        _cts = new CancellationTokenSource();

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, listenPort));

        _udpClient = new UdpClient { Client = socket };

        _ = ListenLoopAsync(_cts.Token);
    }

    private async Task ListenLoopAsync(CancellationToken ct)
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

                if (header.Type == CommandType.DiscoveryBeacon)
                {
                    var beacon = PacketSerializer.DeserializePayload<DiscoveryBeaconPacket>(
                        result.Buffer, PacketHeader.HeaderSize, header.PayloadLength);
                    if (beacon != null)
                    {
                        var teacherIp = result.RemoteEndPoint.Address;
                        OnTeacherBeaconReceived?.Invoke(beacon, teacherIp.ToString());

                        // Phản hồi trực tiếp về máy giáo viên (Unicast)
                        await SendResponseAsync(teacherIp, beacon.TeacherPort);
                    }
                }
                else if (header.Type == CommandType.RemoteInput)
                {
                    var inputCmd = PacketSerializer.DeserializePayload<RemoteInputPacket>(
                        result.Buffer, PacketHeader.HeaderSize, header.PayloadLength);
                    if (inputCmd != null)
                    {
                        OnRemoteInputReceived?.Invoke(inputCmd);
                    }
                }
                else if (header.Type == CommandType.RemoteSession)
                {
                    var sessionCmd = PacketSerializer.DeserializePayload<RemoteSessionPacket>(
                        result.Buffer, PacketHeader.HeaderSize, header.PayloadLength);
                    if (sessionCmd != null)
                    {
                        OnRemoteSessionReceived?.Invoke(sessionCmd);
                    }
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Bỏ qua lỗi tạm thời
            }
        }
    }

    private async Task SendResponseAsync(IPAddress teacherIp, int teacherPort)
    {
        if (_udpClient == null) return;

        var response = ResponsePacketFactory?.Invoke() ?? new DiscoveryResponsePacket
        {
            MachineName = Environment.MachineName,
            CurrentUser = Environment.UserName,
            CommandPort = NetworkConfig.DefaultCommandPort,
            Status = MachineStatus.Online,
            OsVersion = Environment.OSVersion.VersionString
        };

        byte[] packetBytes = PacketSerializer.Serialize(CommandType.DiscoveryResponse, response);
        var targetEndpoint = new IPEndPoint(teacherIp, teacherPort);

        try
        {
            await _udpClient.SendAsync(packetBytes, packetBytes.Length, targetEndpoint);
        }
        catch
        {
            // Bỏ qua nếu lỗi mạng
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
