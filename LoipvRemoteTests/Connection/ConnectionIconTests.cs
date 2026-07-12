using LoipvRemote.Connection;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection
{
    [TestFixture]
    public sealed class ConnectionIconTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("mRemote")]
        [TestCase("mRemoteNG")]
        public void UsesLoipvRemoteForEveryConnectionIcon(string? iconName)
        {
            Assert.That(ConnectionIcon.GetConnectionDisplayIcon(iconName), Is.EqualTo(ConnectionIcon.LoipvRemoteIconName));
        }

        [Test]
        public void ReplacesAnExplicitCustomConnectionIcon()
        {
            Assert.That(ConnectionIcon.GetConnectionDisplayIcon("Linux"), Is.EqualTo(ConnectionIcon.LoipvRemoteIconName));
        }
    }
}
