using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UseCases.Sessions;
using LoipvRemote.Container;
using LoipvRemote.Messages;
using LoipvRemote.Properties;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Panels;
using LoipvRemote.UI.Tabs;
using LoipvRemote.UI.Window;
using LoipvRemote.Tools;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.Resources.Language;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UI;
using System.Runtime.Versioning;

namespace LoipvRemote.Connection
{
    [SupportedOSPlatform("windows")]
    public class ConnectionInitiator : IConnectionInitiator
    {
        private readonly PanelAdder _panelAdder;
        private readonly List<string> _activeConnections = [];
        private readonly SessionLifecycleCoordinator _sessionLifecycleCoordinator;
        private readonly ConnectionSessionOrchestrator _sessionOrchestrator;
        private readonly IProtocolFactory _protocolFactory;
        private readonly ExternalToolsService _externalToolsService;
        private readonly RuntimeState _runtimeState;
        private readonly MessageCollector _messageCollector;
        private readonly ConnectionWorkspaceAdapter _connectionWorkspace;
        private readonly Func<string, ConnectionInfo?> _connectionLookup;
        private readonly Func<ConnectionInfo, ExternalApplicationDefinition?>? _externalApplicationResolver;
        private readonly Func<string, string, string, string>? _protectSecret;
        private readonly Func<ConnectionInfo, bool, ExternalCredential?>? _externalCredentialResolver;

        public ConnectionInitiator(
            IProtocolFactory protocolFactory,
            ExternalToolsService externalToolsService,
            RuntimeState runtimeState,
            MessageCollector messageCollector,
            ConnectionWorkspaceAdapter connectionWorkspace,
            Func<string, ConnectionInfo?> connectionLookup,
            PanelAdder panelAdder,
            ConnectionSessionOrchestrator? sessionOrchestrator = null,
            SessionLifecycleCoordinator? sessionLifecycleCoordinator = null,
            Func<ConnectionInfo, ExternalApplicationDefinition?>? externalApplicationResolver = null,
            Func<string, string, string, string>? protectSecret = null,
            Func<ConnectionInfo, bool, ExternalCredential?>? externalCredentialResolver = null)
        {
            _protocolFactory = protocolFactory ?? throw new ArgumentNullException(nameof(protocolFactory));
            _externalToolsService = externalToolsService ?? throw new ArgumentNullException(nameof(externalToolsService));
            _runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
            _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
            _connectionWorkspace = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
            _connectionLookup = connectionLookup ?? throw new ArgumentNullException(nameof(connectionLookup));
            _panelAdder = panelAdder ?? throw new ArgumentNullException(nameof(panelAdder));
            _sessionLifecycleCoordinator = sessionLifecycleCoordinator ?? new SessionLifecycleCoordinator();
            _sessionOrchestrator = sessionOrchestrator ?? new ConnectionSessionOrchestrator(
                _protocolFactory,
                _sessionLifecycleCoordinator);
            _externalApplicationResolver = externalApplicationResolver;
            _protectSecret = protectSecret;
            _externalCredentialResolver = externalCredentialResolver;
        }

        public IEnumerable<string> ActiveConnections => _activeConnections;

        public bool SwitchToOpenConnection(ConnectionInfo connectionInfo)
        {
            InterfaceControl? interfaceControl = FindConnectionContainer(connectionInfo);
            if (interfaceControl == null) return false;
            ConnectionTab? connT = interfaceControl.FindForm() as ConnectionTab;
            connT?.Focus();
            ConnectionTab? findForm = interfaceControl.FindForm() as ConnectionTab;
            findForm?.Show(findForm.DockPanel);
            return true;
        }

        public void OpenConnection(
            ContainerInfo containerInfo,
            ConnectionInfo.Force force = ConnectionInfo.Force.None,
            ConnectionWindow? conForm = null)
        {
            if (containerInfo == null || containerInfo.Children.Count == 0)
                return;

            foreach (ConnectionInfo child in containerInfo.Children)
            {
                if (child is ContainerInfo childAsContainer)
                    OpenConnection(childAsContainer, force, conForm);
                else
                    OpenConnection(child, force, conForm);
            }
        }

        // async is necessary so UI can update while OpenConnection waits for tunnel connection to get ready in case of connection through SSH tunnel
        public async void OpenConnection(
            ConnectionInfo connectionInfo,
            ConnectionInfo.Force force = ConnectionInfo.Force.None,
            ConnectionWindow? conForm = null)
        {
            if (connectionInfo == null)
                return;

            // Reject a known-invalid connection before entering the asynchronous
            // setup path. This keeps the user-facing validation deterministic and
            // avoids converting it into a generic connection failure.
            if (string.IsNullOrEmpty(connectionInfo.EC2InstanceId) &&
                string.IsNullOrEmpty(connectionInfo.Hostname) &&
                !ProtocolPolicy.AllowsBlankHostname(connectionInfo.Protocol))
            {
                _messageCollector.AddMessage(MessageClass.ErrorMsg, Language.ConnectionOpenFailedNoHostname);
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(connectionInfo.EC2InstanceId))
                {
                    try
                    {
                        string host = await LoipvRemote.Connectors.AWS.EC2FetchDataService.GetEC2InstanceDataAsync("AWSAPI:" + connectionInfo.EC2InstanceId, connectionInfo.EC2Region);
                        if (!string.IsNullOrEmpty(host))
                            connectionInfo.Hostname = host;
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrEmpty(connectionInfo.Hostname))
                {
                    if (!ProtocolPolicy.AllowsBlankHostname(connectionInfo.Protocol))
                    {
                        _messageCollector.AddMessage(MessageClass.ErrorMsg, Language.ConnectionOpenFailedNoHostname);
                        return;
                    }

                    if (string.IsNullOrEmpty(connectionInfo.Name))
                    {
                        connectionInfo.Name = "localhost";
                    }
                }

                StartPreConnectionExternalApp(connectionInfo);

                if (!force.HasFlag(ConnectionInfo.Force.DoNotJump))
                {
                    if (SwitchToOpenConnection(connectionInfo))
                        return;
                }

                string? connectionPanel = SetConnectionPanel(connectionInfo, force);
                if (string.IsNullOrEmpty(connectionPanel)) return;
                ConnectionWindow connectionForm = SetConnectionForm(conForm, connectionPanel);
                Control? connectionContainer = null;

                // Handle connection through SSH tunnel:
                // in case of connection through SSH tunnel, connectionInfo gets cloned, so that modification of its name, hostname and port do not modify the original connection info
                // connectionInfoOriginal points to the original connection info in either case, for where its needed later on.
                ConnectionInfo connectionInfoOriginal = connectionInfo;
                ConnectionInfo? connectionInfoSshTunnel = null; // SSH tunnel connection info will be set if SSH tunnel connection is configured, can be found and connected.
                if (!string.IsNullOrEmpty(connectionInfoOriginal.SSHTunnelConnectionName))
                {
                    // Find the connection info specified as SSH tunnel in the connections tree
                    connectionInfoSshTunnel = _connectionLookup(connectionInfoOriginal.SSHTunnelConnectionName);
                    if (connectionInfoSshTunnel == null)
                    {
                        _messageCollector.AddMessage(MessageClass.WarningMsg, string.Format(Language.SshTunnelConfigProblem, connectionInfoOriginal.Name, connectionInfoOriginal.SSHTunnelConnectionName));
                        return;
                    }
                    _messageCollector.AddMessage(MessageClass.DebugMsg,
                        $"SSH Tunnel connection '{connectionInfoOriginal.SSHTunnelConnectionName}' configured for '{connectionInfoOriginal.Name}' found. Finding free local port for use as local tunnel port ...");
                    // determine a free local port to use as local tunnel port
                    System.Net.Sockets.TcpListener l = new(System.Net.IPAddress.Loopback, 0);
                    l.Start();
                    int localSshTunnelPort = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
                    l.Stop();
                    _messageCollector.AddMessage(MessageClass.DebugMsg,
                        $"{localSshTunnelPort} will be used as local tunnel port. Establishing SSH connection to '{connectionInfoSshTunnel.Hostname}' with additional tunnel options for target connection ...");

                    // clone SSH tunnel connection as tunnel options will be added to it, and those changes shall not be saved to the configuration
                    connectionInfoSshTunnel = connectionInfoSshTunnel.Clone();
                    connectionInfoSshTunnel.SSHOptions += " -L " + localSshTunnelPort + ":" + connectionInfoOriginal.Hostname + ":" + connectionInfoOriginal.Port;

                    // clone target connection info as its hostname will be changed to localhost and port to local tunnel port to establish connection through tunnel, and those changes shall not be saved to the configuration
                    connectionInfo = connectionInfoOriginal.Clone();
                    connectionInfo.Name += " via " + connectionInfoSshTunnel.Name;
                    connectionInfo.Hostname = "localhost";
                    connectionInfo.Port = localSshTunnelPort;

                    // connect the SSH connection to setup the tunnel
                    ConnectionInfo tunnelRuntimeInfo = EnrichRuntimeCredentials(connectionInfoSshTunnel);
                    ConnectionDefinition tunnelDefinition = ConnectionDefinitionMapper.ToDomain(
                        tunnelRuntimeInfo,
                        _protectSecret,
                        externalApplicationResolver: _externalApplicationResolver);
                    IProtocolSession tunnelSession = _protocolFactory.Create(tunnelDefinition);
                    if (tunnelSession is not IEmbeddedWindow)
                    {
                        _messageCollector.AddMessage(MessageClass.WarningMsg,
                            string.Format(Language.SshTunnelIsNotPutty, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                        tunnelSession.Dispose();
                        return;
                    }

                    ProtocolSessionBridge protocolSshTunnel = new ProtocolSessionBridge(tunnelDefinition, tunnelSession);

                    SetConnectionFormEventHandlers(protocolSshTunnel, connectionForm);
                    SetConnectionEventHandlers(protocolSshTunnel);
                    connectionContainer = SetConnectionContainer(connectionInfo, connectionForm);
                    BuildConnectionInterfaceController(connectionInfoSshTunnel, protocolSshTunnel, connectionContainer);
                    InterfaceControl tunnelControl = protocolSshTunnel.InterfaceControl
                        ?? throw new InvalidOperationException("SSH tunnel interface control was not attached.");
                    tunnelControl.OriginalInfo = connectionInfoSshTunnel;

                    SessionStartResult tunnelStartResult = _sessionLifecycleCoordinator.Start(protocolSshTunnel);
                    if (tunnelStartResult == SessionStartResult.InitializationFailed)
                    {
                        _messageCollector.AddMessage(MessageClass.WarningMsg,
                            string.Format(Language.SshTunnelNotInitialized, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                        return;
                    }

                    if (tunnelStartResult == SessionStartResult.ConnectionFailed)
                    {
                        _messageCollector.AddMessage(MessageClass.WarningMsg,
                            string.Format(Language.SshTunnelNotConnected, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                        return;
                    }

                    _messageCollector.AddMessage(MessageClass.DebugMsg,
                        "Putty started for SSH connection for tunnel. Waiting for local tunnel port to become available ...");

                    // wait until SSH tunnel connection is ready, by checking if local port can be connected to, but max 60 sec.
                    System.Net.Sockets.Socket testsock = new(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    while (stopwatch.ElapsedMilliseconds < 60000)
                    {
                        // confirm that SSH connection is still active
                        // works only if putty is connfigured to always close window on exit
                        // else, if connection attempt fails, window remains open and putty process remains running, and we cannot know that connection is already doomed
                        // in this case the timeout will expire and the log message below will be created
                        // awkward for user as he has already acknowledged the putty popup some seconds again when the below notification comes....
                        if (tunnelSession is not IEmbeddedWindow { IsAvailable: true })
                        {
                            protocolSshTunnel.Close();
                            _messageCollector.AddMessage(MessageClass.WarningMsg,
                                string.Format(Language.SshTunnelFailed, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                            return;
                        }

                        try
                        {
                            testsock.Connect(System.Net.IPAddress.Loopback, localSshTunnelPort);
                            testsock.Close();
                            break;
                        }
                        catch
                        {
                            await System.Threading.Tasks.Task.Delay(1000);
                        }
                    }

                    if (stopwatch.ElapsedMilliseconds >= 60000)
                    {
                        protocolSshTunnel.Close();
                        _messageCollector.AddMessage(MessageClass.WarningMsg,
                            string.Format(Language.SshTunnelPortNotReadyInTime, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                        return;
                    }

                    _messageCollector.AddMessage(MessageClass.DebugMsg,
                        "Local tunnel port is now available. Hiding putty display and setting up target connection via local tunnel port ...");

                    // hide the display of the SSH tunnel connection which has been shown until this time, such that password can be entered if required or errors be seen
                    // it stays invisible in the container however which will be reused for the actual connection and such that if the container is closed the SSH tunnel connection is closed as well
                    protocolSshTunnel.InterfaceControl.Hide();
                }

                ConnectionInfo runtimeInfo = EnrichRuntimeCredentials(connectionInfo);
                ConnectionDefinition definition = ConnectionDefinitionMapper.ToDomain(
                    runtimeInfo,
                    _protectSecret,
                    externalApplicationResolver: _externalApplicationResolver);
                IProtocolSession domainSession = _protocolFactory.Create(definition);
                ProtocolSessionBridge newProtocol = new ProtocolSessionBridge(definition, domainSession);
                SetConnectionFormEventHandlers(newProtocol, connectionForm);
                SetConnectionEventHandlers(newProtocol);
                // in case of connection through SSH tunnel the container is already defined and must be use, else it needs to be created here
                if (connectionContainer == null) connectionContainer = SetConnectionContainer(connectionInfo, connectionForm);
                BuildConnectionInterfaceController(connectionInfo, newProtocol, connectionContainer);
                // in case of connection through SSH tunnel the connectionInfo was modified but connectionInfoOriginal in all cases retains the original info
                // and is stored in interface control for further use
                InterfaceControl interfaceControl = newProtocol.InterfaceControl
                    ?? throw new InvalidOperationException("Protocol interface control was not attached.");
                interfaceControl.OriginalInfo = connectionInfoOriginal;
                // SSH tunnel connection is stored in Interface Control to be used in log messages etc
                interfaceControl.SSHTunnelInfo = connectionInfoSshTunnel;

                newProtocol.Force = force;

                ConnectionInfo originalRuntimeInfo = EnrichRuntimeCredentials(connectionInfoOriginal);
                definition = ConnectionDefinitionMapper.ToDomain(
                    originalRuntimeInfo,
                    _protectSecret,
                    externalApplicationResolver: _externalApplicationResolver);
                if (!_sessionOrchestrator.Start(definition, newProtocol).IsStarted)
                    return;

                connectionInfoOriginal.OpenConnections.Add(newProtocol);
                _activeConnections.Add(connectionInfo.ConstantID);
                _connectionWorkspace.Select(connectionInfo);
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace(Language.ConnectionOpenFailed, ex);
            }
        }

        // recursively traverse the tree to find ConnectionInfo of a specific name
        private ConnectionInfo? getSSHConnectionInfoByName(IEnumerable<ConnectionInfo> rootnodes, string SSHTunnelConnectionName)
        {
            ConnectionInfo? result = null;
            foreach (ConnectionInfo node in rootnodes)
            {
                if (node is ContainerInfo container)
                {
                    result = getSSHConnectionInfoByName(container.Children, SSHTunnelConnectionName);
                }
                else
                {
                    if (node.Name == SSHTunnelConnectionName && (node.Protocol == ProtocolKind.Ssh1 || node.Protocol == ProtocolKind.Ssh2)) result = node;
                }
                if (result != null) break;
            }
            return result;
        }

        #region Private
        private void StartPreConnectionExternalApp(ConnectionInfo connectionInfo)
        {
            if (connectionInfo.PreExtApp == "") return;
            ExternalTool extA = _externalToolsService.GetExtAppByName(connectionInfo.PreExtApp);
            extA?.Start(connectionInfo);
        }

        private ConnectionInfo EnrichRuntimeCredentials(ConnectionInfo connectionInfo)
        {
            if (_externalCredentialResolver is null)
                return connectionInfo;

            ExternalCredential? credential = _externalCredentialResolver(connectionInfo, false);
            ExternalCredential? gatewayCredential = _externalCredentialResolver(connectionInfo, true);
            if (credential is null && gatewayCredential is null)
                return connectionInfo;

            ConnectionInfo clone = connectionInfo.Clone();
            if (credential is not null)
            {
                clone.Username = credential.Username;
                clone.Password = credential.Password;
                clone.Domain = credential.Domain;
            }

            if (gatewayCredential is not null)
            {
                clone.RDGatewayUsername = gatewayCredential.Username;
                clone.RDGatewayPassword = gatewayCredential.Password;
                clone.RDGatewayDomain = gatewayCredential.Domain;
            }

            return clone;
        }

        private InterfaceControl? FindConnectionContainer(ConnectionInfo connectionInfo)
        {
            if (connectionInfo.OpenConnections.Count <= 0) return null;
            for (int i = 0; i <= WindowList.Count - 1; i++)
            {
                // the new structure is ConnectionWindow.Controls[0].ActiveDocument.Controls[0]
                //                                       DockPanel                  InterfaceControl
                if (!(WindowList[i] is ConnectionWindow connectionWindow)) continue;
                if (connectionWindow.Controls.Count < 1) continue;
                if (!(connectionWindow.Controls[0] is DockPanel cwDp)) continue;
                foreach (IDockContent dockContent in cwDp.Documents)
                {
                    if (dockContent is not ConnectionTab tab) continue;
                    InterfaceControl ic = InterfaceControl.FindInterfaceControl(tab);
                    if (ic == null) continue;
                    if (ic.Info == connectionInfo || ic.OriginalInfo == connectionInfo)
                        return ic;
                }
            }

            return null;
        }

        private string? SetConnectionPanel(ConnectionInfo connectionInfo, ConnectionInfo.Force force)
        {
            if (connectionInfo.Panel != "" && !force.HasFlag(ConnectionInfo.Force.OverridePanel) && !Properties.OptionsTabsPanelsPage.Default.AlwaysShowPanelSelectionDlg)
                return connectionInfo.Panel;

            FrmChoosePanel frmPnl = new(_panelAdder);
            return frmPnl.ShowDialog() == DialogResult.OK
                ? frmPnl.Panel
                : null;
        }

        private ConnectionWindow SetConnectionForm(ConnectionWindow? conForm, string connectionPanel)
        {
            ConnectionWindow? connectionForm = conForm ?? WindowList.FromString(connectionPanel) as ConnectionWindow;

            if (connectionForm == null)
                // Don't show the panel immediately - it will be shown when first tab is added
                connectionForm = _panelAdder.AddPanel(connectionPanel, showImmediately: false);
            else
                _connectionWorkspace.Show(connectionForm);

            connectionForm.AttachServices(
                _messageCollector,
                _externalToolsService,
                this,
                _connectionWorkspace);
            connectionForm.Focus();
            return connectionForm;
        }

        private Control SetConnectionContainer(ConnectionInfo connectionInfo, ConnectionWindow connectionForm)
        {
            Control connectionContainer = connectionForm.AddConnectionTab(connectionInfo);

            if (connectionInfo.Protocol != ProtocolKind.ExternalApplication) return connectionContainer;

            ExternalTool extT = _externalToolsService.GetExtAppByName(connectionInfo.ExtApp);

            if (extT == null) return connectionContainer;

            if (extT.Icon != null)
                ((ConnectionTab)connectionContainer).Icon = extT.Icon;

            return connectionContainer;
        }

        private static void SetConnectionFormEventHandlers(ProtocolSessionBridge newProtocol, Form connectionForm)
        {
            newProtocol.Closed += ((ConnectionWindow)connectionForm).Prot_Event_Closed;
            newProtocol.TitleChanged += ((ConnectionWindow)connectionForm).Prot_Event_TitleChanged;
        }

        private void SetConnectionEventHandlers(ProtocolSessionBridge newProtocol)
        {
            newProtocol.Disconnected += Prot_Event_Disconnected;
            newProtocol.Connected += Prot_Event_Connected;
            newProtocol.Closed += Prot_Event_Closed;
            newProtocol.ErrorOccured += Prot_Event_ErrorOccured;
        }

        private static void BuildConnectionInterfaceController(ConnectionInfo connectionInfo,
                                                               ProtocolSessionBridge newProtocol,
                                                               Control connectionContainer)
        {
            newProtocol.InterfaceControl = new InterfaceControl(connectionContainer, newProtocol, connectionInfo);
        }

        #endregion

        #region Event handlers

        private void Prot_Event_Disconnected(object sender, string disconnectedMessage, int? reasonCode)
        {
            try
            {
                ProtocolSessionBridge prot = (ProtocolSessionBridge)sender;
                if (prot.InterfaceControl is not { } control)
                    return;
                MessageClass msgClass = MessageClass.InformationMsg;

                if (control.Info.Protocol == ProtocolKind.Rdp)
                {
                    if (reasonCode > 3)
                    {
                        msgClass = MessageClass.WarningMsg;
                    }
                }

                string strHostname = control.OriginalInfo.Hostname;
                if (control.SSHTunnelInfo is not null)
                {
                    strHostname += " via SSH Tunnel " + control.SSHTunnelInfo.Name;
                }
                _messageCollector.AddMessage(msgClass,
                                                    string.Format(
                                                                  Language.ProtocolEventDisconnected,
                                                                  disconnectedMessage,
                                                                  strHostname,
                                                                  prot.InterfaceControl.Info.Protocol.ToString()));
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace(Language.ProtocolEventDisconnectFailed, ex);
            }
        }

        private void Prot_Event_Closed(object sender)
        {
            try
            {
                ProtocolSessionBridge prot = (ProtocolSessionBridge)sender;
                if (prot.InterfaceControl is not { } control)
                    return;
                _messageCollector.AddMessage(MessageClass.InformationMsg, Language.ConnenctionCloseEvent, true);
                string connDetail;
                if (control.OriginalInfo.Hostname == "" && control.Info.Protocol == ProtocolKind.ExternalApplication)
                    connDetail = control.Info.ExtApp;
                else if (control.OriginalInfo.Hostname != "")
                    connDetail = control.OriginalInfo.Hostname;
                else
                    connDetail = "UNKNOWN";

                _messageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.ConnenctionClosedByUser, connDetail, prot.InterfaceControl.Info.Protocol, Environment.UserName));
                control.OriginalInfo.OpenConnections.Remove(prot);
                if (_activeConnections.Contains(control.Info.ConstantID))
                    _activeConnections.Remove(control.Info.ConstantID);

                if (control.Info.PostExtApp == "") return;
                ExternalTool extA = _externalToolsService.GetExtAppByName(control.Info.PostExtApp);
                extA?.Start(control.OriginalInfo);
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace(Language.ConnenctionCloseEventFailed, ex);
            }
        }

        private WindowList WindowList => _runtimeState.WindowList
            ?? throw new InvalidOperationException("Connection windows must be initialized before opening a session.");

        private void Prot_Event_Connected(object sender)
        {
            ProtocolSessionBridge prot = (ProtocolSessionBridge)sender;
            if (prot.InterfaceControl is not { } control)
                return;
            _messageCollector.AddMessage(MessageClass.InformationMsg, Language.ConnectionEventConnected,
                                                true);
            _messageCollector.AddMessage(MessageClass.InformationMsg,
                                                string.Format(Language.ConnectionEventConnectedDetail,
                                                              control.OriginalInfo.Hostname,
                                                              control.Info.Protocol, Environment.UserName,
                                                              control.Info.Description,
                                                              control.Info.UserField));
        }

        private void Prot_Event_ErrorOccured(object sender, string errorMessage, int? errorCode)
        {
            try
            {
                ProtocolSessionBridge prot = (ProtocolSessionBridge)sender;
                if (prot.InterfaceControl is not { } control)
                    return;

                string msg = string.Format(
                                        Language.ConnectionEventErrorOccured,
                                        errorMessage,
                                        control.OriginalInfo.Hostname,
                                        errorCode?.ToString() ?? "-");
                _messageCollector.AddMessage(MessageClass.WarningMsg, msg);
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace(Language.ConnectionFailed, ex);
            }
        }

        #endregion
    }
}
