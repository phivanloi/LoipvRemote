using LoipvRemote.Connection;
using LoipvRemote.UI.Controls;
using LoipvRemote.Tools.Clipboard;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Controls
{
    [TestFixture]
    public sealed class ConnectionContextMenuPasswordTests
    {
        [Test]
        public void OnlyConnectionsWithAConfiguredPasswordCanRevealIt()
        {
            ConnectionInfo configured = new() { Password = "configured-password" };
            ConnectionInfo empty = new();

            Assert.Multiple(() =>
            {
                Assert.That(ConnectionContextMenu.CanShowPassword(configured), Is.True);
                Assert.That(ConnectionContextMenu.CanShowPassword(empty), Is.False);
                Assert.That(ConnectionContextMenu.CanShowPassword(null), Is.False);
            });
        }

        [Test]
        public void CopiesThePasswordOnlyWhenTheCopyActionIsInvoked()
        {
            IClipboard clipboard = Substitute.For<IClipboard>();

            ConnectionContextMenu.CopyPasswordToClipboard(clipboard, "password-value");

            clipboard.Received(1).SetText("password-value");
        }
    }
}
