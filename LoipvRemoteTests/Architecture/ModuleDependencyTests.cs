using System.Xml.Linq;
using NUnit.Framework;

namespace LoipvRemoteTests.Architecture;

[TestFixture]
public sealed class ModuleDependencyTests
{
    [Test]
    public void InfrastructureWindows_DoesNotReferenceProtocolImplementations()
    {
        string root = FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(
            root,
            "LoipvRemote.Infrastructure.Windows",
            "LoipvRemote.Infrastructure.Windows.csproj"));

        string[] references = project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();

        Assert.That(references.Any(reference => reference.Contains("LoipvRemote.Protocols.Rdp", StringComparison.OrdinalIgnoreCase)), Is.False);
        Assert.That(references.Any(reference => reference.Contains("LoipvRemote.Protocols.ExternalApps", StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    [Test]
    public void ConnectionStoreRuntime_UsesApplicationAndHostPortsInsteadOfWindowsImplementations()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "LoipvRemote",
            "App",
            "ConnectionStoreRuntime.cs"));

        string[] forbiddenMarkers =
        [
            "DpapiStringSecretStore",
            "WindowsDpapiSecretProtector",
            "OptionsDBsPage",
            "SqlConnectionStringBuilder",
            "MySqlConnectionStringBuilder",
            "OdbcConnectionStringBuilder"
        ];

        Assert.That(forbiddenMarkers.Where(source.Contains), Is.Empty);
    }

    [Test]
    public void Runtime_DoesNotExposeTheConcreteDpapiStore()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "LoipvRemote", "App", "Runtime.cs"));

        Assert.That(source, Does.Not.Contain("DpapiStringSecretStore"));
        Assert.That(source, Does.Contain("IStringSecretStore UserSecretStore"));
    }

    [Test]
    public void RemoteConnectionSynchronizer_ReceivesItsReloadDependencyExplicitly()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "LoipvRemote",
            "Config",
            "Connections",
            "Multiuser",
            "RemoteConnectionsSyncronizer.cs"));

        Assert.That(source, Does.Not.Match("(?<!System\\.)\\bRuntime\\."));
        Assert.That(source, Does.Contain("ConnectionsService connectionsService"));
    }

    [TestCase("Config", "Settings", "SettingsLoader.cs")]
    [TestCase("Config", "Settings", "SettingsSaver.cs")]
    [TestCase("Config", "Settings", "ExternalAppsLoader.cs")]
    [TestCase("Config", "Settings", "ExternalAppsSaver.cs")]
    [TestCase("Config", "Settings", "DockPanelLayoutSaver.cs")]
    [TestCase("Config", "Settings", "Registry", "RegistryLoader.cs")]
    [TestCase("App", "Shutdown.cs")]
    [TestCase("App", "Initialization", "CredsAndConsSetup.cs")]
    [TestCase("Config", "Connections", "Multiuser", "ConnectionStoreUpdateChecker.cs")]
    [TestCase("Config", "DataProviders", "FileBackupCreator.cs")]
    [TestCase("Config", "DataProviders", "FileDataProvider.cs")]
    [TestCase("Config", "Import", "LoipvRemoteXmlImporter.cs")]
    [TestCase("Config", "Import", "LoipvRemoteCsvImporter.cs")]
    [TestCase("Config", "Import", "SecureCRTImporter.cs")]
    [TestCase("Config", "Import", "RemoteDesktopManagerImporter.cs")]
    [TestCase("Config", "Import", "RegistryImporter.cs")]
    [TestCase("Config", "Import", "ActiveDirectoryImporter.cs")]
    [TestCase("Config", "Serializers", "MiscSerializers", "ActiveDirectoryDeserializer.cs")]
    [TestCase("Tools", "PortScanner.cs")]
    [TestCase("Connection", "ConnectionIcon.cs")]
    [TestCase("Connection", "ConnectionInfo.cs")]
    [TestCase("Connection", "DefaultConnectionInfo.cs")]
    [TestCase("Connection", "DefaultConnectionInheritance.cs")]
    [TestCase("Connection", "PuttySessionInfo.cs")]
    [TestCase("Tools", "ConnectionsTreeToMenuItemsConverter.cs")]
    [TestCase("UI", "Controls", "QuickConnectToolStrip.cs")]
    [TestCase("Tools", "NotificationAreaIcon.cs")]
    [TestCase("UI", "Forms", "OptionsPages", "AppearancePage.cs")]
    [TestCase("Config", "Serializers", "MiscSerializers", "SecureCRTFileDeserializer.cs")]
    [TestCase("Config", "Serializers", "MiscSerializers", "RemoteDesktopConnectionManagerDeserializer.cs")]
    [TestCase("Config", "Serializers", "ConnectionSerializers", "Xml", "XmlConnectionsDeserializer.cs")]
    [TestCase("Tools", "Cmdline", "CmdArgumentsInterpreter.cs")]
    [TestCase("Tools", "ScanHost.cs")]
    [TestCase("Tools", "SecureTransfer.cs")]
    [TestCase("Config", "Putty", "PuttySessionsRegistryProvider.cs")]
    [TestCase("Connection", "InterfaceControl.cs")]
    [TestCase("UI", "StatusImageList.cs")]
    [TestCase("UI", "Forms", "FrmAbout.cs")]
    [TestCase("UI", "Tabs", "TabHelper.cs")]
    [TestCase("UI", "Tabs", "ConnectionTab.cs")]
    [TestCase("UI", "Controls", "ConnectionInfoPropertyGrid", "ConnectionInfoPropertyGrid.cs")]
    [TestCase("UI", "Controls", "QuickConnectComboBox.cs")]
    [TestCase("UI", "Controls", "MultiSshToolStrip.cs")]
    [TestCase("UI", "Controls", "ExternalToolsToolStrip.cs")]
    [TestCase("Tools", "MiscTools.cs")]
    [TestCase("Themes", "ThemeManager.cs")]
    [TestCase("UI", "Forms", "OptionsPages", "AdvancedPage.cs")]
    [TestCase("UI", "Forms", "OptionsPages", "SecurityPage.cs")]
    [TestCase("UI", "Menu", "msMain", "FileMenu.cs")]
    [TestCase("UI", "Forms", "OptionsPages", "CredentialsPage.cs")]
    public void SettingsAndShutdownComponents_DoNotReachIntoRuntimeStatic(params string[] relativePath)
    {
        string source = File.ReadAllText(Path.Combine([FindRepositoryRoot(), "LoipvRemote", .. relativePath]));

        Assert.That(source, Does.Not.Match("(?<!System\\.)\\bRuntime\\."));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoipvRemote.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate LoipvRemote.slnx.");
    }
}
