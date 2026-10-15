using System.Xml.Linq;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class WinUIDpiAwarenessManifestTests
{
    [Test]
    public void ExecutableDeclaresPerMonitorV2DpiAwareness()
    {
        string manifestPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Fixtures",
            "LoipvRemote.app.manifest");
        XDocument manifest = XDocument.Load(manifestPath);
        XNamespace windowsSettings2016 = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";

        string dpiAwareness = manifest
            .Descendants(windowsSettings2016 + "dpiAwareness")
            .Single()
            .Value;

        Assert.That(dpiAwareness, Does.StartWith("PerMonitorV2"));
    }
}
