using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LoipvRemote.Infrastructure.Windows.WindowEmbedding;

// These P/Invoke signatures mirror the Unicode Win32 text APIs, whose
// StringBuilder buffers are required by the native contract.
#pragma warning disable CA1838

namespace LoipvRemote.Infrastructure.Windows.ProcessManagement;

/// <summary>Controls a Windows process and selected child controls without UI dependencies.</summary>
public sealed class WindowsProcessController : IDisposable
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const uint WmSetText = 0x000C;
    private const uint WmGetText = 0x000D;
    private const uint WmCommand = 0x0111;
    private const uint LbSelectString = 0x018C;
    private const int LbErr = -1;

    private readonly System.Diagnostics.Process _process = new();
    private readonly TimeSpan _windowDiscoveryTimeout;
    private IReadOnlyList<IntPtr> _controls = [];
    private IntPtr _mainWindowHandle;

    public WindowsProcessController(TimeSpan windowDiscoveryTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(windowDiscoveryTimeout, TimeSpan.Zero);

        _windowDiscoveryTimeout = windowDiscoveryTimeout;
    }

    public bool Start(string fileName, string? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (fileName.Contains('\0'))
            throw new ArgumentException("The executable path contains a null character.", nameof(fileName));

        _process.StartInfo.UseShellExecute = false;
        _process.StartInfo.FileName = fileName;
        _process.StartInfo.Arguments = arguments ?? string.Empty;
        if (!_process.Start())
            return false;

        _controls = [];
        _mainWindowHandle = GetMainWindowHandle();
        return true;
    }

    public bool SetControlVisible(string className, string text, bool visible = true)
    {
        IntPtr controlHandle = GetControlHandle(className, text);
        return controlHandle != IntPtr.Zero && ShowWindow(controlHandle, visible ? SwShow : SwHide);
    }

    public bool SetControlText(string className, string oldText, string newText)
    {
        IntPtr controlHandle = GetControlHandle(className, oldText);
        return controlHandle != IntPtr.Zero &&
               SendMessage(controlHandle, WmSetText, IntPtr.Zero, new StringBuilder(newText)).ToInt32() != 0;
    }

    public bool SelectListBoxItem(string itemText)
    {
        IntPtr listBoxHandle = GetControlHandle("ListBox", string.Empty);
        return listBoxHandle != IntPtr.Zero &&
               SendMessage(listBoxHandle, LbSelectString, new IntPtr(-1), new StringBuilder(itemText)).ToInt32() != LbErr;
    }

    public bool ClickButton(string text)
    {
        IntPtr buttonHandle = GetControlHandle("Button", text);
        if (buttonHandle == IntPtr.Zero)
            return false;

        int buttonControlId = GetDlgCtrlID(buttonHandle);
        _ = SendMessage(_mainWindowHandle, WmCommand, new IntPtr(buttonControlId), buttonHandle);
        return true;
    }

    public void WaitForExit()
    {
        if (!_process.HasExited)
            _process.WaitForExit();
    }

    public void Dispose()
    {
        _process.Dispose();
        _controls = [];
        _mainWindowHandle = IntPtr.Zero;
    }

    private IntPtr GetMainWindowHandle()
    {
        if (_process.HasExited)
            return IntPtr.Zero;

        try
        {
            _process.WaitForInputIdle(_windowDiscoveryTimeout);
        }
        catch (InvalidOperationException)
        {
            return IntPtr.Zero;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!_process.HasExited && stopwatch.Elapsed < _windowDiscoveryTimeout)
        {
            _process.Refresh();
            if (_process.MainWindowHandle != IntPtr.Zero)
                return _process.MainWindowHandle;

            Thread.Sleep(15);
        }

        return IntPtr.Zero;
    }

    private IntPtr GetControlHandle(string className, string text)
    {
        if (_process.HasExited || _mainWindowHandle == IntPtr.Zero)
            return IntPtr.Zero;

        if (_controls.Count == 0)
            _controls = WindowsHandleEnumerator.EnumerateChildWindows(_mainWindowHandle);

        foreach (IntPtr control in _controls)
        {
            StringBuilder classNameBuilder = new(256);
            if (GetClassName(control, classNameBuilder, classNameBuilder.Capacity) == 0 ||
                !string.Equals(classNameBuilder.ToString(), className, StringComparison.Ordinal))
                continue;

            if (string.IsNullOrEmpty(text))
                return control;

            StringBuilder textBuilder = new(512);
            _ = SendMessage(control, WmGetText, new IntPtr(textBuilder.Capacity), textBuilder);
            if (string.Equals(textBuilder.ToString(), text, StringComparison.Ordinal))
                return control;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetDlgCtrlID(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr windowHandle, StringBuilder className, int maximumCount);
}
