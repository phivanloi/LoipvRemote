using System.Globalization;
using LoipvRemote.Resources.Language;

namespace LoipvRemote.Connection.Monitoring
{
    internal static class RemoteResourceText
    {
        internal static string Get(string resourceKey, string fallback) =>
            Language.ResourceManager.GetString(resourceKey, CultureInfo.CurrentUICulture) ?? fallback;

        internal static string Format(string resourceKey, string fallback, params object[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, Get(resourceKey, fallback), arguments);
    }
}
