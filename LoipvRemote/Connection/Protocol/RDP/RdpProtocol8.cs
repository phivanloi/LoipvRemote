using System;
using System.Drawing;
using System.Windows.Forms;
using LoipvRemote.Messages;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using Microsoft.Win32;
using LoipvRemote.Infrastructure.Windows.Com;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.Connection.Protocol.RDP
{
    [SupportedOSPlatform("windows")]
    /* RDP v8 requires Windows 7 with:
		* https://support.microsoft.com/en-us/kb/2592687
		* OR
		* https://support.microsoft.com/en-us/kb/2923545
		*
		* Windows 8+ support RDP v8 out of the box.
		*/
    public class RdpProtocol8 : RdpProtocol7
    {

        protected override RdpVersion RdpProtocolVersion => global::LoipvRemote.Domain.Protocols.Rdp.RdpVersion.Rdc8;
        protected FormWindowState LastWindowState = FormWindowState.Minimized;

        // Debounce timer to reduce flickering during resize
        private System.Timers.Timer _resizeDebounceTimer;
        private Size _pendingResizeSize;
        private bool _hasPendingResize = false;

        public RdpProtocol8(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
            // Initialize debounce timer (300ms delay).
            // Keep this in the constructor because it doesn't root the instance in any
            // external static object – it's safe for the temporary probing instances
            // created by RdpProtocolFactory.
            _resizeDebounceTimer = new System.Timers.Timer(300);
            _resizeDebounceTimer.AutoReset = false;
            _resizeDebounceTimer.Elapsed += ResizeDebounceTimer_Elapsed;
        }

        public override bool Initialize()
        {
            if (!base.Initialize())
                return false;

            if (RdpVersion < Versions.RDC81) return false; // minimum dll version checked, loaded MSTSCLIB dll version is not capable

            // Subscribe to static/external events here (not in the constructor) so that
            // temporary probing instances created by RdpProtocolFactory.RdpVersionSupported()
            // are not rooted and do not accumulate memory leaks or spurious callbacks.
            MainWindow.ResizeEnd += ResizeEnd;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            // https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpextendedsettings-property
            if (connectionInfo.UseRestrictedAdmin)
            {
                SetExtendedProperty("RestrictedLogon", true);
            }
            else if (connectionInfo.UseRCG)
            {
                SetExtendedProperty("DisableCredentialsDelegation", true);
                SetExtendedProperty("RedirectedAuthentication", true);
            }

            return true;
        }

        public override bool Fullscreen
        {
            get => base.Fullscreen;
            protected set
            {
                base.Fullscreen = value;
                DoResizeClient();
            }
        }

        protected override void Resize(object sender, EventArgs e)
        {
            // Remember minimization so restoring to Normal is treated as a state
            // transition and resizes the RDP session again.
            if (MainWindow.WindowState == FormWindowState.Minimized)
            {
                LastWindowState = FormWindowState.Minimized;
                return;
            }

            MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resize() called - WindowState={MainWindow.WindowState}, LastWindowState={LastWindowState}");

            // Update control size during resize to keep UI synchronized
            // Actual RDP session resize is deferred to ResizeEnd() to prevent flickering
            DoResizeControl();

            // Only resize RDP session on window state changes (Maximize/Restore)
            // Manual drag-resizing will be handled by ResizeEnd()
            if (LastWindowState != MainWindow.WindowState)
            {
                MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize() - Window state changed from {LastWindowState} to {MainWindow.WindowState}, calling DoResizeClient()");
                LastWindowState = MainWindow.WindowState;
                DoResizeClient();
            }
            else
            {
                MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize() - Window state unchanged ({MainWindow.WindowState}), deferring to ResizeEnd()");
            }
        }

        protected override void ResizeEnd(object sender, EventArgs e)
        {
            // Skip resize when minimized
            if (MainWindow.WindowState == FormWindowState.Minimized) return;

            MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"ResizeEnd() called - WindowState={MainWindow.WindowState}");

            // Update window state tracking
            LastWindowState = MainWindow.WindowState;

            // Update control size immediately (no flicker)
            DoResizeControl();

            // Debounce the RDP session resize to reduce flickering
            ScheduleDebouncedResize();
        }

        private void ScheduleDebouncedResize()
        {
            if (InterfaceControl == null) return;

            // Store the pending size
            _pendingResizeSize = InterfaceControl.Size;
            _hasPendingResize = true;

            // Reset the timer (this delays the resize if called repeatedly)
            _resizeDebounceTimer?.Stop();
            _resizeDebounceTimer?.Start();

            MessageCollector?.AddMessage(MessageClass.DebugMsg,
                $"Resize debounced - will resize to {_pendingResizeSize.Width}x{_pendingResizeSize.Height} after 300ms");
        }

        private void ResizeDebounceTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_hasPendingResize) return;

            // Check if controls are still valid (not disposed during shutdown)
            if (Control == null || Control.IsDisposed || InterfaceControl == null || InterfaceControl.IsDisposed)
            {
                _hasPendingResize = false;
                return;
            }

            // Guard against the window handle not yet being created or already destroyed
            if (!InterfaceControl.IsHandleCreated)
            {
                _hasPendingResize = false;
                return;
            }

            _hasPendingResize = false;

            MessageCollector?.AddMessage(MessageClass.DebugMsg,
                $"Debounce timer fired - executing delayed resize to {_pendingResizeSize.Width}x{_pendingResizeSize.Height}");

            // Marshal to the UI thread because DoResizeClient() accesses WinForms and COM objects.
            // Wrap in try/catch: even after the guards above, there is a disposal race between
            // this timer thread and the UI thread that can cause ObjectDisposedException or
            // InvalidOperationException from BeginInvoke.
            try
            {
                if (InterfaceControl.InvokeRequired)
                {
                    InterfaceControl.BeginInvoke(new Action(DoResizeClient));
                }
                else
                {
                    DoResizeClient();
                }
            }
            catch (ObjectDisposedException ex)
            {
                MessageCollector?.AddMessage(MessageClass.DebugMsg,
                    $"ResizeDebounceTimer_Elapsed: control disposed during BeginInvoke ({ex.GetType().Name})");
            }
            catch (InvalidOperationException ex)
            {
                MessageCollector?.AddMessage(MessageClass.DebugMsg,
                    $"ResizeDebounceTimer_Elapsed: control handle unavailable during BeginInvoke ({ex.GetType().Name})");
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            // When display settings change (e.g., outer RDP session reconnects with a different
            // resolution/viewport), schedule a debounced resize so the inner RDP session is
            // updated to match the new panel dimensions once the display has settled.
            // SystemEvents.DisplaySettingsChanged can fire on a non-UI thread, so marshal
            // ScheduleDebouncedResize() back to the UI thread before touching UI state.
            if (!loginComplete) return;
            if (InterfaceControl == null || InterfaceControl.IsDisposed) return;

            if (InterfaceControl.InvokeRequired)
            {
                InterfaceControl.BeginInvoke(new Action(ScheduleDebouncedResize));
            }
            else
            {
                ScheduleDebouncedResize();
            }
        }

        private void DoResizeClient()
        {
            if (!loginComplete)
            {
                MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize skipped for '{connectionInfo.Hostname}': Login not complete");
                return;
            }

            if (!InterfaceControl.Info.AutomaticResize)
            {
                MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize skipped for '{connectionInfo.Hostname}': AutomaticResize is disabled");
                return;
            }

            // FitToWindow: fixed resolution set at connect time, scrollbars handle overflow.
            // SmartSize: SmartSizing scales the image client-side, no session resize needed.
            // Only Fullscreen benefits from dynamically changing the remote session resolution.
            if (InterfaceControl.Info.Resolution != RDPResolutions.Fullscreen)
            {
                MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize skipped for '{connectionInfo.Hostname}': Resolution is {InterfaceControl.Info.Resolution} (only Fullscreen supports dynamic resize)");
                return;
            }

            MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resizing RDP connection to host '{connectionInfo.Hostname}'");

            try
            {
                // Use InterfaceControl.Size instead of Control.Size because Control may be docked
                // and not reflect the actual available space
                Size size = Fullscreen
                    ? Screen.FromControl(Control).Bounds.Size
                    : InterfaceControl.Size;

                MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Calling UpdateSessionDisplaySettings({size.Width}, {size.Height}) for '{connectionInfo.Hostname}' (Control.Size={Control.Size}, InterfaceControl.Size={InterfaceControl.Size})");

                UpdateSessionDisplaySettings((uint)size.Width, (uint)size.Height);

                MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Successfully resized RDP session for '{connectionInfo.Hostname}' to {size.Width}x{size.Height}");
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage(
                    string.Format(Language.ChangeConnectionResolutionError, connectionInfo.Hostname),
                    ex, MessageClass.WarningMsg, false);
            }
        }

        private bool DoResizeControl()
        {
            if (Control == null || InterfaceControl == null) return false;

            // Check if controls are being disposed during shutdown
            if (Control.IsDisposed || InterfaceControl.IsDisposed) return false;

            // FitToWindow: control is undocked at a fixed size with scrollbars; don't touch it.
            if (InterfaceControl.Info.Resolution == RDPResolutions.FitToWindow)
                return false;

            MessageCollector?.AddMessage(MessageClass.DebugMsg,
                $"DoResizeControl - Before: Control.Size={Control.Size}, InterfaceControl.Size={InterfaceControl.Size}, Control.Dock={Control.Dock}");

            // If control is docked, we need to temporarily undock it, resize it, then redock it
            // because WinForms ignores Size assignments on docked controls
            bool wasDocked = Control.Dock == DockStyle.Fill;

            if (wasDocked)
            {
                Control.Dock = DockStyle.None;
            }

            Control.Location = InterfaceControl.Location;

            if (Control.Size == InterfaceControl.Size || InterfaceControl.Size == Size.Empty)
            {
                // Restore docking if we changed it
                if (wasDocked)
                {
                    Control.Dock = DockStyle.Fill;
                }

                MessageCollector?.AddMessage(MessageClass.DebugMsg,
                    $"DoResizeControl - Skipped: Sizes already match or InterfaceControl.Size is empty");
                return false;
            }

            Control.Size = InterfaceControl.Size;

            // Restore docking
            if (wasDocked)
            {
                Control.Dock = DockStyle.Fill;
            }

            MessageCollector?.AddMessage(MessageClass.DebugMsg,
                $"DoResizeControl - After: Control.Size={Control.Size}, Control.Dock={Control.Dock}");

            return true;
        }

        protected virtual void UpdateSessionDisplaySettings(uint width, uint height)
        {
            Runtime.ResizeSession(width, height, Orientation, DesktopScaleFactor, DeviceScaleFactor);
        }

        public override void Close()
        {
            // Unsubscribe from external/static events to prevent memory leaks
            AttachedMainWindow?.ResizeEnd -= ResizeEnd;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

            // Clean up debounce timer
            if (_resizeDebounceTimer != null)
            {
                _resizeDebounceTimer.Stop();
                _resizeDebounceTimer.Elapsed -= ResizeDebounceTimer_Elapsed;
                _resizeDebounceTimer.Dispose();
                _resizeDebounceTimer = null;
            }

            base.Close();
        }

    }
}
