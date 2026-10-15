using System.Text.Json;
using System.Text;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Infrastructure.Windows.Process;
using LoipvRemote.Protocols.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace LoipvRemote.WinUI.Sessions;

/// <summary>Runs the bundled PowerShell in ConPTY and renders it with xterm.js.</summary>
internal sealed class LocalTerminalSession : IProtocolSession, IWinUIContentSession
{
    private const string TerminalVirtualHost = "terminal.loipvremote";
    private readonly ConnectionDefinition _definition;
    private readonly DispatcherQueue _dispatcher;
    private readonly LocalPowerShellRuntimeLocator _runtimeLocator;
    private readonly ConPtyProcessHost _process = new();
    private readonly TaskCompletionSource<TerminalSize> _rendererReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _outputGate = new();
    private readonly StringBuilder _pendingOutput = new();
    private Grid? _view;
    private WebView2? _webView;
    private ProtocolSessionState _state = ProtocolSessionState.Created;
    private bool _rendererCanReceiveOutput;
    private int _focusRequested;
    private int _disposed;

    internal LocalTerminalSession(
        ConnectionDefinition definition,
        DispatcherQueue dispatcher,
        LocalPowerShellRuntimeLocator runtimeLocator)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _runtimeLocator = runtimeLocator ?? throw new ArgumentNullException(nameof(runtimeLocator));
        _process.OutputReceived += ProcessOnOutputReceived;
        _process.Exited += ProcessOnExited;
    }

    public ProtocolSessionState State => _state;

    public ProtocolCapabilities Capabilities =>
        ProtocolCapabilities.Resize |
        ProtocolCapabilities.Clipboard |
        ProtocolCapabilities.InputForwarding;

    public FrameworkElement View =>
        _view ?? throw new InvalidOperationException("The terminal view has not been initialized.");

    public async ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_state is ProtocolSessionState.Initialized or ProtocolSessionState.Connected)
            return true;

        try
        {
            await RunOnUiThreadAsync(InitializeRendererAsync, cancellationToken).ConfigureAwait(false);
            _state = ProtocolSessionState.Initialized;
            return true;
        }
        catch
        {
            _state = ProtocolSessionState.Faulted;
            throw;
        }
    }

    public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_state == ProtocolSessionState.Connected)
            return ValueTask.FromResult(true);
        if (_state != ProtocolSessionState.Initialized)
            return ValueTask.FromResult(false);

        try
        {
            TerminalSize size = _rendererReady.Task.IsCompletedSuccessfully
                ? _rendererReady.Task.Result
                : new TerminalSize(120, 30);
            string executable = _runtimeLocator.Resolve();
            string workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _process.Start(
                executable,
                "-NoLogo",
                workingDirectory,
                ClampColumns(size.Columns),
                ClampRows(size.Rows));
            _state = ProtocolSessionState.Connected;
            Focus();
            return ValueTask.FromResult(true);
        }
        catch
        {
            _state = ProtocolSessionState.Faulted;
            throw;
        }
    }

    public void Focus()
    {
        if (_state != ProtocolSessionState.Connected)
            return;

        Volatile.Write(ref _focusRequested, 1);
        QueueRendererFocus();
    }

    public void Activate() => Focus();

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_state is ProtocolSessionState.Closed or ProtocolSessionState.Created)
            return;
        _state = ProtocolSessionState.Closing;
        await _process.DisposeAsync().ConfigureAwait(false);
        _state = ProtocolSessionState.Closed;
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default) =>
        DisconnectAsync(cancellationToken);

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _process.OutputReceived -= ProcessOnOutputReceived;
        _process.Exited -= ProcessOnExited;
        await _process.DisposeAsync().ConfigureAwait(false);
        await RunOnUiThreadAsync(() =>
        {
            if (_webView is not null)
            {
                _webView.WebMessageReceived -= WebViewOnWebMessageReceived;
                _webView.NavigationCompleted -= WebViewOnNavigationCompleted;
                _webView.Loaded -= WebViewOnLoaded;
                _webView.Close();
                _webView = null;
            }
            _view?.Children.Clear();
            return Task.CompletedTask;
        }, CancellationToken.None).ConfigureAwait(false);
        _state = ProtocolSessionState.Closed;
        GC.SuppressFinalize(this);
    }

    private async Task InitializeRendererAsync()
    {
        if (_webView is not null)
            return;

        _view = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _webView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _view.Children.Add(_webView);
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            TerminalVirtualHost,
            Path.Combine(AppContext.BaseDirectory, "TerminalAssets"),
            CoreWebView2HostResourceAccessKind.DenyCors);
        _webView.WebMessageReceived += WebViewOnWebMessageReceived;
        _webView.NavigationCompleted += WebViewOnNavigationCompleted;
        _webView.Loaded += WebViewOnLoaded;
        _webView.Source = new Uri($"https://{TerminalVirtualHost}/index.html");
    }

    private void WebViewOnLoaded(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        QueueRendererFocus();
    }

    private void WebViewOnNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            _rendererReady.TrySetException(
                new InvalidOperationException($"The terminal renderer failed to load ({args.WebErrorStatus})."));
        }
    }

    private async void WebViewOnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using JsonDocument message = JsonDocument.Parse(args.WebMessageAsJson);
            JsonElement root = message.RootElement;
            if (!root.TryGetProperty("kind", out JsonElement kindElement))
                return;

            switch (kindElement.GetString())
            {
                case "ready":
                    TerminalSize readySize = ReadTerminalSize(root);
                    _rendererReady.TrySetResult(readySize);
                    _process.Resize(ClampColumns(readySize.Columns), ClampRows(readySize.Rows));
                    FlushPendingOutput();
                    QueueRendererFocus();
                    break;
                case "input" when root.TryGetProperty("data", out JsonElement data):
                    await _process.WriteAsync(data.GetString() ?? string.Empty).ConfigureAwait(false);
                    break;
                case "resize":
                    TerminalSize size = ReadTerminalSize(root);
                    _process.Resize(ClampColumns(size.Columns), ClampRows(size.Rows));
                    break;
                case "error":
                    string rendererError = root.TryGetProperty("message", out JsonElement errorMessage)
                        ? errorMessage.GetString() ?? "The terminal renderer failed."
                        : "The terminal renderer failed.";
                    _rendererReady.TrySetException(new InvalidOperationException(rendererError));
                    break;
            }
        }
        catch (InvalidOperationException) when (!_process.IsRunning)
        {
            // Renderer resize/input can race with process startup or teardown.
        }
        catch (ObjectDisposedException)
        {
            // The tab was closed while a WebView message was queued.
        }
    }

    private static TerminalSize ReadTerminalSize(JsonElement root)
    {
        int columns = root.TryGetProperty("columns", out JsonElement columnsElement)
            ? columnsElement.GetInt32()
            : 120;
        int rows = root.TryGetProperty("rows", out JsonElement rowsElement)
            ? rowsElement.GetInt32()
            : 30;
        return new TerminalSize(columns, rows);
    }

    private void ProcessOnOutputReceived(string output)
    {
        lock (_outputGate)
        {
            if (!_rendererCanReceiveOutput)
            {
                _pendingOutput.Append(output);
                return;
            }
        }

        _ = PostMessageAsync(new { kind = "output", data = output });
    }

    private void FlushPendingOutput()
    {
        string output;
        lock (_outputGate)
        {
            _rendererCanReceiveOutput = true;
            output = _pendingOutput.ToString();
            _pendingOutput.Clear();
        }

        if (output.Length > 0)
            _ = PostMessageAsync(new { kind = "output", data = output });
    }

    private void ProcessOnExited() => _state = ProtocolSessionState.Closed;

    private Task PostMessageAsync(object message) =>
        RunOnUiThreadAsync(() =>
        {
            _webView?.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(message));
            return Task.CompletedTask;
        }, CancellationToken.None);

    private void QueueRendererFocus()
    {
        _ = RunOnUiThreadAsync(() =>
        {
            if (_state != ProtocolSessionState.Connected ||
                Volatile.Read(ref _focusRequested) == 0 ||
                _webView is not { IsLoaded: true })
                return Task.CompletedTask;

            _webView.Focus(FocusState.Programmatic);
            _ = PostMessageAsync(new { kind = "focus" });
            return Task.CompletedTask;
        }, CancellationToken.None);
    }

    private Task RunOnUiThreadAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        if (_dispatcher.HasThreadAccess)
            return action();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        if (!_dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            completion.TrySetException(new InvalidOperationException("The WinUI dispatcher is unavailable."));
        }
        return completion.Task;
    }

    private static short ClampColumns(int columns) => (short)Math.Clamp(columns, 2, short.MaxValue);
    private static short ClampRows(int rows) => (short)Math.Clamp(rows, 1, short.MaxValue);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed != 0, this);

    private readonly record struct TerminalSize(int Columns, int Rows);
}
