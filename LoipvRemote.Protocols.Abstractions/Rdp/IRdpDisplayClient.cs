namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Optional display controls exposed by the platform RDP client.</summary>
public interface IRdpDisplayClient
{
    bool SmartSize { get; set; }
    bool FullScreen { get; set; }
    bool ViewOnly { get; set; }
}
