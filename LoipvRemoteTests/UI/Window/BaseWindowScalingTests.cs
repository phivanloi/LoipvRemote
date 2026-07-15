using System.Threading;
using System.Windows.Forms;
using LoipvRemote.UI.Window;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Window;

[TestFixture]
public sealed class BaseWindowScalingTests
{
    [Test]
    [Apartment(ApartmentState.STA)]
    public void DockWindowsInheritDpiAutoscaling()
    {
        using TestWindow window = new();

        Assert.That(window.AutoScaleMode, Is.EqualTo(AutoScaleMode.Dpi));
    }

    private sealed class TestWindow : BaseWindow
    {
    }
}
