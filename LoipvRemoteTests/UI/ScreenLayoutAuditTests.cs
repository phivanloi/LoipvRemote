using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using LoipvRemote.Desktop.UI.Connectors.AWS;
using LoipvRemote.Desktop.UI.Connectors.Delinea;
using LoipvRemote.Desktop.UI.Connectors.OpenBao;
using LoipvRemote.Desktop.UI.Connectors.Passwordstate;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Forms.OptionsPages;
using LoipvRemote.UI.TaskDialog;
using LoipvRemote.UI.Window;
using LoipvRemote.UI.DesignSystem;
using NUnit.Framework;
using WeifenLuo.WinFormsUI.Docking;

namespace LoipvRemoteTests.UI;

[TestFixture]
public sealed class ScreenLayoutAuditTests
{
    private static readonly IReadOnlyList<ScreenFactory> Screens =
    [
        new("Splash", () => new FrmSplashScreen()),
        new("About", () => new frmAbout()),
        new("Export", () => new FrmExport()),
        new("InputBox", () => new FrmInputBox("Input", "Prompt", "value")),
        new("Password", () => new FrmPassword()),
        new("UnhandledException", () => new UnhandledExceptionForm()),
        new("TaskDialog", () => new frmTaskDialog()),
        new("BaseWindow", () => new BaseWindow()),
        new("ConfigWindow", () => new ConfigWindow()),
        new("ConnectionTreeWindow", () => new ConnectionTreeWindow()),
        new("ConnectionWindow", () => new ConnectionWindow(new DockContent())),
        new("ErrorAndInfoWindow", () => new ErrorAndInfoWindow()),
        new("OptionsWindow", () => new OptionsWindow()),
        new("SSHTransferWindow", () => new SSHTransferWindow()),
        new("UltraVNCWindow", () => new UltraVNCWindow()),
        new("AdvancedPage", () => new AdvancedPage()),
        new("AppearancePage", () => new AppearancePage()),
        new("BackupPage", () => new BackupPage()),
        new("ConnectionsPage", () => new ConnectionsPage()),
        new("CredentialsPage", () => new CredentialsPage()),
        new("NotificationsPage", () => new NotificationsPage()),
        new("SecurityPage", () => new SecurityPage()),
        new("SqlServerPage", () => new SqlServerPage()),
        new("StartupExitPage", () => new StartupExitPage()),
        new("TabsPanelsPage", () => new TabsPanelsPage()),
        new("ThemePage", () => new ThemePage()),
        new("AWSConnectionForm", () => new AWSConnectionForm()),
        new("PasswordstateConnectionForm", () => new CPSConnectionForm()),
        new("DelineaConnectionForm", () => new SSConnectionForm()),
        new("OpenBaoConnectionForm", () => new VaultOpenbaoConnectionForm())
    ];

    [Test]
    [Apartment(ApartmentState.STA)]
    public void ParameterlessScreensKeepVisibleControlsInsideTheirParentClientArea()
    {
        List<string> violations = [];

        foreach (ScreenFactory screen in Screens)
        {
            using Control root = screen.Create();
            root.CreateControl();
            UiScaleManager.Instance.Apply(root);
            root.PerformLayout();
            violations.AddRange(FindBoundsViolations(root, screen.Name));
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    [TestCase(1.25f)]
    [TestCase(1.5f)]
    [TestCase(2.0f)]
    [Apartment(ApartmentState.STA)]
    public void ParameterlessScreensKeepVisibleControlsInsideTheirParentClientAreaAtScaledSize(float scale)
    {
        List<string> violations = [];

        foreach (ScreenFactory screen in Screens)
        {
            using Control root = screen.Create();
            root.Scale(new SizeF(scale, scale));
            UiScaleManager.Instance.Apply(root);
            root.PerformLayout();
            violations.AddRange(FindBoundsViolations(root, screen.Name));
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void VisibleInputControlsHaveUsableTypographyAndHeight()
    {
        List<string> violations = [];

        foreach (ScreenFactory screen in Screens)
        {
            using Control root = screen.Create();
            root.CreateControl();
            UiScaleManager.Instance.Apply(root);
            root.PerformLayout();

            foreach (Control control in AllDescendants(root).Where(IsEffectivelyVisible))
            {
                if (control is (TextBoxBase or ComboBox or NumericUpDown) && control.Parent is not NumericUpDown)
                {
                    if (control.Font.Height < 12)
                        violations.Add($"{screen.Name}: {control.GetType().Name} font height={control.Font.Height}");
                    if (control.Height < control.Font.Height + 4)
                        violations.Add($"{screen.Name}: {control.GetType().Name} height={control.Height}, font height={control.Font.Height}");
                }
            }
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> FindBoundsViolations(Control root, string screenName)
    {
        foreach (Control control in AllDescendants(root).Where(IsEffectivelyVisible))
        {
            Control? parent = control.Parent;
            if (parent is null || parent is ScrollableControl { AutoScroll: true } || parent is NumericUpDown)
                continue;

            Rectangle bounds = control.Bounds;
            Rectangle client = parent.ClientRectangle;
            if (!client.Contains(bounds))
                yield return $"{screenName}: {control.GetType().Name} {bounds} outside {parent.GetType().Name} client {client}";
        }
    }

    private static IEnumerable<Control> AllDescendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in AllDescendants(child))
                yield return descendant;
        }
    }

    private static bool IsEffectivelyVisible(Control control)
    {
        for (Control? current = control; current is not null; current = current.Parent)
        {
            if (!current.Visible)
                return false;
        }

        return true;
    }

    private sealed record ScreenFactory(string Name, Func<Control> Create);
}
