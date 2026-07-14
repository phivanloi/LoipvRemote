using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Infrastructure.Windows.ProcessManagement;

public sealed class WindowsExternalApplicationHostFactory : IExternalApplicationHostFactory
{
    public IExternalApplicationHost Create() => new WindowsExternalApplicationHost();
}
