using System.Collections.Immutable;
using System.Runtime.InteropServices;
using LoipvRemote.Protocols.Abstractions;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol.Implementation.MessageTypes.Outgoing;
using MarcusW.VncClient.Protocol.Implementation.Services.Transports;
using MarcusW.VncClient.Rendering;
using MarcusW.VncClient.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoipvRemote.Protocols.Vnc;

/// <summary>
/// Native child-window VNC renderer for the WinUI desktop. The RFB transport is
/// fully managed; this class owns only the Win32 presentation and input surface.
/// </summary>
public sealed class NativeVncClient : IVncClient, IAsyncVncClient, IManagedEmbeddedWindow, IDisposable
{
    private const int WsChild = unchecked((int)0x40000000);
    private const int WsVisible = unchecked((int)0x10000000);
    private const int WsClipChildren = 0x02000000;
    private const int WsClipSiblings = 0x04000000;
    private const int WmPaint = 0x000F;
    private const int WmSetFocus = 0x0007;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmChar = 0x0102;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;
    private const int WmMouseMove = 0x0200;
    private const int WmMouseWheel = 0x020A;
    private const int WmEraseBackground = 0x0014;
    private const int GwlpUserData = -21;
    private const int GwlpWindowProcedure = -4;
    private const uint DibRgbColors = 0;
    private const uint Srccopy = 0x00CC0020;
    private const byte VkBack = 0x08;
    private const byte VkTab = 0x09;
    private const byte VkReturn = 0x0D;
    private const byte VkShift = 0x10;
    private const byte VkControl = 0x11;
    private const byte VkMenu = 0x12;
    private const byte VkPause = 0x13;
    private const byte VkEscape = 0x1B;
    private const byte VkPrior = 0x21;
    private const byte VkNext = 0x22;
    private const byte VkEnd = 0x23;
    private const byte VkHome = 0x24;
    private const byte VkLeft = 0x25;
    private const byte VkUp = 0x26;
    private const byte VkRight = 0x27;
    private const byte VkDown = 0x28;
    private const byte VkInsert = 0x2D;
    private const byte VkDelete = 0x2E;
    private const byte VkLWin = 0x5B;
    private const byte VkRWin = 0x5C;
    private const byte VkF1 = 0x70;

    private static readonly WindowProcedureDelegate WindowProcedureDelegateInstance = WindowProcedure;
    private readonly object _framebufferLock = new();
    private readonly MarcusW.VncClient.VncClient _client = new(NullLoggerFactory.Instance);
    private RfbConnection? _connection;
    private Func<string>? _passwordProvider;
    private IntPtr _windowHandle;
    private IntPtr _parentWindowHandle;
    private IntPtr _previousWindowProcedure;
    private GCHandle _selfHandle;
    private IntPtr _framebufferAddress;
    private Size _framebufferSize;
    private bool _viewOnly;
    private bool _smartSize;
    private int _port = 5900;
    private MouseButtons _pressedButtons;
    private bool _disposed;

    public bool IsAvailable => _windowHandle != IntPtr.Zero && IsWindow(_windowHandle);
    public IntPtr WindowHandle => _windowHandle;

    public void SetPort(int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        _port = port;
    }

    public void SetPasswordProvider(Func<string>? passwordProvider) => _passwordProvider = passwordProvider;

    public void Connect(string host, bool viewOnly, bool smartSize) =>
        ConnectAsync(host, viewOnly, smartSize).AsTask().GetAwaiter().GetResult();

    public async ValueTask ConnectAsync(string host, bool viewOnly, bool smartSize, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (!IsAvailable)
            throw new InvalidOperationException("The VNC renderer must be attached to a WinUI session host before connecting.");
        if (_connection is not null)
            return;

        _viewOnly = viewOnly;
        _smartSize = smartSize;
        var parameters = new ConnectParameters
        {
            TransportParameters = new TcpTransportParameters { Host = host, Port = _port },
            AuthenticationHandler = new PasswordAuthenticationHandler(() => _passwordProvider?.Invoke() ?? string.Empty),
            InitialRenderTarget = new NativeRenderTarget(this),
            AllowSharedConnection = true
        };

        _connection = await _client.ConnectAsync(parameters, cancellationToken).ConfigureAwait(false);
    }

    public void Disconnect() => DisconnectAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        RfbConnection? connection = Interlocked.Exchange(ref _connection, null);
        if (connection is null)
            return;

        await connection.CloseAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        connection.Dispose();
    }

    public void Focus()
    {
        if (IsAvailable)
            _ = SetFocus(_windowHandle);
    }

    public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
    {
        _ = timeout;
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (parentWindowHandle == IntPtr.Zero)
            return false;

        if (_windowHandle != IntPtr.Zero)
        {
            if (_parentWindowHandle == parentWindowHandle)
                return true;

            _ = SetParent(_windowHandle, parentWindowHandle);
            _parentWindowHandle = GetParent(_windowHandle) == parentWindowHandle ? parentWindowHandle : IntPtr.Zero;
            return _parentWindowHandle != IntPtr.Zero;
        }

        IntPtr windowHandle = CreateWindowEx(
                0,
                "STATIC",
                string.Empty,
                WsChild | WsVisible | WsClipChildren | WsClipSiblings,
                0,
                0,
                1,
                1,
                parentWindowHandle,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);
        if (windowHandle == IntPtr.Zero)
            throw new InvalidOperationException($"Could not create the native VNC renderer window (Win32 error {Marshal.GetLastWin32Error()}).");

        _selfHandle = GCHandle.Alloc(this);
        _ = SetWindowLongPtr(windowHandle, GwlpUserData, GCHandle.ToIntPtr(_selfHandle));
        _previousWindowProcedure = SetWindowLongPtr(windowHandle, GwlpWindowProcedure, Marshal.GetFunctionPointerForDelegate(WindowProcedureDelegateInstance));
        if (_previousWindowProcedure == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
        {
            _selfHandle.Free();
            _ = DestroyWindow(windowHandle);
            throw new InvalidOperationException($"Could not subclass the native VNC renderer window (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        _windowHandle = windowHandle;
        _parentWindowHandle = parentWindowHandle;
        return true;
    }

    public void Resize(EmbeddedWindowBounds bounds)
    {
        if (IsAvailable && bounds.IsValid)
            _ = MoveWindow(_windowHandle, bounds.X, bounds.Y, bounds.Width, bounds.Height, true);
    }

    public void RefreshScreen()
    {
        RfbConnection? connection = _connection;
        if (connection is null || _framebufferSize.Width <= 0 || _framebufferSize.Height <= 0)
            return;

        _ = connection.EnqueueFramebufferUpdateRequest(new Rectangle(0, 0, _framebufferSize.Width, _framebufferSize.Height), false, CancellationToken.None);
    }

    public void SendSpecialKeys(VncSpecialKeys keys)
    {
        RfbConnection? connection = _connection;
        if (connection is null || _viewOnly)
            return;

        KeySymbol[] sequence = keys switch
        {
            VncSpecialKeys.CtrlAltDel => [KeySymbol.Control_L, KeySymbol.Alt_L, KeySymbol.Delete],
            VncSpecialKeys.CtrlEsc => [KeySymbol.Control_L, KeySymbol.Escape],
            _ => throw new ArgumentOutOfRangeException(nameof(keys), keys, null)
        };
        foreach (KeySymbol key in sequence)
            _ = connection.EnqueueMessage(new KeyEventMessage(true, key), CancellationToken.None);
        for (int index = sequence.Length - 1; index >= 0; index--)
            _ = connection.EnqueueMessage(new KeyEventMessage(false, sequence[index]), CancellationToken.None);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            Disconnect();
        }
        finally
        {
            lock (_framebufferLock)
            {
                if (_framebufferAddress != IntPtr.Zero)
                    Marshal.FreeHGlobal(_framebufferAddress);
                _framebufferAddress = IntPtr.Zero;
                _framebufferSize = default;
            }
            if (_windowHandle != IntPtr.Zero)
            {
                if (_previousWindowProcedure != IntPtr.Zero)
                    _ = SetWindowLongPtr(_windowHandle, GwlpWindowProcedure, _previousWindowProcedure);
                _ = SetWindowLongPtr(_windowHandle, GwlpUserData, IntPtr.Zero);
                _ = DestroyWindow(_windowHandle);
            }
            if (_selfHandle.IsAllocated)
                _selfHandle.Free();
            _windowHandle = IntPtr.Zero;
            _parentWindowHandle = IntPtr.Zero;
            _previousWindowProcedure = IntPtr.Zero;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private NativeFramebufferReference GrabFramebufferReference(Size size, IImmutableSet<Screen> layout)
    {
        _ = layout;
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "The remote framebuffer must have a positive size.");

        Monitor.Enter(_framebufferLock);
        try
        {
            if (_framebufferSize != size || _framebufferAddress == IntPtr.Zero)
            {
                if (_framebufferAddress != IntPtr.Zero)
                    Marshal.FreeHGlobal(_framebufferAddress);
                checked
                {
                    _framebufferAddress = Marshal.AllocHGlobal(size.Width * size.Height * 4);
                }
                _framebufferSize = size;
            }
            return new NativeFramebufferReference(_framebufferAddress, size, OnFramebufferRendered);
        }
        catch
        {
            Monitor.Exit(_framebufferLock);
            throw;
        }
    }

    private void OnFramebufferRendered()
    {
        try
        {
            if (IsAvailable)
                _ = InvalidateRect(_windowHandle, IntPtr.Zero, false);
        }
        finally
        {
            Monitor.Exit(_framebufferLock);
        }
    }

    private IntPtr ProcessWindowMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmEraseBackground:
                return new IntPtr(1);
            case WmPaint:
                Paint(windowHandle);
                return IntPtr.Zero;
            case WmSetFocus:
                return IntPtr.Zero;
            case WmKeyDown:
            case WmKeyUp:
                if (TryMapVirtualKey((byte)(long)wParam, out KeySymbol keySymbol) &&
                    (!IsCharacterVirtualKey((byte)(long)wParam) || IsModifierVirtualKey((byte)(long)wParam)))
                {
                    SendKey(message == WmKeyDown, keySymbol);
                    return IntPtr.Zero;
                }
                break;
            case WmChar:
                if (!IsControlPressed())
                {
                    char character = (char)(long)wParam;
                    SendKey(true, (KeySymbol)character);
                    SendKey(false, (KeySymbol)character);
                    return IntPtr.Zero;
                }
                break;
            case WmLButtonDown:
                _pressedButtons |= MouseButtons.Left;
                _ = SetCapture(windowHandle);
                SendPointer(lParam);
                return IntPtr.Zero;
            case WmLButtonUp:
                _pressedButtons &= ~MouseButtons.Left;
                SendPointer(lParam);
                ReleaseCapture();
                return IntPtr.Zero;
            case WmRButtonDown:
                _pressedButtons |= MouseButtons.Right;
                _ = SetCapture(windowHandle);
                SendPointer(lParam);
                return IntPtr.Zero;
            case WmRButtonUp:
                _pressedButtons &= ~MouseButtons.Right;
                SendPointer(lParam);
                ReleaseCapture();
                return IntPtr.Zero;
            case WmMButtonDown:
                _pressedButtons |= MouseButtons.Middle;
                _ = SetCapture(windowHandle);
                SendPointer(lParam);
                return IntPtr.Zero;
            case WmMButtonUp:
                _pressedButtons &= ~MouseButtons.Middle;
                SendPointer(lParam);
                ReleaseCapture();
                return IntPtr.Zero;
            case WmMouseMove:
                SendPointer(lParam);
                return IntPtr.Zero;
            case WmMouseWheel:
                Point point = new(GetSignedLowWord(lParam), GetSignedHighWord(lParam));
                _ = ScreenToClient(windowHandle, ref point);
                MouseButtons wheel = GetSignedHighWord(wParam) switch
                {
                    > 0 => MouseButtons.WheelUp,
                    < 0 => MouseButtons.WheelDown,
                    _ => MouseButtons.None
                };
                SendPointer(point.X, point.Y, _pressedButtons | wheel);
                SendPointer(point.X, point.Y, _pressedButtons);
                return IntPtr.Zero;
        }

        return DefWindowProc(windowHandle, message, wParam, lParam);
    }

    private void Paint(IntPtr windowHandle)
    {
        IntPtr deviceContext = BeginPaint(windowHandle, out PaintStruct paintStruct);
        try
        {
            lock (_framebufferLock)
            {
                if (_framebufferAddress == IntPtr.Zero || _framebufferSize.Width <= 0 || _framebufferSize.Height <= 0)
                    return;

                _ = GetClientRect(windowHandle, out Rect clientRect);
                int destinationWidth = _smartSize ? Math.Max(1, clientRect.Right - clientRect.Left) : _framebufferSize.Width;
                int destinationHeight = _smartSize ? Math.Max(1, clientRect.Bottom - clientRect.Top) : _framebufferSize.Height;
                var bitmapInfo = new BitmapInfo
                {
                    Header = new BitmapInfoHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        Width = _framebufferSize.Width,
                        Height = -_framebufferSize.Height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = 0
                    }
                };
                _ = StretchDIBits(
                    deviceContext,
                    0,
                    0,
                    destinationWidth,
                    destinationHeight,
                    0,
                    0,
                    _framebufferSize.Width,
                    _framebufferSize.Height,
                    _framebufferAddress,
                    ref bitmapInfo,
                    DibRgbColors,
                    Srccopy);
            }
        }
        finally
        {
            EndPaint(windowHandle, ref paintStruct);
        }
    }

    private void SendKey(bool down, KeySymbol key)
    {
        if (!_viewOnly)
            _ = _connection?.EnqueueMessage(new KeyEventMessage(down, key), CancellationToken.None);
    }

    private void SendPointer(IntPtr lParam) => SendPointer(GetSignedLowWord(lParam), GetSignedHighWord(lParam), _pressedButtons);

    private void SendPointer(int x, int y, MouseButtons buttons)
    {
        RfbConnection? connection = _connection;
        if (_viewOnly || connection is null)
            return;

        _ = GetClientRect(_windowHandle, out Rect clientRect);
        int clientWidth = Math.Max(1, clientRect.Right - clientRect.Left);
        int clientHeight = Math.Max(1, clientRect.Bottom - clientRect.Top);
        int remoteX = _smartSize ? x * _framebufferSize.Width / clientWidth : x;
        int remoteY = _smartSize ? y * _framebufferSize.Height / clientHeight : y;
        remoteX = Math.Clamp(remoteX, 0, Math.Max(0, _framebufferSize.Width - 1));
        remoteY = Math.Clamp(remoteY, 0, Math.Max(0, _framebufferSize.Height - 1));
        _ = connection.EnqueueMessage(new PointerEventMessage(new Position(remoteX, remoteY), buttons), CancellationToken.None);
    }

    private static bool TryMapVirtualKey(byte virtualKey, out KeySymbol symbol)
    {
        symbol = virtualKey switch
        {
            VkBack => KeySymbol.BackSpace,
            VkTab => KeySymbol.Tab,
            VkReturn => KeySymbol.Return,
            VkShift => KeySymbol.Shift_L,
            VkControl => KeySymbol.Control_L,
            VkMenu => KeySymbol.Alt_L,
            VkPause => KeySymbol.Pause,
            VkEscape => KeySymbol.Escape,
            VkPrior => KeySymbol.Page_Up,
            VkNext => KeySymbol.Page_Down,
            VkEnd => KeySymbol.End,
            VkHome => KeySymbol.Home,
            VkLeft => KeySymbol.Left,
            VkUp => KeySymbol.Up,
            VkRight => KeySymbol.Right,
            VkDown => KeySymbol.Down,
            VkInsert => KeySymbol.Insert,
            VkDelete => KeySymbol.Delete,
            VkLWin => KeySymbol.Super_L,
            VkRWin => KeySymbol.Super_R,
            >= VkF1 and <= VkF1 + 23 => (KeySymbol)((int)KeySymbol.F1 + virtualKey - VkF1),
            >= (byte)'0' and <= (byte)'9' => (KeySymbol)virtualKey,
            >= (byte)'A' and <= (byte)'Z' => (KeySymbol)virtualKey,
            _ => KeySymbol.Null
        };
        return symbol != KeySymbol.Null;
    }

    private static bool IsCharacterVirtualKey(byte virtualKey) =>
        virtualKey is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'Z';

    private static bool IsModifierVirtualKey(byte virtualKey) => virtualKey is VkShift or VkControl or VkMenu;
    private static bool IsControlPressed() => (GetKeyState(VkControl) & 0x8000) != 0;
    private static int GetSignedLowWord(IntPtr value) => unchecked((short)((long)value & 0xFFFF));
    private static int GetSignedHighWord(IntPtr value) => unchecked((short)(((long)value >> 16) & 0xFFFF));

    private static IntPtr WindowProcedure(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
    {
        IntPtr userData = GetWindowLongPtr(windowHandle, GwlpUserData);
        if (userData != IntPtr.Zero)
        {
            try
            {
                NativeVncClient owner = (NativeVncClient)GCHandle.FromIntPtr(userData).Target!;
                return owner.ProcessWindowMessage(windowHandle, message, wParam, lParam);
            }
            catch
            {
                NativeVncClient owner = (NativeVncClient)GCHandle.FromIntPtr(userData).Target!;
                return CallWindowProc(owner._previousWindowProcedure, windowHandle, message, wParam, lParam);
            }
        }
        return DefWindowProc(windowHandle, message, wParam, lParam);
    }

    private sealed class NativeRenderTarget(NativeVncClient owner) : IRenderTarget
    {
        public IFramebufferReference GrabFramebufferReference(Size size, IImmutableSet<Screen> layout) => owner.GrabFramebufferReference(size, layout);
    }

    private sealed class NativeFramebufferReference(IntPtr address, Size size, Action rendered) : IFramebufferReference
    {
        private Action? _rendered = rendered;
        public IntPtr Address => _rendered is null ? throw new ObjectDisposedException(nameof(NativeFramebufferReference)) : address;
        public Size Size => _rendered is null ? throw new ObjectDisposedException(nameof(NativeFramebufferReference)) : size;
        public PixelFormat Format => new("Win32 BGRA", 32, 24, false, true, false, 255, 255, 255, 0, 16, 8, 0, 0);
        public double HorizontalDpi => 96;
        public double VerticalDpi => 96;
        public void Dispose() => Interlocked.Exchange(ref _rendered, null)?.Invoke();
    }

    private sealed class PasswordAuthenticationHandler(Func<string> passwordProvider) : IAuthenticationHandler
    {
        public Task<TInput> ProvideAuthenticationInputAsync<TInput>(RfbConnection connection, MarcusW.VncClient.Protocol.SecurityTypes.ISecurityType securityType, IAuthenticationInputRequest<TInput> request)
            where TInput : class, IAuthenticationInput
        {
            _ = connection;
            _ = securityType;
            _ = request;
            if (typeof(TInput) == typeof(PasswordAuthenticationInput))
                return Task.FromResult((TInput)(IAuthenticationInput)new PasswordAuthenticationInput(passwordProvider()));
            throw new NotSupportedException($"VNC authentication input '{typeof(TInput).Name}' is not supported.");
        }
    }

    private delegate IntPtr WindowProcedureDelegate(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public IntPtr DeviceContext;
        public bool Erase;
        public Rect Paint;
        public bool Restore;
        public bool IncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[]? Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo { public BitmapInfoHeader Header; public uint Colors; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int extendedStyle, string className, string windowName, int style, int x, int y, int width, int height, IntPtr parentWindowHandle, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyWindow(IntPtr windowHandle);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr childWindowHandle, IntPtr parentWindowHandle);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetParent(IntPtr windowHandle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr windowHandle);
    [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr windowHandle);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool MoveWindow(IntPtr windowHandle, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool InvalidateRect(IntPtr windowHandle, IntPtr rectangle, bool erase);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr windowHandle, out PaintStruct paintStruct);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr windowHandle, ref PaintStruct paintStruct);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr windowHandle, out Rect rectangle);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr previousWindowProcedure, IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr windowHandle);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr windowHandle, ref Point point);
    [DllImport("user32.dll")] private static extern short GetKeyState(int virtualKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("gdi32.dll")] private static extern int StretchDIBits(IntPtr deviceContext, int xDest, int yDest, int destWidth, int destHeight, int xSrc, int ySrc, int sourceWidth, int sourceHeight, IntPtr bits, ref BitmapInfo bitmapInfo, uint usage, uint rasterOperation);
}
