namespace LoipvRemote.Application.Configuration;

/// <summary>Provides the configured PuTTY executable path to the desktop composition.</summary>
public interface IPuttyExecutablePathProvider
{
    string? Resolve();
}
