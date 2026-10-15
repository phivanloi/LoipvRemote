using System.ComponentModel;
using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Vnc;

public enum VncSpecialKey
{
    CtrlAltDel,
    CtrlEsc
}

public enum VncCompression
{
    [ProtocolDisplayKey("NoCompression")]
    CompNone = 99,
    [Description("0")] Comp0 = 0,
    [Description("1")] Comp1 = 1,
    [Description("2")] Comp2 = 2,
    [Description("3")] Comp3 = 3,
    [Description("4")] Comp4 = 4,
    [Description("5")] Comp5 = 5,
    [Description("6")] Comp6 = 6,
    [Description("7")] Comp7 = 7,
    [Description("8")] Comp8 = 8,
    [Description("9")] Comp9 = 9
}

public enum VncEncoding
{
    [Description("Raw")] EncRaw,
    [Description("RRE")] EncRRE,
    [Description("CoRRE")] EncCorre,
    [Description("Hextile")] EncHextile,
    [Description("Zlib")] EncZlib,
    [Description("Tight")] EncTight,
    [Description("ZlibHex")] EncZLibHex,
    [Description("ZRLE")] EncZRLE
}

public enum VncAuthMode
{
    [ProtocolDisplayKey("Vnc")]
    AuthVNC,

    [ProtocolDisplayKey("Windows")]
    AuthWin
}

public enum VncProxyType
{
    [ProtocolDisplayKey("None")]
    ProxyNone,

    [ProtocolDisplayKey("Http")]
    ProxyHTTP,

    [ProtocolDisplayKey("Socks5")]
    ProxySocks5,

    [ProtocolDisplayKey("UltraVncRepeater")]
    ProxyUltra
}

public enum VncColors
{
    [ProtocolDisplayKey("Normal")]
    ColNormal,
    [Description("8-bit")] Col8Bit
}

public enum VncSmartSizeMode
{
    [ProtocolDisplayKey("NoSmartSize")]
    SmartSNo,

    [ProtocolDisplayKey("Free")]
    SmartSFree,

    [ProtocolDisplayKey("Aspect")]
    SmartSAspect
}
