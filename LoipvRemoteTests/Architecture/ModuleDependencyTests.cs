using System.Xml.Linq;
using System.Text.RegularExpressions;
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
    public void ExecutableRootUsesDomainProtocolKindWithoutLegacyProtocolBoundary()
    {
        string root = FindRepositoryRoot();
        string executableRoot = Path.Combine(root, "LoipvRemote.Desktop");
        string[] violations = Directory.EnumerateFiles(executableRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("bin", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.Combine("obj", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"(?<!Sockets\.)\bProtocolType\b|ProtocolKindBoundaryMapper|ISupportsViewOnly"))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Executable root must use Domain.ProtocolKind and Protocols.Abstractions directly.");
    }

    [Test]
    public void CompositionRootRoutesExternalApplicationsThroughTheirModuleFactory()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "LoipvRemote.Desktop",
            "Composition",
            "ProtocolServiceRegistration.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("ExternalApplicationProtocolFactory"));
            Assert.That(source, Does.Contain("BrowserProtocolFactory"));
            Assert.That(source, Does.Contain("RdpProtocolFactory"));
            Assert.That(source, Does.Contain("VncProtocolFactory"));
            Assert.That(source, Does.Contain("PuttyProtocolFactory"));
            Assert.That(source, Does.Contain("LocalProtocolFactory"));
            Assert.That(source, Does.Contain("ProtocolFactoryRouter"));
            Assert.That(source, Does.Not.Match("AddSingleton<IProtocolFactory>\\(provider => provider.GetRequiredService<ProtocolFactory>\\(\\)\\)"));
        });
    }

    [Test]
    public void ExecutableRegistrationDelegatesProtocolCompositionToDesktopHost()
    {
        string root = FindRepositoryRoot();
        string applicationRegistration = File.ReadAllText(Path.Combine(
            root,
            "LoipvRemote.Desktop",
            "App",
            "Composition",
            "ApplicationServiceRegistration.cs"));
        string desktopHostRegistration = File.ReadAllText(Path.Combine(
            root,
            "LoipvRemote.Desktop",
            "Composition",
            "DesktopHostServiceRegistration.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(applicationRegistration, Does.Not.Contain("ProtocolServiceRegistration.Register"));
            Assert.That(desktopHostRegistration, Does.Contain("ProtocolServiceRegistration.Register"));
        });
    }

    [Test]
    public void DesktopHostOwnsHostLifecycleRegistration()
    {
        string root = FindRepositoryRoot();
        string desktopRegistration = File.ReadAllText(Path.Combine(
            root,
            "LoipvRemote.Desktop",
            "Composition",
            "DesktopHostServiceRegistration.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(desktopRegistration, Does.Contain("SessionLifecycleCoordinator"));
            Assert.That(desktopRegistration, Does.Contain("SessionLifecycleShutdownService"));
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Desktop", "App", "Composition", "DesktopServiceRegistration.cs")), Is.False);
        });
    }

    [Test]
    public void PuttyResourceMonitoringPrimitivesLiveWithThePuttyProtocolModule()
    {
        string root = FindRepositoryRoot();
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Protocols.Putty", "Monitoring", "LinuxResourceProbe.cs")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Protocols.Putty", "Monitoring", "LinuxResourceSampleParser.cs")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Protocols.Putty", "Monitoring", "RemoteResourceSnapshot.cs")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Desktop", "Connection", "Monitoring", "LinuxResourceProbe.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Desktop", "Connection", "Monitoring", "LinuxResourceSampleParser.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Desktop", "Connection", "Monitoring", "RemoteResourceSnapshot.cs")), Is.False);
        });
    }

    [Test]
    public void PuttyMonitoringRuntimeDoesNotRemainInDesktopShell()
    {
        string root = FindRepositoryRoot();
        string desktopMonitoringRoot = Path.Combine(root, "LoipvRemote.Desktop", "Connection", "Monitoring");
        string puttyMonitoringRoot = Path.Combine(root, "LoipvRemote.Protocols.Putty", "Monitoring");

        string[] desktopSources = Directory.EnumerateFiles(desktopMonitoringRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(puttyMonitoringRoot, "PuttyResourceMonitor.cs")), Is.True);
            Assert.That(File.Exists(Path.Combine(puttyMonitoringRoot, "PuttyHostKeyTrustStore.cs")), Is.True);
            Assert.That(desktopSources.Any(source => source.Contains("using Renci.SshNet", StringComparison.Ordinal)), Is.False);
            Assert.That(desktopSources.Any(source => source.Contains("SshClient", StringComparison.Ordinal)), Is.False);
            Assert.That(File.Exists(Path.Combine(desktopMonitoringRoot, "PuttyHostKeyTrustStore.cs")), Is.False);
        });
    }

    [Test]
    public void DesktopDoesNotReferenceSshNetTransferImplementation()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText(Path.Combine(root, "LoipvRemote.Desktop", "LoipvRemote.Desktop.csproj"));
        string[] sourceFiles = Directory.EnumerateFiles(Path.Combine(root, "LoipvRemote.Desktop"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("bin", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.Combine("obj", string.Empty), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(project, Does.Not.Contain("SSH.NET"));
            Assert.That(sourceFiles.Select(File.ReadAllText).Any(source => source.Contains("Renci.SshNet", StringComparison.Ordinal)), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Protocols.Putty", "Transfers", "PuttyFileTransfer.cs")), Is.True);
        });
    }

    [Test]
    public void CompositionRootDoesNotPublishConcreteConnectionStoreService()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "LoipvRemote.Desktop",
            "App",
            "Composition",
            "ApplicationServiceRegistration.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("IConnectionTreeWorkspace"));
            Assert.That(source, Does.Not.Contain("IConnectionWorkspace"));
            Assert.That(source, Does.Not.Contain("AddSingleton<ConnectionsService>"));
        });
    }

    [TestCase("Tools", "ExternalTool.cs")]
    [TestCase("UI", "DialogFactory.cs")]
    public void DesktopAdaptersDoNotResolveRuntimeStaticServices(params string[] relativePath)
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine([root, "LoipvRemote.Desktop", .. relativePath]));

        Assert.That(source, Does.Not.Match("(?<!System\\.)\\bRuntime\\."));
    }

    [Test]
    public void DesktopCompositionReferencesConcreteProtocolModules()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText(Path.Combine(root, "LoipvRemote.Desktop", "LoipvRemote.Desktop.csproj"));
        string[] required = [
            "LoipvRemote.Protocols.Rdp",
            "LoipvRemote.Protocols.Vnc",
            "LoipvRemote.Protocols.Browser",
            "LoipvRemote.Protocols.Putty"
        ];

        Assert.That(required.All(project.Contains), Is.True);
    }

    [Test]
    public void DesktopCompositionReferencesExternalApplicationProtocolModule()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText(Path.Combine(root, "LoipvRemote.Desktop", "LoipvRemote.Desktop.csproj"));

        Assert.That(project, Does.Contain("LoipvRemote.Protocols.ExternalApps"));
    }

    [Test]
    public void ExecutableRootDoesNotOwnDatabaseConnectorImplementations()
    {
        string root = FindRepositoryRoot();
        string executableDatabaseRoot = Path.Combine(root, "LoipvRemote.Desktop", "Config", "DatabaseConnectors");
        string persistenceConnectorRoot = Path.Combine(root, "LoipvRemote.Infrastructure.Persistence", "Connectors");

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(executableDatabaseRoot), Is.False,
                "Database connector implementations must not remain in the WinForms executable.");
            Assert.That(File.Exists(Path.Combine(persistenceConnectorRoot, "DatabaseConnectorFactory.cs")), Is.True);
            Assert.That(File.Exists(Path.Combine(persistenceConnectorRoot, "IDatabaseConnector.cs")), Is.True);
        });
    }

    [Test]
    public void RepositoryDoesNotCarryReadTheDocsConfigurationWithoutDocumentationSources()
    {
        string root = FindRepositoryRoot();
        Assert.That(File.Exists(Path.Combine(root, ".readthedocs.yaml")), Is.False);
    }

    [Test]
    public void ExecutableRootUsesWinFormsWithoutWpfComposition()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText(Path.Combine(root, "LoipvRemote.Desktop", "LoipvRemote.Desktop.csproj"));
        string[] sourceFiles = Directory.EnumerateFiles(Path.Combine(root, "LoipvRemote.Desktop"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("bin", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.Combine("obj", string.Empty), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(project, Does.Not.Contain("UseWPF"));
            Assert.That(project, Does.Not.Contain("Microsoft.Xaml.Behaviors.Wpf"));
            Assert.That(project, Does.Not.Contain("ProjectTypeGuids"));
            Assert.That(sourceFiles.Select(File.ReadAllText)
                .Any(source => Regex.IsMatch(source, @"System\\.Windows\\.(?!Forms)|using System\\.Windows;")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Desktop", "UI", "Forms", "FrmSplashScreen.cs")), Is.True);
        });
    }

    [Test]
    public void ProtocolRouterHasNoLegacyFallback()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "LoipvRemote.Desktop",
            "Composition",
            "ProtocolFactoryRouter.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Contain("fallbackFactory"));
            Assert.That(source, Does.Not.Contain("_fallbackFactory"));
            Assert.That(source, Does.Contain("no registered protocol module"));
        });
    }

    [Test]
    public void ConnectionInitiatorHasNoVoidConnectionLifecycleEntryPoint()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "LoipvRemote.Desktop",
            "Connection",
            "ConnectionInitiator.cs"));
        string contract = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "LoipvRemote.Desktop",
            "Connection",
            "IConnectionInitiator.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Match(@"async\s+void\s+OpenConnection"));
            Assert.That(source, Does.Not.Match(@"void\s+OpenConnection\s*\("));
            Assert.That(contract, Does.Not.Contain("void OpenConnection"));
            Assert.That(contract, Does.Contain("Task OpenConnectionAsync"));
        });
    }

    [Test]
    public void CredentialConnectorsDoNotBlockOnAsyncOperations()
    {
        string root = FindRepositoryRoot();
        string connectorsRoot = Path.Combine(root, "LoipvRemote.Connectors");
        string[] sources = Directory.EnumerateFiles(connectorsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("bin", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.Combine("obj", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .ToArray();

        Assert.That(sources.Any(source => Regex.IsMatch(source, @"(?<!Message)\.Result\b|GetAwaiter\(\)\.GetResult")), Is.False,
            "Credential connector I/O must remain asynchronous and cancellation-aware.");
    }

    [TestCase("LoipvRemote.Desktop/Messages/WriterDecorators/MessageFocusDecorator.cs")]
    [TestCase("LoipvRemote.Desktop/Config/Connections/Multiuser/RemoteConnectionsSyncronizer.cs")]
    [TestCase("LoipvRemote.Desktop/UI/Adapters/ProtocolSessionBridge.cs")]
    public void AsyncLifecycleAdaptersDoNotUseAsyncVoid(string relativePath)
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

        Assert.That(source, Does.Not.Contain("async void"),
            $"Async lifecycle adapter '{relativePath}' must expose Task-based internal work.");
    }

    [Test]
    public void ConnectorsDoNotReferenceWinFormsOrOwnCredentialPromptForms()
    {
        string root = FindRepositoryRoot();
        string connectorsRoot = Path.Combine(root, "LoipvRemote.Connectors");
        string[] sources = Directory.EnumerateFiles(connectorsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("bin", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.Combine("obj", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .ToArray();
        string project = File.ReadAllText(Path.Combine(connectorsRoot, "LoipvRemote.Connectors.csproj"));

        Assert.Multiple(() =>
        {
            Assert.That(sources.Any(source => Regex.IsMatch(
                source,
                @"System\.Windows\.Forms|Microsoft\.Win32|\bRegistry\b|\bMessageBox\b|\b(Form|DialogResult)\b|ConnectionForm")), Is.False,
                "Connector runtime must not own UI or Windows infrastructure.");
            Assert.That(project, Does.Not.Contain("UseWindowsForms"));
            Assert.That(Directory.EnumerateFiles(connectorsRoot, "*ConnectionForm*", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.Combine("bin", string.Empty), StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains(Path.Combine("obj", string.Empty), StringComparison.OrdinalIgnoreCase)), Is.Empty);
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Desktop", "UI", "Adapters", "WinFormsExternalCredentialPrompt.cs")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Infrastructure.Windows", "Registry", "WindowsExternalCredentialSettingsStore.cs")), Is.True);
        });
    }

    [Test]
    public void RemovedLegacyXmlPersistencePipelineHasNoProductionArtifacts()
    {
        string root = FindRepositoryRoot();
        string[] removedFiles =
        [
            Path.Combine(root, "LoipvRemote.Desktop", "Config", "CredentialHarvester.cs"),
            Path.Combine(root, "LoipvRemote.Desktop", "Config", "Serializers", "XmlConnectionsDecryptor.cs"),
            Path.Combine(root, "LoipvRemote.Desktop", "Config", "Serializers", "ConnectionSerializers", "Xml", "XmlConnectionsSerializer.cs"),
        ];

        Assert.That(removedFiles.Where(File.Exists), Is.Empty);

        string[] productionSources = Directory.EnumerateFiles(
                Path.Combine(root, "LoipvRemote.Desktop"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("bin", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.Combine("obj", string.Empty), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.That(productionSources.Select(File.ReadAllText)
            .Any(source => source.Contains("XmlConnectionsSerializer", StringComparison.Ordinal)
                || source.Contains("XmlConnectionsDeserializer", StringComparison.Ordinal)
                || source.Contains("XmlConnectionNodeSerializer28", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void ConnectionWorkspace_UsesApplicationAndHostPortsInsteadOfWindowsImplementations()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "LoipvRemote.Desktop",
            "Connection",
            "ConnectionWorkspace.cs"));

        string[] forbiddenMarkers =
        [
            "DpapiStringSecretStore",
            "WindowsDpapiSecretProtector",
            "OptionsDBsPage",
            "SqlConnectionStringBuilder",
            "MySqlConnectionStringBuilder",
            "OdbcConnectionStringBuilder"
        ];

        Assert.Multiple(() =>
        {
            Assert.That(forbiddenMarkers.Where(source.Contains), Is.Empty);
            Assert.That(source, Does.Contain("ConnectionStoreConfigurationService"));
            Assert.That(source, Does.Contain("IConnectionStoreOptionsProvider"));
            Assert.That(source, Does.Contain("IStringSecretStore"));
        });
    }

    [Test]
    public void DesktopShellRuntime_DoesNotExposeTheConcreteDpapiStore()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "LoipvRemote.Desktop", "App", "Composition", "DesktopShellRuntime.cs"));

        Assert.That(source, Does.Not.Contain("DpapiStringSecretStore"));
        Assert.That(source, Does.Contain("IStringSecretStore UserSecretStore"));
    }

    [Test]
    public void RuntimeServiceLocatorBridgeHasBeenRemoved()
    {
        string root = FindRepositoryRoot();
        Assert.That(File.Exists(Path.Combine(root, "LoipvRemote.Desktop", "App", "Runtime.cs")), Is.False);

        string[] runtimeReferences = Directory.EnumerateFiles(Path.Combine(root, "LoipvRemote.Desktop"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("bin", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.Combine("obj", string.Empty), StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(item => Regex.IsMatch(item.line, "(?<!System\\.)(?<![A-Za-z0-9_])Runtime\\.[A-Z][A-Za-z0-9_]*"))
            .Select(item => Path.GetRelativePath(root, item.path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.That(runtimeReferences, Is.Empty, string.Join(Environment.NewLine, runtimeReferences));
    }

    [Test]
    public void RemoteConnectionSynchronizer_ReceivesItsReloadDependencyExplicitly()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "LoipvRemote.Desktop",
            "Config",
            "Connections",
            "Multiuser",
            "RemoteConnectionsSyncronizer.cs"));

        Assert.That(source, Does.Not.Match("(?<!System\\.)\\bRuntime\\."));
        Assert.That(source, Does.Contain("IConnectionTreeWorkspace workspace"));
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
    [TestCase("Tools", "Cmdline", "CmdArgumentsInterpreter.cs")]
    [TestCase("Tools", "ScanHost.cs")]
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
        string source = File.ReadAllText(Path.Combine([FindRepositoryRoot(), "LoipvRemote.Desktop", .. relativePath]));

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
