namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Platform boundary for managing an embedded native window.</summary>
public interface IEmbeddedWindowOperations
{
    bool IsForegroundWindow(IntPtr windowHandle);
    IntPtr FindChildWindow(IntPtr parentHandle);
    bool HasClassName(IntPtr windowHandle, string className);
    void Hide(IntPtr windowHandle);
    void Restore(IntPtr windowHandle);
    void SetParent(IntPtr childHandle, IntPtr parentHandle);
    int GetWindowStyle(IntPtr windowHandle);
    bool TrySetWindowStyle(IntPtr windowHandle, int style);
    void RefreshFrame(IntPtr windowHandle);
    void ForwardInputLanguageChange(IntPtr windowHandle);
    void SendMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
    void Move(IntPtr windowHandle, int x, int y, int width, int height);
    void ShowSettingsDialog(IntPtr windowHandle, int commandId);
    void Activate(IntPtr windowHandle);
    void SetFocus(IntPtr windowHandle);
    bool TryFocus(IntPtr ownerWindowHandle, IntPtr embeddedWindowHandle);
    void Close(IntPtr windowHandle);
}
