using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LabManager.Native.System;

public static class NetworkInfoHelper
{
    public static (string IpAddress, string MacAddress) GetActiveNetworkInfo()
    {
        string ip = "127.0.0.1";
        string mac = "00:00:00:00:00:00";

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            // Bỏ qua các card mạng ảo phổ biến nếu có card vật lý
            string name = nic.Description.ToLowerInvariant();
            if (name.Contains("virtual") || name.Contains("pseudo") || name.Contains("hyper-v"))
                continue;

            var ipProps = nic.GetIPProperties();
            foreach (var addr in ipProps.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(addr.Address))
                {
                    ip = addr.Address.ToString();
                    mac = string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                    return (ip, mac);
                }
            }
        }

        // Fallback nếu máy chỉ có card mạng ảo
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus == OperationalStatus.Up &&
                nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            {
                var ipProps = nic.GetIPProperties();
                var addr = ipProps.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (addr != null)
                {
                    ip = addr.Address.ToString();
                    mac = string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                    return (ip, mac);
                }
            }
        }

        return (ip, mac);
    }
}
