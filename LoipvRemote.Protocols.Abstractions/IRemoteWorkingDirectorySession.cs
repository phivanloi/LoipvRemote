namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Exposes the current directory reported by an interactive remote shell.</summary>
public interface IRemoteWorkingDirectorySession
{
    string? CurrentWorkingDirectory { get; }
}
