using System.Diagnostics;
using System.Runtime.Versioning;

namespace LoipvRemote.Protocols.Putty;

[SupportedOSPlatform("windows")]
public static class PuttyTypeDetector
{
    public static PuttyType GetPuttyType(string filename)
    {
        if (string.IsNullOrEmpty(filename) || !File.Exists(filename))
            return PuttyType.Unknown;

        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filename);
        return Classify(filename, versionInfo.InternalName, versionInfo.ProductVersion, versionInfo.Comments);
    }

    public static PuttyType Classify(string filename, string? internalName, string? productVersion, string? comments)
    {
        bool isPutty = Contains(internalName, "PuTTY");
        bool isBundledPuttyNg = Path.GetFileName(filename).Equals("PuTTYNG.exe", StringComparison.OrdinalIgnoreCase) &&
                                (Contains(internalName, "PuTTYNG") || Contains(productVersion, "mRemoteNG (LoipvRemote)"));

        if (isBundledPuttyNg) return PuttyType.PuttyNg;
        if (isPutty && Contains(comments, "KiTTY")) return PuttyType.Kitty;
        if (isPutty && Contains(productVersion, "Xming")) return PuttyType.Xming;
        return isPutty ? PuttyType.Putty : PuttyType.Unknown;
    }

    private static bool Contains(string? value, string expected) =>
        !string.IsNullOrEmpty(value) && value.Contains(expected, StringComparison.OrdinalIgnoreCase);

    public enum PuttyType { Unknown = 0, Putty, PuttyNg, Kitty, Xming }
}
