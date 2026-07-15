using System;
using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Connection;
using LoipvRemote.Messages;
using LoipvRemote.Config.Connections.Multiuser;
using LoipvRemote.Infrastructure.Persistence.Connectors;
using LoipvRemote.Properties;
using LoipvRemote.Security.SymmetricEncryption;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.Config.Settings.Registry;
using System.Windows.Forms;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.UI.Forms.OptionsPages
{
    [SupportedOSPlatform("windows")]
    public sealed partial class SqlServerPage
    {
        #region Private Fields
        private OptRegistrySqlServerPage pageRegSettingsInstance;
        private IConnectionTreeWorkspace? _connectionWorkspace;
        private IStringSecretStore? _userSecretStore;
        private MessageCollector? _messageCollector;
        private ConnectionLoadingService? _connectionLoadingService;
        #endregion

        public SqlServerPage()
        {
            InitializeComponent();
            ApplyTheme();
            PageIcon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.SQLDatabase_16x);
            pageRegSettingsInstance = new OptRegistrySqlServerPage(); // Initialize the field to avoid nullability issues
        }

        public void AttachRuntime(DesktopShellRuntime desktopShellRuntime)
        {
            ArgumentNullException.ThrowIfNull(desktopShellRuntime);
            _connectionWorkspace = desktopShellRuntime.ConnectionTreeWorkspace;
            _userSecretStore = desktopShellRuntime.UserSecretStore;
            _messageCollector = desktopShellRuntime.MessageCollector;
            _connectionLoadingService = desktopShellRuntime.ConnectionLoadingService;
        }

        public override string PageName
        {
            get => Language.SQLServer.TrimEnd(':');
            set { }
        }

        public override void ApplyLanguage()
        {
            base.ApplyLanguage();

            //lblExperimental.Text = Language.Experimental.ToUpper();
            //lblSQLInfo.Text = Language.SQLInfo;

            chkUseSQLServer.Text = Language.UseSQLServer;
            //lblSQLServer.Text = Language.Hostname;
            lblSQLDatabaseName.Text = Language.Database;
            lblSQLUsername.Text = Language.Username;
            lblSQLPassword.Text = Language.Password;
            lblSQLReadOnly.Text = Language.ReadOnly;
            btnTestConnection.Text = Language.TestConnection;

            lblSectionName.Text = Language.DatabaseConnectionManager;
            tabPage1.Text = Language.ServerAndCredentials;
            lblSQLAuthType.Text = Language.SqlAuthentication;
            lblSQLType.Text = Language.DatabasePlatform;
            tabPage2.Text = Language.ConnectionProperties;
            mrngLabel19.Text = Language.NetworkPacketSize;
            mrngLabel18.Text = Language.NetworkProtocol;
            mrngLabel1.Text = Language.ConnectionTimeoutSeconds;
            mrngLabel9.Text = Language.ExecutionTimeoutSeconds;
            tabPage3.Text = Language.TabSecurity;
            mrngLabel7.Text = Language.TrustServerCertificate;
            mrngLabel3.Text = Language.HostNameInCertificate;
            mrngLabel11.Text = Language.ProtocolLabel;
            mrngLabel12.Text = Language.EncryptionLabel;
            mrngLabel13.Text = Language.EnableAlwaysEncrypted;
            mrngLabel14.Text = Language.EnableSecureEnclaves;
            mrngLabel15.Text = Language.EnclaveAttestation;
            mrngLabel16.Text = Language.UrlLabel;
            mrngLabel17.Text = Language.EnableMARS;
            btnExpandOptions.Text = Language.AdvancedExpand;
            mrngLabel4.Text = Language.ServerNameOrIp;
            mrngLabel5.Text = Language.DatabaseName;
            mrngLabel6.Text = Language.UsernameLabel;

            lblRegistrySettingsUsedInfo.Text = Language.OptionsCompanyPolicyMessage;
        }

        public override void LoadSettings()
        {
            chkUseSQLServer.Checked = Properties.OptionsDBsPage.Default.UseSQLServer;
            txtSQLType.Text = Properties.OptionsDBsPage.Default.SQLServerType;
            txtSQLServer.Text = Properties.OptionsDBsPage.Default.SQLHost;
            txtSQLDatabaseName.Text = Properties.OptionsDBsPage.Default.SQLDatabaseName;
            txtSQLUsername.Text = Properties.OptionsDBsPage.Default.SQLUser;
            txtSQLPassword.Text = UserSecretStore.Unprotect(
                Properties.OptionsDBsPage.Default.SQLPass,
                SecretPurposes.SqlPassword);
            chkSQLReadOnly.Checked = Properties.OptionsDBsPage.Default.SQLReadOnly;
            lblTestConnectionResults.Text = "";
        }

        public override void SaveSettings()
        {
            base.SaveSettings();
            bool sqlServerWasPreviouslyEnabled = Properties.OptionsDBsPage.Default.UseSQLServer;

            Properties.OptionsDBsPage.Default.UseSQLServer = chkUseSQLServer.Checked;
            Properties.OptionsDBsPage.Default.SQLServerType = txtSQLType.Text;
            Properties.OptionsDBsPage.Default.SQLHost = txtSQLServer.Text;
            Properties.OptionsDBsPage.Default.SQLDatabaseName = txtSQLDatabaseName.Text;
            Properties.OptionsDBsPage.Default.SQLUser = txtSQLUsername.Text;
            Properties.OptionsDBsPage.Default.SQLPass = UserSecretStore.Protect(
                txtSQLPassword.Text,
                SecretPurposes.SqlPassword);
            Properties.OptionsDBsPage.Default.SQLReadOnly = chkSQLReadOnly.Checked;

            if (Properties.OptionsDBsPage.Default.UseSQLServer)
                ReinitializeSqlUpdater();
            else if (!Properties.OptionsDBsPage.Default.UseSQLServer && sqlServerWasPreviouslyEnabled)
                DisableSql();
        }

        public override void LoadRegistrySettings()
        {
            Type settingsType = typeof(OptRegistrySqlServerPage);
            RegistryLoader.RegistrySettings.TryGetValue(settingsType, out var settings);

            // Ensure settings is not null before assignment
            if (settings is OptRegistrySqlServerPage registrySettings)
            {
                pageRegSettingsInstance = registrySettings;
            }
            else
            {
                pageRegSettingsInstance = new OptRegistrySqlServerPage(); // Initialize with a new instance instead of null
                return; // Exit early if settings is null
            }

            RegistryLoader.Cleanup(settingsType);

            // Skip validation of SQL Server registry settings if not set in the registry.
            if (!pageRegSettingsInstance.UseSQLServer.IsSet)
                return;

            // Updates the visibility of the information label indicating whether registry settings are used.
            lblRegistrySettingsUsedInfo.Visible = true;
            DisableControl(chkUseSQLServer);

            // End validation of SQL Server registry settings if UseSQLServer is false.
            if (!Properties.OptionsDBsPage.Default.UseSQLServer)
                return;

            // ***
            // Disable controls based on the registry settings.
            //
            if (pageRegSettingsInstance.SQLServerType.IsSet)
                DisableControl(txtSQLType);

            if (pageRegSettingsInstance.SQLHost.IsSet)
                DisableControl(txtSQLServer);

            if (pageRegSettingsInstance.SQLDatabaseName.IsSet)
                DisableControl(txtSQLDatabaseName);

            if (pageRegSettingsInstance.SQLUser.IsSet)
                DisableControl(txtSQLUsername);

            if (pageRegSettingsInstance.SQLPassword.IsSet)
                DisableControl(txtSQLPassword);

            if (pageRegSettingsInstance.SQLReadOnly.IsSet)
                DisableControl(chkSQLReadOnly);
        }

        private void ReinitializeSqlUpdater()
        {
            if (_connectionWorkspace is null || _messageCollector is null)
                return;

            IConnectionTreeWorkspace workspace = _connectionWorkspace;
            workspace.RemoteConnectionsSyncronizer?.Dispose();
            workspace.RemoteConnectionsSyncronizer = new RemoteConnectionsSyncronizer(
                new ConnectionStoreUpdateChecker(workspace, _messageCollector),
                workspace);
            workspace.LoadConnections(true, false, "");
        }

        private void DisableSql()
        {
            if (_connectionWorkspace is null || _connectionLoadingService is null)
                return;

            _connectionWorkspace.RemoteConnectionsSyncronizer?.Dispose();
            _connectionWorkspace.RemoteConnectionsSyncronizer = null!;
            _connectionLoadingService.LoadConnections(true);
        }

        private IStringSecretStore UserSecretStore => _userSecretStore
            ?? PassthroughSecretStore.Instance;

        private sealed class PassthroughSecretStore : IStringSecretStore
        {
            public static PassthroughSecretStore Instance { get; } = new();
            public string Protect(string plaintext, string purpose) => plaintext;
            public string Unprotect(string protectedValue, string purpose) => protectedValue;
        }

        private void chkUseSQLServer_CheckedChanged(object sender, EventArgs e)
        {
            toggleSQLPageControls(chkUseSQLServer.Checked);
        }

        /// <summary>
        /// Enable or disable SQL connection page controls based on SQL server settings availability.
        /// Controls are enabled if corresponding registry settings are not set, allowing user interaction
        /// when SQL server usage is enabled.
        /// </summary>
        /// <param name="useSQLServer">Flag indicating whether SQL server functionality is enabled.</param>
        private void toggleSQLPageControls(bool useSQLServer)
        {
            if (!chkUseSQLServer.Enabled) return;
            pnlServerBlock.Visible = useSQLServer;
        }

        private void btnExpandOptions_Click(object sender, EventArgs e)
        {
            if (btnExpandOptions.Text == "Advanced >>")
            {
                btnExpandOptions.Text = "<< Simple";
                tabCtrlSQL.Visible = true;
            }
            else
            {
                btnExpandOptions.Text = "Advanced >>";
                tabCtrlSQL.Visible = false;
            }
        }

        private async void btnTestConnection_Click(object sender, EventArgs e)
        {
            string type = txtSQLType.Text;
            string server = txtSQLServer.Text;
            string database = txtSQLDatabaseName.Text;
            string username = txtSQLUsername.Text;
            string password = txtSQLPassword.Text;

            lblTestConnectionResults.Text = Language.TestingConnection;
            imgConnectionStatus.Image = Properties.Resources.Loading_Spinner;
            btnTestConnection.Enabled = false;

            // Replace the hardcoded connection string with the actual parameters
            string connectionString = $"Data Source={server};Initial Catalog={database};User ID={username};Password={password}";
            ConnectionTestResult connectionTestResult = await DatabaseConnectionTester.TestConnectivity(type, server, database, username, password);

            btnTestConnection.Enabled = true;

            switch (connectionTestResult)
            {
                case ConnectionTestResult.ConnectionSucceded:
                    UpdateConnectionImage(true);
                    lblTestConnectionResults.Text = Language.ConnectionSuccessful;
                    break;
                case ConnectionTestResult.ServerNotAccessible:
                    UpdateConnectionImage(false);
                    lblTestConnectionResults.Text =
                        BuildTestFailedMessage(string.Format(Language.ServerNotAccessible, server));
                    break;
                case ConnectionTestResult.CredentialsRejected:
                    UpdateConnectionImage(false);
                    lblTestConnectionResults.Text =
                        BuildTestFailedMessage(string.Format(Language.LoginFailedForUser, username));
                    break;
                case ConnectionTestResult.UnknownDatabase:
                    UpdateConnectionImage(false);
                    lblTestConnectionResults.Text =
                        BuildTestFailedMessage(string.Format(Language.DatabaseNotAvailable, database));
                    break;
                case ConnectionTestResult.UnknownError:
                    UpdateConnectionImage(false);
                    lblTestConnectionResults.Text = BuildTestFailedMessage(Language.RdpErrorUnknown);
                    break;
                default:
                    UpdateConnectionImage(false);
                    lblTestConnectionResults.Text = BuildTestFailedMessage(Language.RdpErrorUnknown);
                    break;
            }
        }

        private void UpdateConnectionImage(bool connectionSuccess)
        {
            imgConnectionStatus.Image = connectionSuccess ? Properties.Resources.Test_16x : Properties.Resources.LogError_16x;
        }

        private string BuildTestFailedMessage(string specificMessage)
        {
            return Language.ConnectionOpenFailed + Environment.NewLine + specificMessage;
        }

        private void txtSQLAuthType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Ensure SelectedItem is not null before accessing it
            if (txtSQLAuthType.SelectedItem != null)
            {
                // Get the selected value
                string? selectedValue = txtSQLAuthType.SelectedItem.ToString();

                // Check the selected value and call appropriate action
                if (selectedValue == "Windows Authentication")
                {
                    lblSQLUsername.Text = "User name:";
                    lblSQLUsername.Enabled = false;
                    txtSQLUsername.Enabled = false;
                    txtSQLUsername.Text = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    lblSQLPassword.Visible = false;
                    txtSQLPassword.Visible = false;
                }
                else if (selectedValue == "SQL Server Authentication")
                {
                    lblSQLUsername.Text = "login:";
                    lblSQLUsername.Enabled = true;
                    txtSQLUsername.Enabled = true;
                    txtSQLUsername.Text = "";
                    lblSQLPassword.Visible = true;
                    txtSQLPassword.Visible = true;
                }
                else if (selectedValue == "Microsoft Entra MFA")
                {
                    lblSQLUsername.Text = "User name:";
                    lblSQLUsername.Enabled = true;
                    txtSQLUsername.Enabled = true;
                    txtSQLUsername.Text = "";
                    lblSQLPassword.Visible = false;
                    txtSQLPassword.Visible = false;
                }
                else if (selectedValue == "Microsoft Entra Password")
                {
                    lblSQLUsername.Text = "User name:";
                    lblSQLUsername.Enabled = true;
                    txtSQLUsername.Enabled = true;
                    txtSQLUsername.Text = "";
                    lblSQLPassword.Visible = true;
                    txtSQLPassword.Visible = true;
                }
                else if (selectedValue == "Microsoft Entra Integrated")
                {
                    lblSQLUsername.Text = "User name:";
                    lblSQLUsername.Enabled = false;
                    txtSQLUsername.Enabled = false;
                    txtSQLUsername.Text = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    lblSQLPassword.Visible = false;
                    txtSQLPassword.Visible = false;
                }
                else if (selectedValue == "Microsoft Entra Service Principal")
                {
                    lblSQLUsername.Text = "User name:";
                    lblSQLUsername.Enabled = true;
                    txtSQLUsername.Enabled = true;
                    txtSQLUsername.Text = "";
                    lblSQLPassword.Visible = true;
                    txtSQLPassword.Visible = true;
                }
                else if (selectedValue == "Microsoft Entra Managed Identity")
                {
                    lblSQLUsername.Text = "User assigned identity:";
                    lblSQLUsername.Enabled = true;
                    txtSQLUsername.Enabled = true;
                    txtSQLUsername.Text = "";
                    lblSQLPassword.Visible = false;
                    txtSQLPassword.Visible = false;
                }
                else if (selectedValue == "Microsoft Entra Default")
                {
                    lblSQLUsername.Text = "User name:";
                    lblSQLUsername.Enabled = true;
                    txtSQLUsername.Enabled = true;
                    txtSQLUsername.Text = "";
                    lblSQLPassword.Visible = false;
                    txtSQLPassword.Visible = false;
                }
                else
                {
                    // Handle other values or do nothing
                    Console.WriteLine("No matching option.");
                }
            }
        }

    }
}
