using System.Globalization;

namespace LoipvRemote.WinUI.Hosting;

/// <summary>
/// Captures only native-window state needed to diagnose a failed embedded
/// session. Connection names, endpoints and credentials are intentionally not
/// written to this file.
/// </summary>
internal static class EmbeddingDiagnostics
{
    private static readonly object SyncRoot = new();
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LoipvRemote",
        "embedding-diagnostics.log");

    internal static void Write(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                string? directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.AppendAllText(
                    FilePath,
                    string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"));
            }
        }
        catch
        {
            // Diagnostics must never alter connection behaviour.
        }
    }
}
