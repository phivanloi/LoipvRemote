namespace LoipvRemote.Application.Configuration;

/// <summary>Creates an explicitly configured connection definition store.</summary>
public interface IConnectionDefinitionStoreFactory
{
    IConnectionDefinitionStore Create(ConnectionDefinitionStoreOptions options);
}

public enum ConnectionDefinitionStoreKind
{
    Xml,
    SqlServer
}

/// <summary>Non-secret store selection and location supplied by application configuration.</summary>
public sealed record ConnectionDefinitionStoreOptions(
    ConnectionDefinitionStoreKind Kind,
    string Location);
