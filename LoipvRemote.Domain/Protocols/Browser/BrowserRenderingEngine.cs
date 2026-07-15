using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Browser;

public enum BrowserRenderingEngine
{
    [ProtocolDisplayKey("HttpInternetExplorer")]
    IE = 1,

    [ProtocolDisplayKey("HttpCEF")]
    EdgeChromium = 2
}
