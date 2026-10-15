using Microsoft.UI.Xaml;

namespace LoipvRemote.WinUI.Sessions;

/// <summary>Session visual owned by the WinUI shell instead of a foreign HWND.</summary>
public interface IWinUIContentSession
{
    FrameworkElement View { get; }
    void Activate();
}
