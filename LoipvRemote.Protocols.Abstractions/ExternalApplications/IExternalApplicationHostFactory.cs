namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Creates one platform host for each external-application session.</summary>
public interface IExternalApplicationHostFactory
{
    IExternalApplicationHost Create();
}
