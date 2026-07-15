using LoipvRemote.Protocols.Putty;
using LoipvRemote.UseCases.Configuration;

namespace LoipvRemote.Desktop.Composition;

/// <summary>Default desktop locator used when the application has no custom PuTTY path.</summary>
public sealed class SystemPuttyExecutablePathProvider : IPuttyExecutablePathProvider
{
    private readonly SystemPuttyExecutableLocator _locator = new();

    public string? Resolve() => _locator.Locate();
}
