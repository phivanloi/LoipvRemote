using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Domain.Events;

public sealed record ConnectionSessionStateChanged(Guid ConnectionId, ProtocolSessionState State, DateTimeOffset OccurredAt);
