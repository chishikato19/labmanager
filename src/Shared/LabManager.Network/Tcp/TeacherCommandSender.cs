using System.Net.Sockets;
using LabManager.Core.Models;

namespace LabManager.Network.Tcp;

public class TeacherCommandSender
{
    public async Task<bool> SendCommandAsync(string targetIp, byte[] packetBytes, int targetPort = NetworkConfig.DefaultCommandPort, int timeoutMs = 3000)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);

            await client.ConnectAsync(targetIp, targetPort, cts.Token);
            using var stream = client.GetStream();
            await stream.WriteAsync(packetBytes, cts.Token);
            await stream.FlushAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> SendToMultipleAsync(IEnumerable<string> targetIps, byte[] packetBytes, int targetPort = NetworkConfig.DefaultCommandPort)
    {
        var tasks = targetIps.Distinct().Select(async ip =>
        {
            bool success = await SendCommandAsync(ip, packetBytes, targetPort);
            return (Ip: ip, Success: success);
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(r => r.Ip, r => r.Success);
    }
}
