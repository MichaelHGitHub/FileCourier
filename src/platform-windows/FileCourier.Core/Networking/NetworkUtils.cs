using System.Linq;
using System.Net.NetworkInformation;

namespace FileCourier.Core.Networking;

public static class NetworkUtils
{
    public static string GetMacAddress()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic => nic.GetPhysicalAddress().ToString())
            .FirstOrDefault() ?? string.Empty;
    }
}
