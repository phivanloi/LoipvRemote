using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using LoipvRemote.UI.Forms;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms;

[TestFixture]
public sealed class FrmSplashScreenTests
{
    [Test]
    [Apartment(ApartmentState.STA)]
    public void SplashUsesDpiScalingAndKeepsEveryVisibleElementInsideItsClientArea()
    {
        using FrmSplashScreen splash = new();
        _ = splash.Handle;
        splash.PerformLayout();

        Assert.That(splash.AutoScaleMode, Is.EqualTo(AutoScaleMode.Dpi));
        Assert.That(AllDescendants(splash), Is.All.Matches<Control>(control =>
            !control.Visible || splash.ClientRectangle.Contains(splash.RectangleToClient(control.RectangleToScreen(control.ClientRectangle)))),
            "Every visible splash element must remain inside the splash client area after DPI scaling.");
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

    [Test]
    [Apartment(ApartmentState.STA)]
    public void ContentRemainsInsideTheSplashAtOneHundredTwentyFivePercentScale()
    {
        using FrmSplashScreen splash = new();
        splash.Scale(new SizeF(1.25f, 1.25f));
        splash.PerformLayout();

        Assert.That(AllDescendants(splash).All(control =>
            !control.Visible || splash.ClientRectangle.Contains(
                splash.RectangleToClient(control.RectangleToScreen(control.ClientRectangle)))),
            Is.True);
    }

    [TestCase(1.0f)]
    [TestCase(1.25f)]
    [TestCase(1.5f)]
    [TestCase(2.0f)]
    [Apartment(ApartmentState.STA)]
    public void BrandingLabelsNeverClipOrOverlap(float scale)
    {
        using FrmSplashScreen splash = new();
        _ = splash.Handle;
        splash.Scale(new SizeF(scale, scale));
        splash.PerformLayout();

        Label title = AllDescendants(splash).OfType<Label>().Single(label => label.Text == "LoipvRemote");
        Label subtitle = AllDescendants(splash).OfType<Label>().Single(label => label.Text == "Remote Connection Manager");
        Label version = AllDescendants(splash).OfType<Label>().Single(label => label.Text.StartsWith("Phiên bản", StringComparison.Ordinal));

        Rectangle client = splash.ClientRectangle;
        Rectangle titleBounds = BoundsInRoot(title, splash);
        Rectangle subtitleBounds = BoundsInRoot(subtitle, splash);
        Rectangle versionBounds = BoundsInRoot(version, splash);

        Assert.Multiple(() =>
        {
            Assert.That(client.Contains(titleBounds), Is.True, $"title={titleBounds}, client={client}");
            Assert.That(client.Contains(subtitleBounds), Is.True, $"subtitle={subtitleBounds}, client={client}");
            Assert.That(client.Contains(versionBounds), Is.True, $"version={versionBounds}, client={client}");
            Assert.That(titleBounds.Bottom, Is.LessThanOrEqualTo(subtitleBounds.Top),
                $"title={titleBounds}, subtitle={subtitleBounds}");
            Assert.That(subtitleBounds.Bottom, Is.LessThanOrEqualTo(versionBounds.Top),
                $"subtitle={subtitleBounds}, version={versionBounds}");
        });
    }

    private static Rectangle BoundsInRoot(Control control, Control root)
    {
        Rectangle bounds = control.Bounds;
        for (Control? parent = control.Parent; parent is not null && !ReferenceEquals(parent, root); parent = parent.Parent)
            bounds.Offset(parent.Bounds.Location);

        return bounds;
    }
}
