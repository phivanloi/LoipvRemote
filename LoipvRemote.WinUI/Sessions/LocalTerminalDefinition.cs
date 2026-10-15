using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.WinUI.ViewModels;

namespace LoipvRemote.WinUI.Sessions;

internal static class LocalTerminalDefinition
{
    private const string SessionKindOption = "__LoipvRemoteSessionKind";
    private const string SessionKindValue = "LocalTerminal";

    internal static Guid SidebarItemId { get; } = new("5D269080-079F-4DDC-B741-77AE8C2234EF");

    internal static ConnectionTreeItem CreateSidebarItem(bool hasOpenTerminalTab = false) => new(
        SidebarItemId,
        "Terminal",
        IsFolder: false,
        Children: [],
        IsConnected: hasOpenTerminalTab,
        Protocol: ProtocolKind.Ssh2,
        IsLocalTerminal: true);

    internal static ConnectionDefinition CreateSession(int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);

        string name = sequence == 1 ? "Terminal" : $"Terminal {sequence}";
        return new ConnectionDefinition(
            Guid.NewGuid(),
            name,
            Environment.MachineName,
            22,
            ProtocolKind.Ssh2,
            CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SessionKindOption] = SessionKindValue
                },
                Array.Empty<string>()));
    }

    internal static bool IsLocalTerminal(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Options?.Values.TryGetValue(SessionKindOption, out string? kind) == true &&
            string.Equals(kind, SessionKindValue, StringComparison.Ordinal);
    }
}
