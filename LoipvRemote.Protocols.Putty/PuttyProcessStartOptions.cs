namespace LoipvRemote.Protocols.Putty;

public sealed record PuttyProcessStartOptions(
    string ExecutablePath,
    string Arguments,
    bool StartMinimized,
    bool StartHidden = false);
