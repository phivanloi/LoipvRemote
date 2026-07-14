using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Desktop.Presentation;

/// <summary>UI-only representation of a connection list or tab item.</summary>
public sealed record ConnectionPresentationModel(
    Guid Id,
    string Title,
    string Endpoint,
    ProtocolKind Protocol,
    ProtocolSessionState SessionState);
