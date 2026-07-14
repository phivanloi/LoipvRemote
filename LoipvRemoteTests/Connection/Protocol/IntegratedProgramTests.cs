using LoipvRemote.Connection;
using LoipvRemote.Connection.Protocol;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Messages;
using LoipvRemote.Protocols.ExternalApps;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Tools;
using LoipvRemote.Tools.CustomCollections;
using LoipvRemote.UI.Window;
using NUnit.Framework;
using WeifenLuo.WinFormsUI.Docking;

namespace LoipvRemoteTests.Connection.Protocol;

public class IntegratedProgramTests
{
    private readonly ExternalTool _extTool = new()
    {
        DisplayName = "notepad",
        FileName = @"%windir%\system32\notepad.exe",
        Arguments = "",
        TryIntegrate = true
    };


    [Test]
    public void CanStartExternalApp()
    {
        ExternalToolsService externalToolsService = CreateExternalToolsService(_extTool);
        var sut = new IntegratedProgram(new TestExternalApplicationHostFactory());
        sut.AttachServices(new MessageCollector(), externalToolsService: externalToolsService);
        sut.InterfaceControl = BuildInterfaceControl("notepad", sut);
        sut.Initialize();
        var appStarted = sut.Connect();
        sut.Disconnect();
        Assert.That(appStarted);
    }

    [Test]
    public void ConnectingToExternalAppThatDoesntExistDoesNothing()
    {
        ExternalToolsService externalToolsService = CreateExternalToolsService(_extTool);
        var sut = new IntegratedProgram(new TestExternalApplicationHostFactory());
        sut.AttachServices(new MessageCollector(), externalToolsService: externalToolsService);
        sut.InterfaceControl = BuildInterfaceControl("doesntExist", sut);
        var appInitialized = sut.Initialize();
        Assert.That(appInitialized, Is.False);
    }

    private static ExternalToolsService CreateExternalToolsService(ExternalTool externalTool)
    {
        return new ExternalToolsService
        {
            ExternalTools = new FullyObservableCollection<ExternalTool> { externalTool }
        };
    }

    private InterfaceControl BuildInterfaceControl(string extAppName, ProtocolBase sut)
    {
        var connectionWindow = new ConnectionWindow(new DockContent());
        var connectionInfo = new ConnectionInfo { ExtApp = extAppName, Protocol = ProtocolType.IntApp };
        return new InterfaceControl(connectionWindow, sut, connectionInfo);
    }

    private sealed class TestExternalApplicationHostFactory : IExternalApplicationHostFactory
    {
        public IExternalApplicationHost Create() => new TestExternalApplicationHost();
    }

    private sealed class TestExternalApplicationHost : IExternalApplicationHost
    {
        public bool IsRunning { get; private set; }
        public IntPtr WindowHandle => (IntPtr)1;
        public string WindowTitle => "test";
        public event EventHandler? Exited;

        public bool Start(ExternalApplicationDefinition definition)
        {
            IsRunning = definition.IsValid;
            return IsRunning;
        }

        public bool WaitForMainWindow(TimeSpan timeout) => IsRunning;
        public bool AttachTo(IntPtr parentWindowHandle) => IsRunning;
        public void Resize(EmbeddedWindowBounds bounds) { }
        public void Focus() { }

        public void Close()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            Exited?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Close();
    }
}
