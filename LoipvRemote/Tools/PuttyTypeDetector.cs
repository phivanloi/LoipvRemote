using System;
using System.Diagnostics;
using LoipvRemote.Connection.Protocol;
using System.IO;
using System.Runtime.Versioning;

namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class PuttyTypeDetector
    {
        public static PuttyType GetPuttyType()
        {
            return GetPuttyType(PuttyBase.PuttyPath);
        }

        public static PuttyType GetPuttyType(string filename)
        {
            if (string.IsNullOrEmpty(filename) || !File.Exists(filename))
                return PuttyType.Unknown;

            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filename);
            return Classify(filename, versionInfo.InternalName, versionInfo.ProductVersion, versionInfo.Comments);
        }

        internal static PuttyType Classify(string filename, string? internalName, string? productVersion, string? comments)
        {
            bool isPutty = Contains(internalName, "PuTTY");
            bool isBundledPuttyNg = Path.GetFileName(filename).Equals("PuTTYNG.exe", StringComparison.OrdinalIgnoreCase) &&
                                    (Contains(internalName, "PuTTYNG") || Contains(productVersion, "mRemoteNG (LoipvRemote)"));

            if (isBundledPuttyNg)
                return PuttyType.PuttyNg;

            if (isPutty && Contains(comments, "KiTTY"))
                return PuttyType.Kitty;

            if (isPutty && Contains(productVersion, "Xming"))
                return PuttyType.Xming;

            return isPutty ? PuttyType.Putty : PuttyType.Unknown;
        }

        private static bool Contains(string? value, string expected)
        {
            return !string.IsNullOrEmpty(value) && value.Contains(expected, StringComparison.OrdinalIgnoreCase);
        }

        public enum PuttyType
        {
            Unknown = 0,
            Putty,
            PuttyNg,
            Kitty,
            Xming
        }
    }
}
