using LoipvRemote.Domain.Connections;

namespace LoipvRemote.UseCases.Configuration;

/// <summary>Host adapter that supplies the currently selected persistence backend.</summary>
public interface IConnectionStoreOptionsProvider
{
    ConnectionDefinitionStoreOptions GetOptions(bool useDatabase, string connectionFileName);
}
