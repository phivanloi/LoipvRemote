using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using LoipvRemote.Messages;
using LoipvRemote.UI;
using LoipvRemote.UI.Window;
using NUnit.Framework;
using NotificationMessage = LoipvRemote.Messages.Message;

namespace LoipvRemoteTests.UI.Window;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class ErrorAndInfoWindowLayoutTests
{
    [Test]
    public void EmptyNotificationWindowUsesEntireClientAreaForList()
    {
        using ErrorAndInfoWindow window = new();
        window.ClientSize = new Size(420, 900);
        window.Show();
        window.PerformLayout();
        Application.DoEvents();

        Assert.That(window.pnlErrorMsg.Visible, Is.False);
        Assert.That(window.lblEmptyNotifications.Visible, Is.True);
        Assert.That(window.lvErrorCollector.Bounds, Is.EqualTo(window.ClientRectangle));
        Assert.That(window.lvErrorCollector.Dock, Is.EqualTo(DockStyle.Fill));
    }

    [Test]
    public void EmptyNotificationWindowStillFillsClientAreaAfterResize()
    {
        using ErrorAndInfoWindow window = new();
        window.ClientSize = new Size(900, 320);
        window.Show();
        window.PerformLayout();
        Application.DoEvents();

        Assert.That(window.pnlErrorMsg.Visible, Is.False);
        Assert.That(window.lblEmptyNotifications.Visible, Is.True);
        Assert.That(window.lvErrorCollector.Bounds, Is.EqualTo(window.ClientRectangle));
    }

    [TestCase(420, 900)]
    [TestCase(900, 320)]
    public void SelectedNotificationKeepsListAndDetailsInsideClientArea(int width, int height)
    {
        using ErrorAndInfoWindow window = new();
        window.ClientSize = new Size(width, height);
        window.Show();
        NotificationMessageListViewItem item = new(new NotificationMessage(MessageClass.InformationMsg, "Test notification"));
        window.lvErrorCollector.Items.Add(item);
        item.Selected = true;
        Application.DoEvents();

        Assert.That(window.pnlErrorMsg.Visible, Is.True);
        Assert.That(window.lblEmptyNotifications.Visible, Is.False);
        Assert.That(window.txtMsgText.Visible, Is.True);
        Assert.That(window.ClientRectangle.Contains(window.pnlErrorMsg.Bounds), Is.True);
        Assert.That(window.ClientRectangle.Contains(window.lvErrorCollector.Bounds), Is.True);
        Assert.That(Rectangle.Intersect(window.pnlErrorMsg.Bounds, window.lvErrorCollector.Bounds), Is.EqualTo(Rectangle.Empty));
    }
}
