using System.Reflection;
using System.Windows.Forms;
using LoipvRemote.Desktop.UI.Connectors.AWS;
using LoipvRemote.Desktop.UI.Connectors.Delinea;
using LoipvRemote.Desktop.UI.Connectors.OpenBao;
using LoipvRemote.Desktop.UI.Connectors.Passwordstate;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Forms.OptionsPages;
using LoipvRemote.UI.Tabs;
using LoipvRemote.UI.TaskDialog;
using LoipvRemote.UI.Window;
using NUnit.Framework;
using WeifenLuo.WinFormsUI.Docking;

namespace LoipvRemoteTests.UI;

[TestFixture]
public sealed class ScreenCatalogTests
{
    private static readonly IReadOnlyList<ScreenDescriptor> Screens =
    [
        new(typeof(FrmSplashScreen), ScreenArea.Startup),
        new(typeof(FrmMain), ScreenArea.Shell),
        new(typeof(frmAbout), ScreenArea.Dialog),
        new(typeof(FrmChoosePanel), ScreenArea.Dialog),
        new(typeof(FrmExport), ScreenArea.Dialog),
        new(typeof(FrmInputBox), ScreenArea.Dialog),
        new(typeof(FrmOptions), ScreenArea.Dialog),
        new(typeof(FrmPassword), ScreenArea.Dialog),
        new(typeof(UnhandledExceptionForm), ScreenArea.Dialog),
        new(typeof(frmTaskDialog), ScreenArea.Dialog),
        new(typeof(BaseWindow), ScreenArea.Docking),
        new(typeof(ConfigWindow), ScreenArea.Docking),
        new(typeof(ConnectionTreeWindow), ScreenArea.Docking),
        new(typeof(ConnectionWindow), ScreenArea.Docking),
        new(typeof(ErrorAndInfoWindow), ScreenArea.Docking),
        new(typeof(ExternalToolsWindow), ScreenArea.Tools),
        new(typeof(OptionsWindow), ScreenArea.Options),
        new(typeof(PortScanWindow), ScreenArea.Tools),
        new(typeof(SSHTransferWindow), ScreenArea.Tools),
        new(typeof(UltraVNCWindow), ScreenArea.Tools),
        new(typeof(ActiveDirectoryImportWindow), ScreenArea.Tools),
        new(typeof(ConnectionTab), ScreenArea.Protocol),
        new(typeof(OptionsPage), ScreenArea.Options),
        new(typeof(AdvancedPage), ScreenArea.Options),
        new(typeof(AppearancePage), ScreenArea.Options),
        new(typeof(BackupPage), ScreenArea.Options),
        new(typeof(ConnectionsPage), ScreenArea.Options),
        new(typeof(CredentialsPage), ScreenArea.Options),
        new(typeof(NotificationsPage), ScreenArea.Options),
        new(typeof(SecurityPage), ScreenArea.Options),
        new(typeof(SqlServerPage), ScreenArea.Options),
        new(typeof(StartupExitPage), ScreenArea.Options),
        new(typeof(TabsPanelsPage), ScreenArea.Options),
        new(typeof(ThemePage), ScreenArea.Options),
        new(typeof(AWSConnectionForm), ScreenArea.Connector),
        new(typeof(CPSConnectionForm), ScreenArea.Connector),
        new(typeof(SSConnectionForm), ScreenArea.Connector),
        new(typeof(VaultOpenbaoConnectionForm), ScreenArea.Connector)
    ];

    [Test]
    public void CatalogHasNoDuplicateTypes()
    {
        Assert.That(Screens.Select(screen => screen.Type), Is.Unique);
    }

    [Test]
    public void CatalogContainsEveryConcreteDesktopFormAndDockContent()
    {
        Assembly desktopAssembly = typeof(FrmMain).Assembly;
        HashSet<Type> catalogTypes = Screens.Select(screen => screen.Type).ToHashSet();
        Type[] discoverableTypes = desktopAssembly.GetTypes()
            .Where(type => type.IsPublic && !type.IsAbstract &&
                (typeof(Form).IsAssignableFrom(type) || typeof(DockContent).IsAssignableFrom(type)))
            .Where(type => type.Namespace is not null &&
                (type.Namespace.StartsWith("LoipvRemote.UI.Forms", StringComparison.Ordinal) ||
                 type.Namespace.StartsWith("LoipvRemote.UI.Window", StringComparison.Ordinal) ||
                 type.Namespace.StartsWith("LoipvRemote.UI.Tabs", StringComparison.Ordinal) ||
                 type.Namespace.StartsWith("LoipvRemote.UI.TaskDialog", StringComparison.Ordinal)))
            .ToArray();

        Type[] missing = discoverableTypes.Where(type => !catalogTypes.Contains(type)).ToArray();
        Assert.That(missing, Is.Empty,
            "Every concrete desktop Form/DockContent must have a screen catalog entry: " +
            string.Join(", ", missing.Select(type => type.FullName)));
    }

    [Test]
    public void CatalogContainsAllConfiguredOptionsPages()
    {
        Type[] expectedPages =
        [
            typeof(AdvancedPage), typeof(AppearancePage), typeof(BackupPage),
            typeof(ConnectionsPage), typeof(CredentialsPage), typeof(NotificationsPage),
            typeof(SecurityPage), typeof(SqlServerPage), typeof(StartupExitPage),
            typeof(TabsPanelsPage), typeof(ThemePage)
        ];

        HashSet<Type> catalogTypes = Screens.Select(screen => screen.Type).ToHashSet();
        Assert.That(expectedPages.All(catalogTypes.Contains), Is.True);
    }

    [Test]
    public void CatalogCoversEveryRequiredScreenArea()
    {
        Assert.That(Enum.GetValues<ScreenArea>().All(area => Screens.Any(screen => screen.Area == area)), Is.True);
    }

    private sealed record ScreenDescriptor(Type Type, ScreenArea Area);

    private enum ScreenArea
    {
        Startup,
        Shell,
        Docking,
        Options,
        Tools,
        Dialog,
        Connector,
        Protocol
    }
}
