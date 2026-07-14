using LoipvRemote.Domain.Connections;
using LoipvRemote.UseCases.Configuration;

namespace LoipvRemoteTests.TestHelpers;

public sealed class XmlConnectionStoreOptionsProvider : IConnectionStoreOptionsProvider
{
    public ConnectionDefinitionStoreOptions GetOptions(bool useDatabase, string connectionFileName)
    {
        if (useDatabase)
            throw new NotSupportedException("The test provider supports XML stores only.");
        return new ConnectionDefinitionStoreOptions(ConnectionDefinitionStoreKind.Xml, connectionFileName);
    }
}
