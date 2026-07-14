using LoipvRemote.Desktop.Presentation;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Desktop.UIAdapters;

/// <summary>Prevents WinForms controls from consuming Domain records directly.</summary>
public static class ConnectionPresentationMapper
{
    public static ConnectionPresentationModel ToPresentation(
        ConnectionDefinition definition,
        ProtocolSessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new ConnectionPresentationModel(
            definition.Id,
            definition.Name,
            $"{definition.Host}:{definition.Port}",
            definition.Protocol,
            sessionState);
    }
}
