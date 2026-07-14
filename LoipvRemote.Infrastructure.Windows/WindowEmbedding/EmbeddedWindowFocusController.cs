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
    private readonly object _syncRoot = new();

    public EmbeddedWindowFocusController(
        WindowThreadResolver getWindowThreadProcessId,
        Func<uint, uint, bool, bool> attachThreadInput,
        Func<IntPtr, IntPtr> setFocus)
    {
        _getWindowThreadProcessId = getWindowThreadProcessId ?? throw new ArgumentNullException(nameof(getWindowThreadProcessId));
        _attachThreadInput = attachThreadInput ?? throw new ArgumentNullException(nameof(attachThreadInput));
        _setFocus = setFocus ?? throw new ArgumentNullException(nameof(setFocus));
    }

    public bool TryFocus(IntPtr ownerWindowHandle, IntPtr embeddedWindowHandle)
    {
        if (ownerWindowHandle == IntPtr.Zero || embeddedWindowHandle == IntPtr.Zero)
            return false;

        lock (_syncRoot)
        {
            uint ownerThreadId = _getWindowThreadProcessId(ownerWindowHandle, out _);
            uint embeddedThreadId = _getWindowThreadProcessId(embeddedWindowHandle, out _);
            if (ownerThreadId == 0 || embeddedThreadId == 0)
                return false;

            if (ownerThreadId == embeddedThreadId)
            {
                _setFocus(embeddedWindowHandle);
                return true;
            }

            if (!_attachThreadInput(ownerThreadId, embeddedThreadId, true))
                return false;

            try
            {
                _setFocus(embeddedWindowHandle);
                return true;
            }
            finally
            {
                _attachThreadInput(ownerThreadId, embeddedThreadId, false);
            }
        }
    }

    public void Release(IntPtr embeddedWindowHandle) { }
}
