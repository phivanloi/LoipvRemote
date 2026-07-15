using System.Text;

namespace LoipvRemote.Protocols.Rdp;

/// <summary>Encodes Azure load-balance data for the Windows RDP ActiveX control.</summary>
public static class AzureLoadBalanceInfoEncoder
{
    public static string Encode(string loadBalanceInfo)
    {
        ArgumentNullException.ThrowIfNull(loadBalanceInfo);

        if (loadBalanceInfo.Length % 2 == 1)
            loadBalanceInfo += " ";

        loadBalanceInfo += "\r\n";
        return Encoding.Unicode.GetString(Encoding.UTF8.GetBytes(loadBalanceInfo));
    }
}
