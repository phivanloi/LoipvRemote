using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.WinUI.Hosting;

namespace LoipvRemote.WinUI.Sessions;

/// <summary>Owns tab session lifecycle independently of a visual docking implementation.</summary>
public sealed class RemoteSessionWorkspace(IWinUIProtocolSessionFactory protocolFactory)
{
    private readonly IWinUIProtocolSessionFactory _protocolFactory = protocolFactory ?? throw new ArgumentNullException(nameof(protocolFactory));
    private readonly Dictionary<Guid, RemoteSessionTab> _tabs = [];

    public RemoteSessionTab Open(Domain.Connections.ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_tabs.TryGetValue(definition.Id, out RemoteSessionTab? existing))
            return existing;

        var tab = new RemoteSessionTab(definition);
        _tabs.Add(definition.Id, tab);
        return tab;
    }

    public async Task ConnectAsync(
        RemoteSessionTab tab,
        IEmbeddedSessionSurface surface,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(surface);
        await tab.LifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (tab.State == RemoteSessionTabState.Connected)
                return;

            IProtocolSession session = _protocolFactory.Create(tab.Connection);
            CancellationToken connectionCancellationToken = tab.BeginConnecting(session, cancellationToken);
            bool hasEmbeddedSurface = session is IEmbeddedWindow;
            if (hasEmbeddedSurface)
            {
                surface.EnsureHostWindow();
                surface.SetVisible(false);
            }
            if (session is IEmbeddedWindowHost host)
                host.SetHostWindowHandle(surface.Handle);

            if (session is IEmbeddedWindow embedded && session is not IEmbeddedWindowHost &&
                !surface.Attach(embedded, TimeSpan.FromSeconds(10)))
            {
                throw new InvalidOperationException("The protocol surface could not be attached to the WinUI session host.");
            }

            if (!await session.InitializeAsync(connectionCancellationToken))
                throw new InvalidOperationException("The protocol session could not be initialized.");

            // RDP ActiveX must be attached before initialization so ATL can
            // create the control.  Its HWND is only available after that
            // initialization, however, so attach once more to force the first
            // real resize instead of leaving the control at its 1x1 creation
            // size until a tab switch happens to relayout it.
            if (session is IEmbeddedWindow initializedEmbedded && session is not IEmbeddedWindowHost &&
                !surface.Attach(initializedEmbedded, TimeSpan.FromSeconds(10)))
            {
                throw new InvalidOperationException("The initialized protocol surface could not be attached to the WinUI session host.");
            }

            if (!await session.ConnectAsync(connectionCancellationToken))
                throw new InvalidOperationException("The protocol session could not connect.");

            if (session is IEmbeddedWindow connectedEmbedded && session is IEmbeddedWindowHost &&
                !surface.Attach(connectedEmbedded, TimeSpan.FromSeconds(10)))
            {
                throw new InvalidOperationException("The connected protocol window could not be attached to the WinUI session host.");
            }

            tab.MarkConnected(session);
            if (hasEmbeddedSurface)
            {
                surface.SetVisible(true);
                surface.Focus();
                // PuTTY can create its terminal input queue after its HWND is
                // attached. Reclaim focus after the first WinUI layout pass so
                // a newly connected SSH tab accepts typing without requiring a
                // manual tab switch.
                surface.RestoreFocusAfterTransition();
            }
            else
            {
                surface.SetVisible(false);
                session.Focus();
            }
        }
        catch
        {
            if (tab.Session is { } session)
            {
                await DisposeSessionAsync(session, CancellationToken.None);
                tab.MarkFaulted(session);
            }
            throw;
        }
        finally
        {
            tab.LifecycleGate.Release();
        }
    }

    public static void Activate(RemoteSessionTab tab, IEmbeddedSessionSurface surface)
    {
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(surface);
        if (tab.State != RemoteSessionTabState.Connected || tab.Session is null)
            return;

        if (tab.Session is not IEmbeddedWindow embedded)
        {
            surface.SetVisible(false);
            tab.Session.Focus();
            return;
        }

        surface.EnsureHostWindow();
        if (!surface.Attach(embedded, TimeSpan.FromSeconds(10)))
            throw new InvalidOperationException("The connected protocol window could not be activated in the WinUI session host.");

        surface.SetVisible(true);
        surface.Focus();
    }

    public static void Deactivate(IEmbeddedSessionSurface? surface) => surface?.SetVisible(false);

    public async Task CloseAsync(RemoteSessionTab tab, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tab);
        tab.CancelPendingConnection();
        await tab.LifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _tabs.Remove(tab.Connection.Id);
        try
        {
            if (tab.Session is { } session)
                await DisposeSessionAsync(session, cancellationToken);
        }
        finally
        {
            tab.MarkClosed();
            tab.LifecycleGate.Release();
        }
    }

    public async Task CloseAllAsync(CancellationToken cancellationToken = default)
    {
        List<Exception>? failures = null;
        RemoteSessionTab[] tabs = _tabs.Values.ToArray();
        foreach (RemoteSessionTab tab in tabs)
            tab.CancelPendingConnection();

        foreach (RemoteSessionTab tab in tabs)
        {
            try
            {
                await CloseAsync(tab, cancellationToken);
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is { Count: > 0 })
            throw new AggregateException("One or more protocol sessions did not close cleanly.", failures);
    }

    private static async Task DisposeSessionAsync(IProtocolSession session, CancellationToken cancellationToken = default)
    {
        try
        {
            await session.CloseAsync(cancellationToken);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }
}
