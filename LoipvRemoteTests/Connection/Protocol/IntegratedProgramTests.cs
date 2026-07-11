using LoipvRemote.App;
using LoipvRemote.Connection;
using LoipvRemote.Connection.Protocol;
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
        SetExternalToolList(_extTool);
        var sut = new IntegratedProgram();
        sut.InterfaceControl = BuildInterfaceControl("notepad", sut);
        sut.Initialize();
        var appStarted = sut.Connect();
        sut.Disconnect();
        Assert.That(appStarted);
    }

    [Test]
    public void ConnectingToExternalAppThatDoesntExistDoesNothing()
    {
        SetExternalToolList(_extTool);
        var sut = new IntegratedProgram();
        sut.InterfaceControl = BuildInterfaceControl("doesntExist", sut);
        var appInitialized = sut.Initialize();
        Assert.That(appInitialized, Is.False);
    }

    private void SetExternalToolList(ExternalTool externalTool)
    {
        Runtime.ExternalToolsService.ExternalTools = new FullyObservableCollection<ExternalTool> { externalTool };
    }

    private InterfaceControl BuildInterfaceControl(string extAppName, ProtocolBase sut)
    {
        var connectionWindow = new ConnectionWindow(new DockContent());
        var connectionInfo = new ConnectionInfo { ExtApp = extAppName, Protocol = ProtocolType.IntApp };
        return new InterfaceControl(connectionWindow, sut, connectionInfo);
    }
}