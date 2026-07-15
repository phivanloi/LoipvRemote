using LoipvRemote.Connection;
using LoipvRemote.Messages;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.Tools;

public sealed class ExternalToolRuntime(
    MessageCollector messageCollector,
    IStringSecretStore userSecretStore,
    ConnectionInitiator connectionInitiator) : IExternalToolRuntime
{
    public MessageCollector MessageCollector { get; } = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
    public IStringSecretStore UserSecretStore { get; } = userSecretStore ?? throw new ArgumentNullException(nameof(userSecretStore));
    public ConnectionInitiator ConnectionInitiator { get; } = connectionInitiator ?? throw new ArgumentNullException(nameof(connectionInitiator));
}
