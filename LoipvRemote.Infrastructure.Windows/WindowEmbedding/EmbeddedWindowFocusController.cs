using System;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>
/// Transfers keyboard focus to an embedded window without leaving the desktop
/// and child-process input queues attached after the focus operation.
/// </summary>
public sealed class EmbeddedWindowFocusController
{
    public delegate uint WindowThreadResolver(IntPtr windowHandle, out uint processId);

    private readonly WindowThreadResolver _getWindowThreadProcessId;
    private readonly Func<uint, uint, bool, bool> _attachThreadInput;
    private readonly Func<IntPtr, IntPtr> _setFocus;
    private readonly Func<IntPtr> _getFocus;
    private readonly Func<IntPtr> _getForegroundWindow;
    private readonly Func<IntPtr, bool> _setForegroundWindow;
    private readonly object _syncRoot = new();

    public EmbeddedWindowFocusController(
        WindowThreadResolver getWindowThreadProcessId,
        Func<uint, uint, bool, bool> attachThreadInput,
        Func<IntPtr, IntPtr> setFocus,
        Func<IntPtr> getFocus,
        Func<IntPtr> getForegroundWindow,
        Func<IntPtr, bool> setForegroundWindow)
    {
        _getWindowThreadProcessId = getWindowThreadProcessId ?? throw new ArgumentNullException(nameof(getWindowThreadProcessId));
        _attachThreadInput = attachThreadInput ?? throw new ArgumentNullException(nameof(attachThreadInput));
        _setFocus = setFocus ?? throw new ArgumentNullException(nameof(setFocus));
        _getFocus = getFocus ?? throw new ArgumentNullException(nameof(getFocus));
        _getForegroundWindow = getForegroundWindow ?? throw new ArgumentNullException(nameof(getForegroundWindow));
        _setForegroundWindow = setForegroundWindow ?? throw new ArgumentNullException(nameof(setForegroundWindow));
    }

    public bool TryFocus(IntPtr ownerWindowHandle, IntPtr embeddedWindowHandle)
    {
        if (ownerWindowHandle == IntPtr.Zero || embeddedWindowHandle == IntPtr.Zero)
            return false;

        lock (_syncRoot)
        {
            if (_getForegroundWindow() != ownerWindowHandle &&
                !_setForegroundWindow(ownerWindowHandle) &&
                _getForegroundWindow() != ownerWindowHandle)
            {
                return false;
            }

            uint ownerThreadId = _getWindowThreadProcessId(ownerWindowHandle, out _);
            uint embeddedThreadId = _getWindowThreadProcessId(embeddedWindowHandle, out _);
            if (ownerThreadId == 0 || embeddedThreadId == 0)
                return false;

            if (ownerThreadId == embeddedThreadId)
            {
                _setFocus(embeddedWindowHandle);
                return _getFocus() == embeddedWindowHandle;
            }

            if (!_attachThreadInput(ownerThreadId, embeddedThreadId, true))
                return false;

            try
            {
                _setFocus(embeddedWindowHandle);
                return _getFocus() == embeddedWindowHandle;
            }
            finally
            {
                _attachThreadInput(ownerThreadId, embeddedThreadId, false);
            }
        }
    }

}
