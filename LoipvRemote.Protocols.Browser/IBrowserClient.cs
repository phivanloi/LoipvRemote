namespace LoipvRemote.Protocols.Browser;

/// <summary>Host-neutral browser surface used by the browser protocol lifecycle.</summary>
public interface IBrowserClient
{
    void Navigate(Uri endpoint);
}
