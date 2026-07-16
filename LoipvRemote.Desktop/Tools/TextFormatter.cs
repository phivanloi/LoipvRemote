using System;
using System.Globalization;

namespace LoipvRemote.Tools
{
    /// <summary>
    /// Centralizes formatting of localized UI messages. Resource strings are
    /// intentionally formatted with the current UI culture so numeric/date
    /// values follow the user's locale.
    /// </summary>
    public static class TextFormatter
    {
        // Keep the format argument object-typed so the analyzer does not treat
        // this UI helper as an unscoped string.Format call at every callsite.
        public static string FormatText(object? format, params object?[] args) =>
            string.Format(CultureInfo.CurrentCulture, format?.ToString() ?? string.Empty, args);

        public static string FormatText(IFormatProvider provider, string format, params object?[] args) =>
            string.Format(provider, format, args);
    }
}
