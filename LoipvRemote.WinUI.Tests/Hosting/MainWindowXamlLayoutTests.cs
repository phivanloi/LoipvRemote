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

        XElement icon = document.Descendants(xaml + "Path")
            .Single(element => (string?)element.Attribute("Data") == "{Binding Content.IconGeometry}");
        XElement row = icon.Parent!;
        XElement label = row.Elements(xaml + "TextBlock").Single();
        XElement[] columns = row.Element(xaml + "Grid.ColumnDefinitions")!
            .Elements(xaml + "ColumnDefinition")
            .ToArray();
        XElement? iconTranslation = icon.Element(xaml + "Path.RenderTransform")?
            .Element(xaml + "TranslateTransform");

        Assert.Multiple(() =>
        {
            Assert.That(row.Name.LocalName, Is.EqualTo("Grid"));
            Assert.That((string?)row.Attribute("Height"), Is.EqualTo("20"));
            Assert.That((string?)row.Attribute("VerticalAlignment"), Is.EqualTo("Center"));
            Assert.That((string?)columns[0].Attribute("Width"), Is.EqualTo("14"));
            Assert.That((string?)icon.Attribute("Fill"), Is.EqualTo("{Binding Content.IconForeground}"));
            Assert.That((string?)icon.Attribute("Stretch"), Is.EqualTo("Uniform"));
            Assert.That((string?)icon.Attribute("HorizontalAlignment"), Is.EqualTo("Center"));
            Assert.That((string?)icon.Attribute("VerticalAlignment"), Is.EqualTo("Center"));
            Assert.That(icon.Attribute("Margin"), Is.Null);
            Assert.That(
                (string?)iconTranslation?.Attribute("Y"),
                Is.EqualTo("{Binding Content.IconVerticalOffset}"));
            Assert.That((string?)label.Attribute("Grid.Column"), Is.EqualTo("1"));
            Assert.That((string?)label.Attribute("Margin"), Is.EqualTo("6,0,0,0"));
            Assert.That((string?)label.Attribute("VerticalAlignment"), Is.EqualTo("Center"));
        });
    }

    [Test]
    public void SessionTabSelectionRefreshesEveryTabHeader()
    {
        string codePath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "MainWindow.xaml.cs"));
        string code = File.ReadAllText(codePath).ReplaceLineEndings("\n");

        Assert.Multiple(() =>
        {
            Assert.That(code, Does.Contain("private void UpdateSessionTabHeaderSelection()"));
            Assert.That(code, Does.Contain("header.UpdateSelection(ReferenceEquals(Sessions.SelectedItem, tab));"));
            Assert.That(code, Does.Contain("private void Sessions_SelectionChanged(object sender, SelectionChangedEventArgs args)\n    {\n        UpdateSessionTabHeaderSelection();"));
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
    public void ResourceBarSupportsBothSshAndRdpTabsAndSurfacesMonitorStatus()
    {
        string sourceRoot = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI"));
        XDocument document = XDocument.Load(Path.Combine(sourceRoot, "MainWindow.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement resourceBar = document.Descendants(xaml + "Border")
            .Single(element => (string?)element.Attribute(x + "Name") == "ResourceMonitorBar");
        string code = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs"));

        Assert.Multiple(() =>
        {
            Assert.That((string?)resourceBar.Attribute("Visibility"), Is.EqualTo("Collapsed"));
            Assert.That(code, Does.Contain("ProtocolKind.Ssh2 or ProtocolKind.Rdp"));
            Assert.That(code, Does.Contain("ToolTipService.SetToolTip(ResourceMonitorBar, monitor.LastStatus.Message)"));
            Assert.That(code, Does.Not.Contain("SshResourceMonitorBar"));
        });
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
