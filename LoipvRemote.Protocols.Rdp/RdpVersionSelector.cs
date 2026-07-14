using LoipvRemote.Domain.Protocols.Rdp;

namespace LoipvRemote.Protocols.Rdp;

/// <summary>Chooses a supported RDP client generation without depending on ActiveX UI adapters.</summary>
public static class RdpVersionSelector
{
    public static RdpVersion SelectHighestSupported(Func<RdpVersion, bool> isSupported)
    {
        ArgumentNullException.ThrowIfNull(isSupported);

        foreach (RdpVersion version in GetCandidateVersions().OrderDescending())
        {
            if (isSupported(version))
                return version;
        }

        throw new NotSupportedException("No supported RDP client generation is available.");
    }

    public static IReadOnlyList<RdpVersion> GetSupportedVersions(Func<RdpVersion, bool> isSupported)
    {
        ArgumentNullException.ThrowIfNull(isSupported);
        return GetCandidateVersions().Where(isSupported).ToArray();
    }

    private static IEnumerable<RdpVersion> GetCandidateVersions() =>
        Enum.GetValues<RdpVersion>().Where(version => version != RdpVersion.Highest);
}
