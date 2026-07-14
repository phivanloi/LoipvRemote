using System.Management;

namespace LoipvRemote.Infrastructure.Windows.SystemInfo;

public static class WindowsSystemInfo
{
    public static string GetOperatingSystemDescription()
    {
        using ManagementObjectSearcher searcher = new(
            "SELECT Caption, ServicePackMajorVersion FROM Win32_OperatingSystem WHERE Primary=True");
        foreach (ManagementBaseObject item in searcher.Get())
        {
            string caption = Convert.ToString(item.GetPropertyValue("Caption"))?.Trim() ?? string.Empty;
            int servicePack = Convert.ToInt32(item.GetPropertyValue("ServicePackMajorVersion"));
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
            int addressWidth = Convert.ToInt32(item.GetPropertyValue("AddressWidth"));
            return $"{addressWidth}-bit";
        }
        return string.Empty;
    }
}
