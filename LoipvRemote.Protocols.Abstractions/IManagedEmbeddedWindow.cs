namespace LoipvRemote.Protocols.Abstractions;

/// <summary>
/// Marks an embedded surface that must join the desktop's managed control tree
/// before its protocol runtime is initialized.
/// </summary>
public interface IManagedEmbeddedWindow : IEmbeddedWindow
{
}
