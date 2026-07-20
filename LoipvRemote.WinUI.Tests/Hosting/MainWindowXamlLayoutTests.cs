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
    public void FailedSessionTabsHideThePreviouslyVisibleNativeSurface()
    {
        string codePath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "MainWindow.xaml.cs"));
        string code = File.ReadAllText(codePath).ReplaceLineEndings("\n");
        int methodStart = code.IndexOf("private void ShowSession(", StringComparison.Ordinal);
        int methodEnd = code.IndexOf("private void SubscribeResourceMonitor(", methodStart, StringComparison.Ordinal);
        string method = code[methodStart..methodEnd];
        int deactivate = method.IndexOf("SessionPresentationPolicy.ShouldDeactivateNativeSurface(tab.State)", StringComparison.Ordinal);
        int connectingBranch = method.IndexOf("if (tab.State == RemoteSessionTabState.Connecting)", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(deactivate, Is.GreaterThanOrEqualTo(0));
            Assert.That(method, Does.Contain("RemoteSessionWorkspace.Deactivate(_embeddedSessionSurface);"));
            Assert.That(deactivate, Is.LessThan(connectingBranch));
        });
    }

    [Test]
    public void ConcurrentConnectionCompletionPresentsTheTabThatIsStillSelected()
    {
        string codePath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "MainWindow.xaml.cs"));
        string code = File.ReadAllText(codePath).ReplaceLineEndings("\n");

        Assert.Multiple(() =>
        {
            Assert.That(code, Does.Contain("PresentSelectedSessionAfterConnectionCompleted(sessionTab);"));
            Assert.That(code, Does.Contain("private void PresentSelectedSessionAfterConnectionCompleted(RemoteSessionTab completedSession)"));
            Assert.That(code, Does.Contain("ShowSession(selectedSession, activateNativeSession);"));
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
    public void ResourceBarSupportsBothSshAndRdpTabsWithoutAnOverallStatusToolTip()
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
            Assert.That(code, Does.Not.Contain("ToolTipService.SetToolTip(ResourceMonitorBar"));
            Assert.That(code, Does.Contain("ResourceMonitorMemoryToolTip"));
            Assert.That(code, Does.Contain("ResourceMonitorDiskToolTip"));
            Assert.That(code, Does.Not.Contain("SshResourceMonitorBar"));
        });
    }

    [Test]
    public void ResourceBarShowsCompactPercentagesWithPersistentHoverRamAndDiskDetails()
    {
        string sourceRoot = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI"));
        XDocument document = XDocument.Load(Path.Combine(sourceRoot, "MainWindow.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement memoryChip = document.Descendants(xaml + "Border")
            .Single(element => (string?)element.Attribute(x + "Name") == "ResourceMonitorMemoryChip");
        XElement diskChip = document.Descendants(xaml + "Border")
            .Single(element => (string?)element.Attribute(x + "Name") == "ResourceMonitorDiskChip");
        XElement memoryToolTip = memoryChip.Descendants(xaml + "ToolTip").Single();
        XElement diskToolTip = diskChip.Descendants(xaml + "ToolTip").Single();
        string[] autoSizedChipNames =
        [
            "ResourceMonitorCpuChip",
            "ResourceMonitorMemoryChip",
            "ResourceMonitorDiskChip",
            "ResourceMonitorReceiveChip",
            "ResourceMonitorTransmitChip",
            "ResourceMonitorUptimeChip",
            "SshWorkingDirectoryButton",
        ];
        XElement[] autoSizedChips = document.Descendants()
            .Where(element => autoSizedChipNames.Contains((string?)element.Attribute(x + "Name")))
            .ToArray();
        string code = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(document.Descendants(xaml + "Button")
                .Any(element => (string?)element.Attribute(x + "Name") == "ResourceMonitorMemoryChip"), Is.False);
            Assert.That(document.Descendants(xaml + "Button")
                .Any(element => (string?)element.Attribute(x + "Name") == "ResourceMonitorDiskChip"), Is.False);
            Assert.That(memoryChip.Descendants(xaml + "Flyout"), Is.Empty);
            Assert.That(diskChip.Descendants(xaml + "Flyout"), Is.Empty);
            Assert.That((string?)memoryChip.Attribute("PointerEntered"), Is.EqualTo("ResourceMonitorDetailChip_PointerEntered"));
            Assert.That((string?)memoryChip.Attribute("PointerExited"), Is.EqualTo("ResourceMonitorDetailChip_PointerExited"));
            Assert.That((string?)diskChip.Attribute("PointerEntered"), Is.EqualTo("ResourceMonitorDetailChip_PointerEntered"));
            Assert.That((string?)diskChip.Attribute("PointerExited"), Is.EqualTo("ResourceMonitorDetailChip_PointerExited"));
            Assert.That((string?)memoryToolTip.Attribute(x + "Name"), Is.EqualTo("ResourceMonitorMemoryToolTip"));
            Assert.That((string?)diskToolTip.Attribute(x + "Name"), Is.EqualTo("ResourceMonitorDiskToolTip"));
            Assert.That(autoSizedChips, Has.Length.EqualTo(autoSizedChipNames.Length));
            Assert.That(autoSizedChips.Attributes("MinWidth"), Is.Empty);
            Assert.That(autoSizedChips.Attributes("MaxWidth"), Is.Empty);
            Assert.That(code, Does.Contain("ResourceMonitorPresentation.FormatMemoryPercent"));
            Assert.That(code, Does.Contain("ResourceMonitorPresentation.FormatMemoryDetails"));
            Assert.That(code, Does.Contain("ResourceMonitorPresentation.FormatDiskDetails"));
            Assert.That(code, Does.Contain("ApplyResourceWarning"));
            Assert.That(code, Does.Contain("toolTip.IsOpen = true"));
            Assert.That(code, Does.Contain("toolTip.IsOpen = false"));
        });
    }

    [Test]
    public void SshWorkingDirectoryChipOpensTheSftpBrowser()
    {
        string sourceRoot = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI"));
        XDocument document = XDocument.Load(Path.Combine(sourceRoot, "MainWindow.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement button = document.Descendants(xaml + "Button")
            .Single(element => (string?)element.Attribute(x + "Name") == "SshWorkingDirectoryButton");
        XElement content = button.Elements().Single();
        XElement icon = content.Element(xaml + "FontIcon")!;
        XElement[] labels = content.Elements(xaml + "TextBlock").ToArray();
        string code = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs"));

        Assert.Multiple(() =>
        {
            Assert.That((string?)button.Attribute("Visibility"), Is.EqualTo("Collapsed"));
            Assert.That((string?)button.Attribute("Click"), Is.EqualTo("SshWorkingDirectoryButton_Click"));
            Assert.That((string?)button.Attribute("HorizontalAlignment"), Is.EqualTo("Left"));
            Assert.That((string?)button.Attribute("HorizontalContentAlignment"), Is.EqualTo("Left"));
            Assert.That(button.Attribute("MinWidth"), Is.Null);
            Assert.That(button.Attribute("MaxWidth"), Is.Null);
            Assert.That(icon, Is.Not.Null);
            Assert.That((string?)icon.Attribute("AutomationProperties.Name"), Is.EqualTo("File transfer"));
            Assert.That((string?)labels[0].Attribute("Text"), Is.EqualTo(":"));
            Assert.That((string?)labels[1].Attribute(x + "Name"), Is.EqualTo("SshWorkingDirectoryText"));
            Assert.That(labels[1].Attribute("TextTrimming"), Is.Null);
            Assert.That(code, Does.Contain("IRemoteWorkingDirectorySession"));
            Assert.That(code, Does.Contain("SftpBrowserDialog"));
        });
    }

    [Test]
    public void SftpBrowserUsesCommanderStyleLocalAndRemotePanes()
    {
        string codePath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "SftpBrowserDialog.cs"));
        string code = File.ReadAllText(codePath);

        Assert.Multiple(() =>
        {
            Assert.That(code, Does.Contain("\"SftpLocalFiles\""));
            Assert.That(code, Does.Contain("\"SftpRemoteFiles\""));
            Assert.That(code, Does.Contain("SftpDialogSizing.Fit"));
            Assert.That(code, Does.Contain("var window = new Window"));
            Assert.That(code, Does.Contain("SetNativeOwner(window, _owner);"));
            Assert.That(code, Does.Contain("KeepAboveEmbeddedSession(window, _owner);"));
            Assert.That(
                code.IndexOf("SetNativeOwner(window, _owner);", StringComparison.Ordinal),
                Is.LessThan(code.IndexOf("window.Activate();", StringComparison.Ordinal)));
            Assert.That(
                code.IndexOf("KeepAboveEmbeddedSession(window, _owner);", StringComparison.Ordinal),
                Is.LessThan(code.IndexOf("window.Activate();", StringComparison.Ordinal)));
            Assert.That(code, Does.Not.Contain("IsAlwaysOnTop"));
            Assert.That(code, Does.Contain("NativeMethods.SetWindowPos("));
            Assert.That(code, Does.Contain("if (!IsLoipvRemoteForeground(ownerHandle))"));
            Assert.That(code, Does.Contain("window.DispatcherQueue.CreateTimer()"));
            Assert.That(code, Does.Contain("topmostTimer.Stop()"));
            Assert.That(code, Does.Contain("window.Activate()"));
            Assert.That(code, Does.Contain("SizeAndCenterWindow(window, _owner, dialogSize, rasterizationScale)"));
            Assert.That(code, Does.Not.Contain("new ContentDialog"));
            Assert.That(code, Does.Not.Contain("showDialogAsync"));
            Assert.That(code, Does.Contain("Text = $\"SFTP - {connection.Name} | {connection.Host}:{connection.Port}\""));
            Assert.That(code, Does.Contain("FontSize = 22"));
            Assert.That(code, Does.Contain("CreateToolbarButton(\"Up\", Symbol.Up)"));
            Assert.That(code, Does.Contain("CreateToolbarButton(\"Refresh\", Symbol.Refresh)"));
            Assert.That(code, Does.Contain("CreateToolbarButton(\"New folder\", Symbol.NewFolder)"));
            Assert.That(code, Does.Contain("new FontIcon { Glyph = \"\\uE711\", FontSize = 14 }"));
            Assert.That(code, Does.Contain("new TextBlock { Text = \"Close\""));
            Assert.That(code, Does.Contain("Orientation = Orientation.Horizontal"));
            Assert.That(code, Does.Not.Contain("CornerRadius = new CornerRadius(20)"));
            Assert.That(code, Does.Not.Contain("Glyph = \"\\uE711\", FontSize = 22"));
            Assert.That(code, Does.Contain("Content = new Viewbox"));
            Assert.That(code, Does.Contain("Child = new SymbolIcon(symbol)"));
            Assert.That(code, Does.Contain("Width = 32"));
            Assert.That(code, Does.Contain("Height = 32"));
            Assert.That(code, Does.Contain("Icon = new SymbolIcon(symbol)"));
            Assert.That(code, Does.Contain("ToolTipService.SetToolTip(button, accessibleName)"));
            Assert.That(code, Does.Not.Contain("Content = \"Up\""));
            Assert.That(code, Does.Not.Contain("Content = \"Refresh\""));
            Assert.That(code, Does.Not.Contain("Content = \"New folder\""));
            Assert.That(code, Does.Contain("RightTapped"));
            Assert.That(code, Does.Not.Contain("CloseButtonText = \"Close\""));
            Assert.That(code, Does.Not.Contain("var uploadButton = new Button"));
            Assert.That(code, Does.Not.Contain("var downloadButton = new Button"));
            Assert.That(code, Does.Not.Contain("FileOpenPicker"));
            Assert.That(code, Does.Not.Contain("FileSavePicker"));
            Assert.That(code, Does.Contain("SftpConfirmationOverlay"));
            Assert.That(code, Does.Contain("HorizontalAlignment = HorizontalAlignment.Center"));
            Assert.That(code, Does.Contain("VerticalAlignment = VerticalAlignment.Center"));
            Assert.That(code, Does.Not.Contain("FlyoutPlacementMode.Bottom"));
            Assert.That(code, Does.Not.Contain("flyout.ShowAt(target)"));
            Assert.That(code, Does.Contain("TransferStatus.Glyph"));
            Assert.That(code, Does.Contain("TransferStatus.Text"));
            Assert.That(code, Does.Contain("new Progress<FileTransferProgress>"));
            Assert.That(code, Does.Not.Contain("SetBusy(true, $\"Uploading"));
            Assert.That(code, Does.Not.Contain("SetBusy(true, $\"Downloading"));
        });
    }

    [Test]
    public void ClosingSftpBrowserRestoresKeyboardFocusToActiveSshSession()
    {
        string codePath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "MainWindow.xaml.cs"));
        string code = File.ReadAllText(codePath);

        int dialogClosed = code.IndexOf("_sftpDialogOpen = false;", StringComparison.Ordinal);
        int focusRestored = code.IndexOf("RecoverSessionKeyboardFocus();", dialogClosed, StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(dialogClosed, Is.GreaterThanOrEqualTo(0));
            Assert.That(focusRestored, Is.GreaterThan(dialogClosed));
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
