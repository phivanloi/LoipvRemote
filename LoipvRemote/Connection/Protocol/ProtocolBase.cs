using LoipvRemote.Tools;
using System;
using System.Threading;
using System.Windows.Forms;
using LoipvRemote.UI.Tabs;
using LoipvRemote.UI.Forms;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Messages;
using LoipvRemote.Tools;
using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using System.Runtime.Versioning;

// ReSharper disable UnusedMember.Local

namespace LoipvRemote.Connection.Protocol
{
    [SupportedOSPlatform("windows")]
    public abstract class ProtocolBase : IProtocolSession
    {
        protected static IEmbeddedWindowOperations EmbeddedWindowOperations { get; } = new WindowsEmbeddedWindowOperations();

        #region Private Variables

        private ConnectionTab _connectionTab;
        private InterfaceControl _interfaceControl;
        private ConnectingEventHandler ConnectingEvent;
        private ConnectedEventHandler ConnectedEvent;
        private DisconnectedEventHandler DisconnectedEvent;
        private ErrorOccuredEventHandler ErrorOccuredEvent;
        private ClosingEventHandler ClosingEvent;
        private ClosedEventHandler ClosedEvent;
        private MessageCollector? _messageCollector;
        private FrmMain? _mainWindow;
        private ExternalToolsService? _externalToolsService;
        private IClipboardChangedSource? _clipboardChangedSource;

        #endregion

        #region Public Properties

        #region Control

        private string Name { get; }

        private ConnectionTab ConnectionTab
        {
            get => _connectionTab;
            set
            {
                _connectionTab = value;
                _connectionTab.ResizeBegin += ResizeBegin;
                _connectionTab.Resize += Resize;
                _connectionTab.ResizeEnd += ResizeEnd;
            }
        }

        public InterfaceControl InterfaceControl
        {
            get => _interfaceControl;
            set
            {
                _interfaceControl = value;

                if (_interfaceControl.Parent is ConnectionTab ct)
                    ConnectionTab = ct;
            }
        }

        protected Control Control { get; set; }

        #endregion

        public ConnectionInfo.Force Force { get; set; }

        public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;

        public virtual ProtocolCapabilities Capabilities => ProtocolCapabilities.None;

        protected readonly System.Timers.Timer tmrReconnect = new(5000);
        protected ReconnectGroup ReconnectGroup;

        protected ProtocolBase(string name)
        {
            Name = name;
        }

        protected ProtocolBase()
        {
        }

        public void AttachServices(
            MessageCollector messageCollector,
            FrmMain? mainWindow = null,
            ExternalToolsService? externalToolsService = null,
            IClipboardChangedSource? clipboardChangedSource = null)
        {
            _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
            _mainWindow = mainWindow;
            _externalToolsService = externalToolsService;
            _clipboardChangedSource = clipboardChangedSource;
        }

        /// <summary>
        /// Gets the session-scoped diagnostic sink. Protocol instances created outside the
        /// composition root (for capability probing) keep working without consulting the
        /// legacy global Runtime singleton.
        /// </summary>
        protected MessageCollector MessageCollector => _messageCollector ??= new MessageCollector();

        protected FrmMain MainWindow => _mainWindow
            ?? throw new InvalidOperationException("The protocol session must be attached to a desktop workspace before it is initialized.");

        protected FrmMain? AttachedMainWindow => _mainWindow;

        protected ExternalToolsService ExternalToolsService => _externalToolsService
            ?? throw new InvalidOperationException("The protocol session must be attached to external-tool services before it is initialized.");

        protected IClipboardChangedSource ClipboardChangedSource => _clipboardChangedSource
            ?? throw new InvalidOperationException("The protocol session must be attached to a clipboard notification source before it is initialized.");

        #endregion

        #region Methods

        //public abstract int GetDefaultPort();

        public virtual void Focus()
        {
            try
            {
                Control.Focus();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace("Couldn't focus Control (Connection.Protocol.Base)", ex);
            }
        }

        protected virtual void ResizeBegin(object sender, EventArgs e)
        {
        }

        protected virtual void Resize(object sender, EventArgs e)
        {
        }

        protected virtual void ResizeEnd(object sender, EventArgs e)
        {
        }

        public virtual bool Initialize()
        {
            try
            {
                _interfaceControl.Parent.Tag = _interfaceControl;
                _interfaceControl.Show();

                if (Control == null)
                {
                    State = ProtocolSessionState.Initialized;
                    return true;
                }


                Control.Name = Name;
                // Use Dock.Fill to respect padding (e.g., for connection frame color)
                Control.Dock = DockStyle.Fill;
                _interfaceControl.Controls.Add(Control);
                _interfaceControl.RemoteResourceBar?.BringToFront();

                State = ProtocolSessionState.Initialized;
                return true;
            }
            catch (Exception ex)
            {
                _messageCollector?.AddExceptionStackTrace("Couldn't SetProps (Connection.Protocol.Base)", ex);
                State = ProtocolSessionState.Faulted;
                return false;
            }
        }

        public virtual bool Connect()
        {
            if (InterfaceControl.Info.Protocol == ProtocolType.RDP) return false;
            if (ConnectedEvent == null) return false;
            Event_Connected(this);
            State = ProtocolSessionState.Connected;
            return true;
        }

        public virtual void Disconnect()
        {
            Close();
        }

        public virtual void Close()
        {
            State = ProtocolSessionState.Closing;
            Thread t = new(CloseBG);
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        private void CloseBG()
        {
            ClosedEvent?.Invoke(this);
            try
            {
                tmrReconnect.Enabled = false;

                if (Control != null)
                {
                    try
                    {
                        DisposeControl();
                    }
                    catch (Exception ex)
                    {
                        _messageCollector?.AddExceptionStackTrace(
                            "Couldn't dispose control, probably form is already closed (Connection.Protocol.Base)", ex);
                    }
                }

                if (_interfaceControl == null) return;

                try
                {
                    if (_interfaceControl.Parent == null) return;

                    if (_interfaceControl.Parent.Tag != null)
                    {
                        SetTagToNothing();
                    }

                    DisposeInterface();
                }
                catch (Exception ex)
                {
                    _messageCollector?.AddExceptionStackTrace(
                        "Couldn't set InterfaceControl.Parent.Tag or Dispose Interface, " +
                        "probably form is already closed (Connection.Protocol.Base)", ex);
                }
            }
            catch (Exception ex)
            {
                _messageCollector?.AddExceptionStackTrace(
                    "Couldn't Close InterfaceControl BG (Connection.Protocol.Base)", ex);
            }
            finally
            {
                State = ProtocolSessionState.Closed;
            }
        }

        /// <summary>
        /// Gives a protocol an opportunity to route shell messages to its embedded
        /// child window. The desktop shell must not know protocol-specific Win32
        /// details (for example PuTTY IME messages).
        /// </summary>
        public virtual bool TryForwardInputMessage(int message, IntPtr wParam, IntPtr lParam) => false;

        private delegate void DisposeInterfaceCB();

        private void DisposeInterface()
        {
            if (_interfaceControl.IsDisposed)
            {
                return;
            }
            if (_interfaceControl.InvokeRequired)
            {
                DisposeInterfaceCB s = new(DisposeInterface);
                _interfaceControl.Invoke(s);
            }
            else
            {
                _interfaceControl.Dispose();
            }
        }

        private delegate void SetTagToNothingCB();

        private void SetTagToNothing()
        {
            if (!_interfaceControl.IsAccessible || _interfaceControl.IsDisposed ||
                !_interfaceControl.Parent.IsAccessible || _interfaceControl.Parent.IsDisposed)
            { return; }

            if (_interfaceControl.Parent.InvokeRequired)
            {
                SetTagToNothingCB s = new(SetTagToNothing);
                _interfaceControl.Parent.Invoke(s);
            }
            else
            {
                _interfaceControl.Parent.Tag = null;
            }
        }

        private delegate void DisposeControlCB();

        private void DisposeControl()
        {
            // do not attempt to dispose the control if the control is already closed, closing or disposed
            if (Control == null || !Control.IsAccessible || Control.IsDisposed) { return; }

            if (Control.InvokeRequired)
            {
                DisposeControlCB s = new(DisposeControl);
                Control.Invoke(s);
            }
            else
            {
                Control.Dispose();
            }
        }

        #endregion

        #region Events

        public delegate void ConnectingEventHandler(object sender);

        public event ConnectingEventHandler Connecting
        {
            add => ConnectingEvent = (ConnectingEventHandler)Delegate.Combine(ConnectingEvent, value);
            remove => ConnectingEvent = (ConnectingEventHandler)Delegate.Remove(ConnectingEvent, value);
        }

        public delegate void ConnectedEventHandler(object sender);

        public event ConnectedEventHandler Connected
        {
            add => ConnectedEvent = (ConnectedEventHandler)Delegate.Combine(ConnectedEvent, value);
            remove => ConnectedEvent = (ConnectedEventHandler)Delegate.Remove(ConnectedEvent, value);
        }

        public delegate void DisconnectedEventHandler(object sender, string disconnectedMessage, int? reasonCode);

        public event DisconnectedEventHandler Disconnected
        {
            add => DisconnectedEvent = (DisconnectedEventHandler)Delegate.Combine(DisconnectedEvent, value);
            remove => DisconnectedEvent = (DisconnectedEventHandler)Delegate.Remove(DisconnectedEvent, value);
        }

        public delegate void ErrorOccuredEventHandler(object sender, string errorMessage, int? errorCode);

        public event ErrorOccuredEventHandler ErrorOccured
        {
            add => ErrorOccuredEvent = (ErrorOccuredEventHandler)Delegate.Combine(ErrorOccuredEvent, value);
            remove => ErrorOccuredEvent = (ErrorOccuredEventHandler)Delegate.Remove(ErrorOccuredEvent, value);
        }

        public delegate void ClosingEventHandler(object sender);

        public event ClosingEventHandler Closing
        {
            add => ClosingEvent = (ClosingEventHandler)Delegate.Combine(ClosingEvent, value);
            remove => ClosingEvent = (ClosingEventHandler)Delegate.Remove(ClosingEvent, value);
        }

        public delegate void ClosedEventHandler(object sender);

        public event ClosedEventHandler Closed
        {
            add => ClosedEvent = (ClosedEventHandler)Delegate.Combine(ClosedEvent, value);
            remove => ClosedEvent = (ClosedEventHandler)Delegate.Remove(ClosedEvent, value);
        }

        public delegate void TitleChangedEventHandler(object sender, string newTitle);

        private TitleChangedEventHandler TitleChangedEvent;

        public event TitleChangedEventHandler TitleChanged
        {
            add => TitleChangedEvent = (TitleChangedEventHandler)Delegate.Combine(TitleChangedEvent, value);
            remove => TitleChangedEvent = (TitleChangedEventHandler)Delegate.Remove(TitleChangedEvent, value);
        }

        public void Event_Closing(object sender)
        {
            ClosingEvent?.Invoke(sender);
        }

        protected void Event_Closed(object sender)
        {
            InterfaceControl?.RemoteResourceBar?.Stop();
            ClosedEvent?.Invoke(sender);
        }

        protected void Event_Connecting(object sender)
        {
            ConnectingEvent?.Invoke(sender);
        }

        protected void Event_Connected(object sender)
        {
            InterfaceControl?.RemoteResourceBar?.Start();
            ConnectedEvent?.Invoke(sender);
        }

        protected void Event_Disconnected(object sender, string disconnectedMessage, int? reasonCode)
        {
            DisconnectedEvent?.Invoke(sender, disconnectedMessage, reasonCode);
        }

        protected void Event_ErrorOccured(object sender, string errorMsg, int? errorCode)
        {
            ErrorOccuredEvent?.Invoke(sender, errorMsg, errorCode);
        }

        protected void Event_TitleChanged(object sender, string newTitle)
        {
            TitleChangedEvent?.Invoke(sender, newTitle);
        }

        protected void Event_ReconnectGroupCloseClicked()
        {
            Close();
        }

        #endregion

        private void Dispose(bool disposing)
        {
            if (disposing) return;
            tmrReconnect?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
