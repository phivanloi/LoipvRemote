using LoipvRemote.UI.Adapters;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using LoipvRemote.UI.Tabs;
using LoipvRemote.UI.Controls;
using LoipvRemote.UI.Window;
using LoipvRemote.Desktop.Sessions;
using LoipvRemote.Connection.Monitoring;
using WeifenLuo.WinFormsUI.Docking;
using System.Runtime.Versioning;

namespace LoipvRemote.Connection
{
    [SupportedOSPlatform("windows")]
    public sealed partial class InterfaceControl : IDesktopSessionSurface
    {
        private bool _protocolFocusScheduled;
        private System.Windows.Forms.Timer? _protocolFocusRetryTimer;
        private int _protocolFocusRetryAttempt;
        private readonly PuttyResourceMonitorFactory? _monitorFactory;

        public ProtocolSessionBridge Protocol { get; set; } = null!;
        public ConnectionInfo Info { get; set; } = null!;
        // in case the connection is through a SSH tunnel the Info is a copy of original info with hostname and port number overwritten with localhost and local tunnel port
        // and the original Info is saved in the following variable
        public ConnectionInfo OriginalInfo { get; set; } = null!;
        // in case the connection is through a SSH tunnel the Info of the SSHTunnelConnection is also saved for reference in log messages etc.
        public ConnectionInfo? SSHTunnelInfo { get; set; }
        public RemoteResourceBar? RemoteResourceBar { get; }

        internal Rectangle RemoteContentBounds
        {
            get
            {
                Rectangle bounds = ClientRectangle;
                if (RemoteResourceBar?.Visible == true)
                    bounds.Height = Math.Max(0, bounds.Height - RemoteResourceBar.Height);
                return bounds;
            }
        }


        public InterfaceControl(Control parent, ProtocolSessionBridge protocol, ConnectionInfo info, PuttyResourceMonitorFactory? monitorFactory = null)
        {
            try
            {
                Protocol = protocol;
                Info = info;
                _monitorFactory = monitorFactory;
                Parent = parent;
                Location = new Point(0, 0);
                Size = parent.Size;
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                InitializeComponent();
                _protocolFocusRetryTimer = new System.Windows.Forms.Timer(components)
                {
                    Interval = ProtocolFocusRetryPolicy.IntervalMilliseconds
                };
                _protocolFocusRetryTimer.Tick += ProtocolFocusRetryTimerOnTick;
                ImeMode = ImeMode.On;


                if (Info.Protocol == ProtocolKind.Ssh2)
                {
                    if (_monitorFactory is not null)
                    {
                        RemoteResourceBar = new RemoteResourceBar(Info, _monitorFactory);
                        Controls.Add(RemoteResourceBar);
                        RemoteResourceBar.BringToFront();
                    }
                }

                // Enable custom painting for border
                this.Paint += InterfaceControl_Paint;

                // Set padding to prevent content from covering the frame border
                UpdatePaddingForFrameColor();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Couldn't create new InterfaceControl.{Environment.NewLine}{ex}");
            }
        }

        Rectangle IDesktopSessionSurface.ContentBounds => RemoteContentBounds;
        bool IDesktopSessionSurface.IsDisposed => IsDisposed;
        bool IDesktopSessionSurface.IsVisible => Visible;

        event EventHandler IDesktopSessionSurface.Resize
        {
            add => base.Resize += value;
            remove => base.Resize -= value;
        }

        void IDesktopSessionSurface.SetParentTag(object? value)
        {
            if (Parent is not null)
                Parent.Tag = value;
        }

        void IDesktopSessionSurface.ClearParentTag()
        {
            if (Parent?.Tag is InterfaceControl)
                Parent.Tag = null;
        }

        void IDesktopSessionSurface.ShowSurface() => Show();

        void IDesktopSessionSurface.StartActivity() => RemoteResourceBar?.Start();

        void IDesktopSessionSurface.StopActivity() => RemoteResourceBar?.Stop();

        void IDesktopSessionSurface.DisposeSurface()
        {
            if (!IsDisposed)
                Dispose();
        }


        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            ScheduleProtocolFocus();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            ScheduleProtocolFocus();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
                ScheduleProtocolFocus();
        }

        private void ScheduleProtocolFocus()
        {
            if (_protocolFocusScheduled || IsDisposed || !IsHandleCreated)
                return;

            if (_protocolFocusRetryTimer?.Enabled == true)
                return;

            _protocolFocusScheduled = true;
            BeginInvoke((MethodInvoker)(() =>
            {
                _protocolFocusScheduled = false;
                if (!IsActiveConnection())
                    return;

                _protocolFocusRetryAttempt = 0;
                Protocol?.Focus();
                _protocolFocusRetryAttempt++;
                _protocolFocusRetryTimer?.Start();
            }));
        }

        private void ProtocolFocusRetryTimerOnTick(object? sender, EventArgs e)
        {
            if (!ProtocolFocusRetryPolicy.ShouldAttempt(_protocolFocusRetryAttempt, IsActiveConnection()))
            {
                _protocolFocusRetryTimer?.Stop();
                return;
            }

            Protocol?.Focus();
            _protocolFocusRetryAttempt++;
        }

        private bool IsActiveConnection()
        {
            return !IsDisposed && Visible &&
                   FindForm() is ConnectionTab tab &&
                   ReferenceEquals(tab.DockPanel?.ActiveDocument, tab);
        }

        private void InterfaceControl_Paint(object? sender, PaintEventArgs e)
        {
            // Draw colored border based on ConnectionFrameColor property
            if (Info?.ConnectionFrameColor != null && Info.ConnectionFrameColor != ConnectionFrameColor.None)
            {
                Color frameColor = GetFrameColor(Info.ConnectionFrameColor);
                int borderWidth = 4; // 4 pixel border for visibility

                using (Pen pen = new Pen(frameColor, borderWidth))
                {
                    // Draw border inside the control bounds
                    Rectangle rect = new Rectangle(
                        borderWidth / 2,
                        borderWidth / 2,
                        this.Width - borderWidth,
                        this.Height - borderWidth
                    );
                    e.Graphics.DrawRectangle(pen, rect);
                }
            }
        }

        private void UpdatePaddingForFrameColor()
        {
            // Add padding to prevent content from covering the frame border
            if (Info?.ConnectionFrameColor != null && Info.ConnectionFrameColor != ConnectionFrameColor.None)
            {
                int borderWidth = 4; // Must match the border width in InterfaceControl_Paint
                // Add 2px margin so the border is fully visible and not covered by child controls
                int padding = borderWidth / 2 + 2;
                this.Padding = new Padding(padding);
            }
            else
            {
                this.Padding = new Padding(0);
            }
        }

        private static Color GetFrameColor(ConnectionFrameColor frameColor)
        {
            return frameColor switch
            {
                ConnectionFrameColor.Red => Color.FromArgb(220, 53, 69),      // Bootstrap danger red
                ConnectionFrameColor.Yellow => Color.FromArgb(255, 193, 7),   // Warning yellow
                ConnectionFrameColor.Green => Color.FromArgb(40, 167, 69),    // Success green
                ConnectionFrameColor.Blue => Color.FromArgb(0, 123, 255),     // Primary blue
                ConnectionFrameColor.Purple => Color.FromArgb(111, 66, 193),  // Purple
                _ => Color.Transparent
            };
        }

        protected override void WndProc(ref Message m)
        {
            if (Protocol.TryForwardInputMessage(m.Msg, m.WParam, m.LParam))
            {
                return;
            }

            base.WndProc(ref m);
        }

        public static InterfaceControl? FindInterfaceControl(DockPanel DockPnl)
        {
            if (DockPnl is null || DockPnl.IsDisposed)
                return null;

            // The root shell normally contains ConnectionWindow documents. Each
            // ConnectionWindow owns a nested DockPanel whose active document is
            // the actual ConnectionTab. Resolve both levels so WndProc never
            // forwards keyboard input to a stale/unknown session.
            try
            {
                if (DockPnl.ActiveDocument is ConnectionTab ct)
                    return FindInterfaceControl(ct);

                if (DockPnl.ActiveDocument is ConnectionWindow connectionWindow)
                    return connectionWindow.TryGetInterfaceControl();

                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Could not resolve an active interface control from DockPanel: {ex}");
                return null;
            }
        }

        public static InterfaceControl? FindInterfaceControl(ConnectionTab tab)
        {
            if (tab is null || tab.IsDisposed)
                return null;

            try
            {
                if (tab.Controls.Count < 1) return null;
                // If the tab has more than one control, the second control is
                // the protocol surface for an SSH tunnel connection.
                if (tab.Controls.Count > 1 && tab.Controls[1] is InterfaceControl ic1)
                    return ic1;

                return tab.Controls[0] as InterfaceControl;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Could not resolve an interface control from ConnectionTab: {ex}");
                return null;
            }

        }
    }
}
