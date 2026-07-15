using LoipvRemote.Connection;
using LoipvRemote.Messages;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.Tools;

public interface IExternalToolRuntime
{
    MessageCollector MessageCollector { get; }
    IStringSecretStore UserSecretStore { get; }
    ConnectionInitiator ConnectionInitiator { get; }
}
