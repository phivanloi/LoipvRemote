using LoipvRemote.Connection;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Window;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.UI.Adapters;

/// <summary>Owns the main-window binding used by connection session UI operations.</summary>
public sealed class ConnectionWorkspaceAdapter : IClipboardChangedSource
{
    private FrmMain? _mainWindow;

    public event Action? ClipboardChanged;

    public void Attach(FrmMain mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        if (_mainWindow is not null)
        {
            if (!ReferenceEquals(_mainWindow, mainWindow))
                throw new InvalidOperationException("The connection workspace is already attached to a different main window.");

            return;
        }

        _mainWindow = mainWindow;
        FrmMain.ClipboardChanged += OnClipboardChanged;
    }

    public FrmMain MainWindow => GetMainWindow();

    public bool TryGetMainWindow(out FrmMain? mainWindow)
    {
        mainWindow = _mainWindow;
        return mainWindow is not null;
    }

    public void Show(ConnectionWindow connectionWindow)
    {
        ArgumentNullException.ThrowIfNull(connectionWindow);
        connectionWindow.Show(GetMainWindow().pnlDock);
    }

    public void Show(BaseWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.Show(GetMainWindow().pnlDock);
    }

    public void Select(ConnectionInfo connection) => GetMainWindow().SelectedConnection = connection;

    public void ShowError(string message, string caption) =>
        MessageBox.Show(GetMainWindow(), message, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public DialogResult ShowDialog(Form dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        return dialog.ShowDialog(GetMainWindow());
    }

    private FrmMain GetMainWindow() => _mainWindow
        ?? throw new InvalidOperationException("The connection workspace must be attached before opening a session.");

    private void OnClipboardChanged() => ClipboardChanged?.Invoke();
}
