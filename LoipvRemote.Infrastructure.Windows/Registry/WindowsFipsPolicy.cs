using Microsoft.Win32;

namespace LoipvRemote.Infrastructure.Windows.Registry;

public static class WindowsFipsPolicy
{
    public static bool IsEnabled()
    {
        using RegistryKey? legacyKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"System\CurrentControlSet\Control\Lsa");
        if (legacyKey?.GetValue("FIPSAlgorithmPolicy") is int legacyValue && legacyValue != 0)
            return true;

        using RegistryKey? currentKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"System\CurrentControlSet\Control\Lsa\FIPSAlgorithmPolicy");
        return currentKey?.GetValue("Enabled") is int currentValue && currentValue != 0;
    }
}
