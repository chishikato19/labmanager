using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace LabManager.Native.System;

public static class WakeOnLanHelper
{
    public static byte[] BuildMagicPacket(string macAddress)
    {
        string cleanMac = macAddress.Replace(":", "").Replace("-", "").Replace(".", "").Trim();
        if (cleanMac.Length != 12)
        {
            throw new ArgumentException("Địa chỉ MAC phải chứa đúng 12 ký tự hex (ví dụ: AA:BB:CC:DD:EE:FF).", nameof(macAddress));
        }

        byte[] macBytes = new byte[6];
        for (int i = 0; i < 6; i++)
        {
            macBytes[i] = byte.Parse(cleanMac.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        // Magic Packet: 6 bytes 0xFF + 16 lần MAC (102 bytes)
        byte[] packet = new byte[102];
        Array.Fill(packet, (byte)0xFF, 0, 6);

        for (int i = 0; i < 16; i++)
        {
            Buffer.BlockCopy(macBytes, 0, packet, 6 + (i * 6), 6);
        }

        return packet;
    }

    public static async Task SendWakeOnLanAsync(string macAddress, int port = 9, IPAddress? broadcastIp = null)
    {
        byte[] magicPacket = BuildMagicPacket(macAddress);
        var targetIp = broadcastIp ?? IPAddress.Broadcast;

        using var client = new UdpClient();
        client.EnableBroadcast = true;
        await client.SendAsync(magicPacket, magicPacket.Length, new IPEndPoint(targetIp, port));
    }
}
