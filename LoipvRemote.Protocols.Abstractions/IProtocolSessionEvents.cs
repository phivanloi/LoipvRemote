namespace LoipvRemote.Protocols.Abstractions;

/// <summary>
/// Optional asynchronous lifecycle events exposed by protocol runtimes whose
/// Connect call only starts a connection attempt.
/// </summary>
public interface IProtocolSessionEvents
{
    event EventHandler? Connecting;
    event EventHandler? Connected;
    event EventHandler<ProtocolSessionDisconnectedEventArgs>? Disconnected;
    event EventHandler<ProtocolSessionErrorEventArgs>? ErrorOccurred;
}

public sealed class ProtocolSessionDisconnectedEventArgs(string message, int? code) : EventArgs
{
    public string Message { get; } = message;
    public int? Code { get; } = code;
}

public sealed class ProtocolSessionErrorEventArgs(string message, int? code) : EventArgs
{
    public string Message { get; } = message;
    public int? Code { get; } = code;
}
