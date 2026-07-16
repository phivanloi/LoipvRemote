using System.Management;
using System.Globalization;

namespace LoipvRemote.Infrastructure.Windows.SystemInfo;

public static class WindowsSystemInfo
{
    public static string GetOperatingSystemDescription()
    {
        using ManagementObjectSearcher searcher = new(
            "SELECT Caption, ServicePackMajorVersion FROM Win32_OperatingSystem WHERE Primary=True");
        foreach (ManagementBaseObject item in searcher.Get())
        {
            string caption = Convert.ToString(item.GetPropertyValue("Caption"), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            int servicePack = Convert.ToInt32(item.GetPropertyValue("ServicePackMajorVersion"), CultureInfo.InvariantCulture);
            return servicePack == 0 ? caption : $"{caption} Service Pack {servicePack}";
        }
        return string.Empty;
    }

    public static string GetProcessorArchitecture()
    {
        using ManagementObjectSearcher searcher = new(
            "SELECT AddressWidth FROM Win32_Processor WHERE DeviceID='CPU0'");
        foreach (ManagementBaseObject item in searcher.Get())
        {
            int addressWidth = Convert.ToInt32(item.GetPropertyValue("AddressWidth"), CultureInfo.InvariantCulture);
            return $"{addressWidth}-bit";
        }
        return string.Empty;
    }
}
