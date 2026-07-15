using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Infrastructure.Windows.ProcessManagement;
using LoipvRemote.Messages;
using LoipvRemote.ApplicationServices.Configuration;
using LoipvRemote.Resources.Language;
using LoipvRemote.UseCases.Credentials;

// ReSharper disable ArrangeAccessorOwnerBody

namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class ExternalTool : INotifyPropertyChanged
    {
        private string _displayName = string.Empty; // Initialize to avoid CS8618
        private string _fileName = string.Empty; // Initialize to avoid CS8618
        private bool _waitForExit;
        private string _arguments = string.Empty; // Initialize to avoid CS8618
        private string _workingDir = string.Empty; // Initialize to avoid CS8618
        private bool _tryIntegrate;
        private bool _showOnToolbar = true;
        private bool _runElevated;
        private readonly IExternalToolRuntime? _runtime;

        #region Public Properties

        public string DisplayName
        {
            get => _displayName;
            set => SetField(ref _displayName, value, nameof(DisplayName));
        }

        public string FileName
        {
            get => _fileName;
            set => SetField(ref _fileName, value, nameof(FileName));
        }

        public bool WaitForExit
        {
            get => _waitForExit;
            set
            {
                // WaitForExit cannot be turned on when TryIntegrate is true
                if (TryIntegrate)
                    return;
                SetField(ref _waitForExit, value, nameof(WaitForExit));
            }
        }

        public string Arguments
        {
            get => _arguments;
            set => SetField(ref _arguments, value, nameof(Arguments));
        }

        public string WorkingDir
        {
            get => _workingDir;
            set => SetField(ref _workingDir, value, nameof(WorkingDir));
        }

        public bool TryIntegrate
        {
            get => _tryIntegrate;
            set
            {
                // WaitForExit cannot be turned on when TryIntegrate is true
                if (value)
                    WaitForExit = false;
                SetField(ref _tryIntegrate, value, nameof(TryIntegrate));
            }
        }

        public bool ShowOnToolbar
        {
            get => _showOnToolbar;
            set => SetField(ref _showOnToolbar, value, nameof(ShowOnToolbar));
        }

        public bool RunElevated
        {
            get => _runElevated;
            set => SetField(ref _runElevated, value, nameof(RunElevated));
        }

        public ConnectionInfo ConnectionInfo { get; set; } = new ConnectionInfo(); // Initialize to avoid CS8618

        public Icon Icon => File.Exists(FileName) ? MiscTools.GetIconFromFile(FileName) ?? Properties.Resources.LoipvRemote_Icon : Properties.Resources.LoipvRemote_Icon;

        public Image Image => Icon?.ToBitmap() ?? Properties.Resources.LoipvRemote_Icon.ToBitmap();

        #endregion

        public ExternalTool(string displayName = "",
                            string fileName = "",
                            string arguments = "",
                            string workingDir = "",
                            bool runElevated = false,
                            IExternalToolRuntime? runtime = null)
        {
            _runtime = runtime;
            DisplayName = displayName;
            FileName = fileName;
            Arguments = arguments;
            WorkingDir = workingDir;
            RunElevated = runElevated;
        }

        public void Start(ConnectionInfo startConnectionInfo = null!)
        {
            try
            {
                if (string.IsNullOrEmpty(FileName))
                {
                    RuntimeServices.MessageCollector.AddMessage(MessageClass.ErrorMsg, "ExternalApp.Start() failed: FileName cannot be blank.");
                    return;
                }

                ConnectionInfo = startConnectionInfo ?? new ConnectionInfo(); // Ensure ConnectionInfo is not null
                if (startConnectionInfo is ContainerInfo container)
                {
                    container.Children.ForEach(Start);
                    return;
                }

                if (TryIntegrate)
                    StartIntegrated();
                else
                    StartExternalProcess();
            }
            catch (Exception ex)
            {
                RuntimeServices.MessageCollector.AddExceptionMessage("ExternalApp.Start() failed.", ex);
            }
        }

        private void StartExternalProcess()
        {
            Process process = new();
            SetProcessProperties(process, ConnectionInfo);
            process.Start();

            if (WaitForExit)
            {
                process.WaitForExit();
            }
        }

        private void SetProcessProperties(Process process, ConnectionInfo startConnectionInfo)
        {
            ExternalApplicationDefinition definition = ToDefinition(startConnectionInfo);

            // Validate the executable path to prevent command injection
            PathValidator.ValidateExecutablePathOrThrow(definition.ExecutablePath, nameof(FileName));
            if (!string.IsNullOrWhiteSpace(definition.WorkingDirectory))
                PathValidator.ValidatePathOrThrow(definition.WorkingDirectory, nameof(WorkingDir));

            process.StartInfo = ExternalApplicationProcessStartInfoFactory.Create(definition);
        }

        /// <summary>Maps the configured tool to the domain launch definition.</summary>
        public ExternalApplicationDefinition ToDefinition(ConnectionInfo connectionInfo)
        {
            ArgumentNullException.ThrowIfNull(connectionInfo);
            ExternalApplicationArgumentParser argumentParser = new(BuildArgumentContext(connectionInfo));

            return new ExternalApplicationDefinition(
                DisplayName,
                argumentParser.ParseArguments(FileName),
                argumentParser.ParseArguments(Arguments),
                argumentParser.ParseArguments(WorkingDir),
                RunElevated,
                TryIntegrate,
                WaitForExit);
        }

    private ExternalApplicationArgumentContext BuildArgumentContext(ConnectionInfo connectionInfo)
        {
            string username = connectionInfo.Username;
            string password = connectionInfo.Password;
            string domain = connectionInfo.Domain;

            if (Properties.OptionsCredentialsPage.Default.EmptyCredentials == "windows")
            {
                username = string.IsNullOrEmpty(username) ? Environment.UserName : username;
                domain = string.IsNullOrEmpty(domain) ? Environment.UserDomainName : domain;
            }
            else if (Properties.OptionsCredentialsPage.Default.EmptyCredentials == "custom")
            {
                username = string.IsNullOrEmpty(username)
                    ? Properties.OptionsCredentialsPage.Default.DefaultUsername
                    : username;
                password = string.IsNullOrEmpty(password)
                    ? RuntimeServices.UserSecretStore.Unprotect(
                        Convert.ToString(Properties.OptionsCredentialsPage.Default.DefaultPassword),
                        SecretPurposes.DefaultCredentialPassword)
                    : password;
                domain = string.IsNullOrEmpty(domain)
                    ? Properties.OptionsCredentialsPage.Default.DefaultDomain
                    : domain;
            }

            return new ExternalApplicationArgumentContext(
                connectionInfo.Name,
                connectionInfo.Hostname,
                connectionInfo.Port,
                username,
                password,
                domain,
                connectionInfo.Description,
                connectionInfo.MacAddress,
                connectionInfo.UserField);
        }

        private void StartIntegrated()
        {
            try
            {
                ConnectionInfo newConnectionInfo = BuildConnectionInfoForIntegratedApp();
                RuntimeServices.ConnectionInitiator.OpenConnection(newConnectionInfo);
            }
            catch (Exception ex)
            {
                RuntimeServices.MessageCollector.AddExceptionMessage("ExternalApp.StartIntegrated() failed.", ex);
            }
        }

        private IExternalToolRuntime RuntimeServices => _runtime
            ?? throw new InvalidOperationException("ExternalTool must be created with an IExternalToolRuntime before it can start.");

        private ConnectionInfo BuildConnectionInfoForIntegratedApp()
        {
            ConnectionInfo newConnectionInfo = GetAppropriateInstanceOfConnectionInfo();
            SetConnectionInfoFields(newConnectionInfo);
            return newConnectionInfo;
        }

        private ConnectionInfo GetAppropriateInstanceOfConnectionInfo()
        {
            ConnectionInfo newConnectionInfo = ConnectionInfo == null ? new ConnectionInfo() : ConnectionInfo.Clone();
            return newConnectionInfo;
        }

        private void SetConnectionInfoFields(ConnectionInfo newConnectionInfo)
        {
            newConnectionInfo.Protocol = ProtocolKind.ExternalApplication;
            newConnectionInfo.ExtApp = DisplayName;
            newConnectionInfo.Name = DisplayName;
            newConnectionInfo.Panel = Language._Tools;
        }

        public event PropertyChangedEventHandler? PropertyChanged = delegate { }; // Updated to match nullability

        protected virtual void RaisePropertyChangedEvent(object sender, string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            RaisePropertyChangedEvent(this, propertyName);
            return true;
        }
    }
}
