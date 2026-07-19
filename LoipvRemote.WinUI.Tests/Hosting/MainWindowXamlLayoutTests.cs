using NUnit.Framework;
using System.Xml.Linq;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class MainWindowXamlLayoutTests
{
    [Test]
    public void QuickConnectImmediatelyConnectsTheOpenedSession()
    {
        string codePath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "MainWindow.xaml.cs"));
        string code = File.ReadAllText(codePath);

        const string expectedSequence = "ShowSession(tab);\n        QueueTitleBarInteractiveRegionUpdate();\n        await ConnectSessionAsync(tab);";
        Assert.That(code.ReplaceLineEndings("\n"), Does.Contain(expectedSequence));
    }

    [Test]
    public void ConnectionTreeIconAndLabelAreVisuallyCenteredTogether()
    {
        string xamlPath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "MainWindow.xaml"));
        XDocument document = XDocument.Load(xamlPath);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XElement icon = document.Descendants(xaml + "PathIcon")
            .Single(element => (string?)element.Attribute("Data") == "{Binding Content.IconGeometry}");
        XElement row = icon.Parent!;
        XElement label = row.Elements(xaml + "TextBlock").Single();

        Assert.Multiple(() =>
        {
            Assert.That((string?)row.Attribute("VerticalAlignment"), Is.EqualTo("Center"));
            Assert.That((string?)icon.Attribute("VerticalAlignment"), Is.EqualTo("Center"));
            Assert.That((string?)icon.Attribute("Margin"), Is.EqualTo("0,1,0,0"));
            Assert.That((string?)label.Attribute("VerticalAlignment"), Is.EqualTo("Center"));
        });
    }

    [Test]
    public void SessionViewportKeepsTheSameHeightAcrossSshAndRdpTabs()
    {
        string xamlPath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "MainWindow.xaml"));
        XDocument document = XDocument.Load(xamlPath);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement sessionSurface = document.Descendants(xaml + "Grid")
            .Single(element => (string?)element.Attribute(x + "Name") == "SessionSurface");
        XElement[] rows = sessionSurface.Element(xaml + "Grid.RowDefinitions")!
            .Elements(xaml + "RowDefinition")
            .ToArray();

        Assert.That((string?)rows[1].Attribute("Height"), Is.EqualTo("38"));
    }

    [Test]
    public void LowLevelInputHooksQueueUiWorkAndCaptureTheExactTab()
    {
        string codePath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "MainWindow.xaml.cs"));
        string code = File.ReadAllText(codePath);

        Assert.Multiple(() =>
        {
            Assert.That(code, Does.Contain("QueueSessionTabNavigation"));
            Assert.That(code, Does.Contain("ProcessQueuedSessionTabNavigation"));
            Assert.That(code, Does.Contain("RecoverSessionKeyboardFocus"));
            Assert.That(code, Does.Contain("Interlocked.Exchange(ref _pendingSessionTabNavigation"));
            Assert.That(code, Does.Contain("TryQueueCloseSessionTabAtClientPoint"));
            Assert.That(code, Does.Contain("CloseSessionTabAsync(Sessions, tab)"));
        });
    }
}
