using System;
using System.Runtime.Versioning;
using LoipvRemote.UI.Forms;

namespace LoipvRemote.App.Composition;

/// <summary>
/// Owns the currently running WinForms shell window without creating it as a
/// service-locator singleton. The host is built before the window exists, so
/// the reference is attached explicitly by the composition root.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MainWindowContext
{
    public FrmMain? Current { get; private set; }

    internal void Attach(FrmMain mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        if (Current is not null && !ReferenceEquals(Current, mainWindow))
            throw new InvalidOperationException("A different main window is already attached.");

        Current = mainWindow;
    }

    internal void Detach(FrmMain mainWindow)
    {
        if (ReferenceEquals(Current, mainWindow))
            Current = null;
    }
}
