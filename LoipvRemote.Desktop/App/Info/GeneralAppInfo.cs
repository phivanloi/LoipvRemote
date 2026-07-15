using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;
using static System.Environment;


namespace LoipvRemote.App.Info
{
    [SupportedOSPlatform("windows")]
    public static class GeneralAppInfo
    {
        public const string UrlBugs = "https://github.com/LoipvRemote/LoipvRemote/issues/new";
        /// <summary>Numeric application version without build metadata or source-revision suffixes.</summary>
        public static readonly string ApplicationVersion = NormalizeProductVersion(Application.ProductVersion);
        public static readonly string? ProductName = Application.ProductName;
        public static readonly string? Copyright = (Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCopyrightAttribute), false) as AssemblyCopyrightAttribute)?.Copyright;
        public static readonly string? HomePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);

        //public static string ReportingFilePath = "";
        private static readonly string puttyPath = HomePath + "\\PuTTYNG.exe";

        public static string UserAgent
        {
            get
            {
                List<string> details =
                [
                    "compatible",
                    OSVersion.Platform == PlatformID.Win32NT
                        ? $"Windows NT {OSVersion.Version.Major}.{OSVersion.Version.Minor}"
                        : OSVersion.VersionString
                ];
                if (Is64BitProcess)
                {
                    details.Add("WOW64");
                }

                details.Add(Thread.CurrentThread.CurrentUICulture.Name);
                details.Add($".NET CLR {Environment.Version}");
                string detailsString = string.Join("; ", [.. details]);

                return $"Mozilla/5.0 ({detailsString}) {ProductName}/{ApplicationVersion}";
            }
        }

        public static string PuttyPath => puttyPath;

        public static Version? GetApplicationVersion()
        {
            string cleanedVersion = ApplicationVersion.Split(' ')[0].Replace("(", "").Replace(")", "").Replace("Build", "");
            cleanedVersion = cleanedVersion + "." + ApplicationVersion.Split(' ')[^1].Replace(")", "");

            _ = System.Version.TryParse(cleanedVersion, out Version? parsedVersion);
            return parsedVersion;
        }

        public static string NormalizeProductVersion(string? productVersion)
        {
            if (string.IsNullOrWhiteSpace(productVersion))
                return "0.0.0.0";

            string value = productVersion.Trim();
            int metadataStart = value.IndexOfAny(['+', ' ']);
            if (metadataStart >= 0)
                value = value[..metadataStart];

            if (!System.Version.TryParse(value, out System.Version? parsed) || parsed is null)
                return "0.0.0.0";

            return new System.Version(
                parsed.Major,
                parsed.Minor,
                parsed.Build < 0 ? 0 : parsed.Build,
                parsed.Revision < 0 ? 0 : parsed.Revision).ToString();
        }
    }
}
