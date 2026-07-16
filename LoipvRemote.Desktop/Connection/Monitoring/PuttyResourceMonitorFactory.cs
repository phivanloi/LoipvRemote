using LoipvRemote.Protocols.Putty.Monitoring;

namespace LoipvRemote.Connection.Monitoring;

/// <summary>Desktop composition adapter that supplies protocol monitoring dependencies.</summary>
public sealed class PuttyResourceMonitorFactory(IPuttyHostKeyTrustStore hostKeyTrustStore)
{
    private readonly IPuttyHostKeyTrustStore _hostKeyTrustStore = hostKeyTrustStore ?? throw new ArgumentNullException(nameof(hostKeyTrustStore));

    public SshResourceMonitor Create(Connection.ConnectionInfo connection) => new(connection, _hostKeyTrustStore);
}
