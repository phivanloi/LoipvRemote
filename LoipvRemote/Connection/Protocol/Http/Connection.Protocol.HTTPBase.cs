using System.Runtime.Versioning;
using LoipvRemote.Protocols.Browser;
using LoipvRemote.Resources.Language;
using LoipvRemote.Tools;
using LoipvRemote.UI.Tabs;

namespace LoipvRemote.Connection.Protocol.Http;

[SupportedOSPlatform("windows")]
public class HTTPBase : ProtocolBase
{
    private readonly BrowserDesktopClient _browser;
    private string _tabTitle = string.Empty;
    private BrowserSession? _session;

    protected string httpOrS = string.Empty;
    protected int defaultPort;

    protected HTTPBase(RenderingEngine renderingEngine)
    {
        _browser = new BrowserDesktopClient(renderingEngine == RenderingEngine.EdgeChromium);
        _browser.TitleChanged += BrowserOnTitleChanged;
        _browser.InitializationFailed += (_, exception) =>
            MessageCollector.AddExceptionStackTrace(Language.HttpSetPropsFailed, exception);
        Control = _browser.Control;
    }

    public override bool Initialize()
    {
        base.Initialize();
        if (InterfaceControl.Parent is ConnectionTab tab)
            _tabTitle = tab.TabText;

        try
        {
            _session = new BrowserSession(_browser);
            return _session.Initialize(new BrowserConnectionOptions(
                InterfaceControl.Info.Hostname,
                InterfaceControl.Info.Port,
                httpOrS,
                defaultPort));
        }
        catch (Exception exception)
        {
            MessageCollector.AddExceptionStackTrace(Language.HttpSetPropsFailed, exception);
            return false;
        }
    }

    public override bool Connect()
    {
        try
        {
            if (_session is null || !_session.Connect())
                return false;
            return base.Connect();
        }
        catch (Exception exception)
        {
            MessageCollector.AddExceptionStackTrace(Language.HttpConnectFailed, exception);
            return false;
        }
    }

    public override void Close()
    {
        try
        {
            _session?.Disconnect();
            _browser.Dispose();
        }
        catch (Exception exception)
        {
            MessageCollector.AddExceptionStackTrace("Error during HTTPBase cleanup", exception);
        }

        base.Close();
    }

    private void BrowserOnTitleChanged(object? sender, string title)
    {
        try
        {
            if (InterfaceControl.Parent is not ConnectionTab tab)
                return;
            string shortTitle = title.Length >= 15 ? title[..10] + "..." : title;
            tab.TabText = string.IsNullOrEmpty(_tabTitle) ? shortTitle : _tabTitle + " - " + shortTitle;
        }
        catch (Exception exception)
        {
            MessageCollector.AddExceptionStackTrace(Language.HttpDocumentTileChangeFailed, exception);
        }
    }

    public enum RenderingEngine
    {
        [LocalizedAttributes.LocalizedDescription(nameof(Language.HttpInternetExplorer))]
        IE = 1,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.HttpCEF))]
        EdgeChromium = 2
    }
}
