namespace LoipvRemote.Protocols.Abstractions;

[Flags]
public enum ProtocolCapabilities
{
    None = 0,
    EmbeddedWindow = 1,
    Resize = 2,
    Reconnect = 4,
    Clipboard = 8,
    FileTransfer = 16,
    CredentialInjection = 32,
    InputForwarding = 64
}
