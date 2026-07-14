using LoipvRemote.Infrastructure.Windows.ProcessManagement;
using NUnit.Framework;

namespace LoipvRemoteTests.Infrastructure.Windows;

public class WindowsProcessControllerTests
{
    [Test]
    public void Constructor_RejectsNonPositiveWindowDiscoveryTimeout()
    {
        Assert.That(
            () => new WindowsProcessController(TimeSpan.Zero),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Start_RejectsBlankExecutablePath()
    {
        using var controller = new WindowsProcessController(TimeSpan.FromSeconds(1));

        Assert.That(
            () => controller.Start(" "),
            Throws.TypeOf<ArgumentException>());
    }
}
