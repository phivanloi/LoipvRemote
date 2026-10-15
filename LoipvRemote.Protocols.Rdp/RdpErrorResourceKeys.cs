namespace LoipvRemote.Protocols.Rdp;

public static class RdpErrorResourceKeys
{
    public static string GetErrorResourceKey(int errorCode) => errorCode switch
    {
        1 => "RdpErrorCode1",
        2 => "RdpErrorOutOfMemory",
        3 => "RdpErrorWindowCreation",
        4 => "RdpErrorCode2",
        5 => "RdpErrorCode3",
        6 => "RdpErrorCode4",
        7 => "RdpErrorConnection",
        100 => "RdpErrorWinsock",
        _ => "RdpErrorUnknown"
    };
}
