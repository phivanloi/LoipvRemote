using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Windows.Forms;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Browser;

public sealed class BrowserDesktopClient : IBrowserClient, IEmbeddedWindow, IDisposable
{
    private readonly bool _useEdge;
    private readonly string? _userDataFolder;
    private readonly Task _initialization;

    public BrowserDesktopClient(bool useEdge)
    {
        _useEdge = useEdge;
        if (useEdge)
        {
            _userDataFolder = Path.Combine(Path.GetTempPath(), "LoipvRemote_WebView2", Guid.NewGuid().ToString());
            WebView2 edge = new() { Dock = DockStyle.Fill };
            Control = edge;
            _initialization = InitializeEdgeAsync(edge);
        }
        else
        {
            WebBrowser browser = new()
            {
                Dock = DockStyle.Fill,
                ScrollBarsEnabled = true,
                ScriptErrorsSuppressed = true
            };
            browser.Navigated += (_, _) => browser.AllowWebBrowserDrop = false;
            browser.DocumentTitleChanged += (_, _) => TitleChanged?.Invoke(this, browser.DocumentTitle);
            Control = browser;
            _initialization = Task.CompletedTask;
        }
    }

    public event EventHandler<string>? TitleChanged;
    public event EventHandler<Exception>? InitializationFailed;

    public Control Control { get; }
    public bool IsAvailable => !Control.IsDisposed;
    public IntPtr WindowHandle => Control.IsHandleCreated ? Control.Handle : IntPtr.Zero;

    public void Navigate(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!_useEdge)
        {
            ((WebBrowser)Control).Navigate(endpoint);
            return;
        }

        WebView2 edge = (WebView2)Control;
        TaskScheduler scheduler = SynchronizationContext.Current is null
            ? TaskScheduler.Current
            : TaskScheduler.FromCurrentSynchronizationContext();
        _ = _initialization.ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully && edge.CoreWebView2 is not null && !edge.IsDisposed)
                edge.Source = endpoint;
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
            scheduler);
    }

    public void Focus() => Control.Focus();

    public void Dispose()
    {
        _ = _initialization.ContinueWith(_ => DeleteUserDataFolder(),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private async Task InitializeEdgeAsync(WebView2 edge)
    {
        try
        {
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
            await edge.EnsureCoreWebView2Async(environment);
            edge.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
            edge.CoreWebView2.DocumentTitleChanged += (_, _) =>
                TitleChanged?.Invoke(this, edge.CoreWebView2.DocumentTitle);
        }
        catch (Exception exception)
        {
            InitializationFailed?.Invoke(this, exception);
            throw;
        }
    }

    private void DeleteUserDataFolder()
    {
        if (string.IsNullOrEmpty(_userDataFolder) || !Directory.Exists(_userDataFolder))
            return;

        string expectedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LoipvRemote_WebView2"));
        string candidate = Path.GetFullPath(_userDataFolder);
        if (candidate.StartsWith(expectedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            try { Directory.Delete(candidate, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
