using System.Linq;

namespace mRemoteNG.UI.Window
{
    internal static class TerminalTitleFormatter
    {
        private const int MaximumTerminalTitleLength = 200;

        internal static string Format(string? terminalTitle, string connectionName)
        {
            string cleanTitle = new((terminalTitle ?? string.Empty)
                .Where(character => !char.IsControl(character))
                .Take(MaximumTerminalTitleLength)
                .ToArray());
            cleanTitle = cleanTitle.Trim();

            string result = string.IsNullOrEmpty(cleanTitle)
                ? connectionName
                : $"{cleanTitle} ({connectionName})";

            return result.Replace("&", "&&");
        }
    }
}
