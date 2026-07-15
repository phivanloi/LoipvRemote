using System.Drawing;

namespace LoipvRemote.Desktop.Sessions;

/// <summary>
/// WinForms-facing surface contract used by the desktop session host.
/// The protocol modules only receive an abstract session host and never see
/// the executable's controls or forms.
/// </summary>
public interface IDesktopSessionSurface
{
    IntPtr Handle { get; }
    bool IsDisposed { get; }
    bool IsVisible { get; }
    Rectangle ContentBounds { get; }

    event EventHandler Resize;

    void SetParentTag(object? value);
    void ClearParentTag();
    void ShowSurface();
    void StartActivity();
    void StopActivity();
    void DisposeSurface();
}
