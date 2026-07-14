namespace LoipvRemote.Domain.Connections;

/// <summary>Configuration required to start an external application connection.</summary>
public sealed record ExternalApplicationDefinition(
    string DisplayName,
    string ExecutablePath,
    string Arguments,
    string WorkingDirectory,
    bool RunElevated,
    bool EmbedWindow,
    bool WaitForExit)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(DisplayName) &&
        !string.IsNullOrWhiteSpace(ExecutablePath) &&
        !(RunElevated && EmbedWindow) &&
        !(WaitForExit && EmbedWindow);
}
