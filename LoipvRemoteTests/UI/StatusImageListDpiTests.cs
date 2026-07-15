using LoipvRemote.UI;
using NUnit.Framework;

namespace LoipvRemoteTests.UI;

public sealed class StatusImageListDpiTests
{
    [Test]
    public void ConnectionIconsScaleWithTheOwningTreeDpi()
    {
        using StatusImageList images = new();

        images.ApplyDpi(120);

        Assert.That(images.ImageList.ImageSize.Width, Is.EqualTo(25));
        Assert.That(images.ImageList.ImageSize.Height, Is.EqualTo(25));
    }
}
