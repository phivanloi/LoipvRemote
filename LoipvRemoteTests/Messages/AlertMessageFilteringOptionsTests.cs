using LoipvRemote.Messages.MessageFilteringOptions;
using NUnit.Framework;

namespace LoipvRemoteTests.Messages
{
    [TestFixture]
    public sealed class AlertMessageFilteringOptionsTests
    {
        [Test]
        public void ShowsOnlyWarningsAndErrors()
        {
            AlertMessageFilteringOptions options = new();

            Assert.Multiple(() =>
            {
                Assert.That(options.AllowDebugMessages, Is.False);
                Assert.That(options.AllowInfoMessages, Is.False);
                Assert.That(options.AllowWarningMessages, Is.True);
                Assert.That(options.AllowErrorMessages, Is.True);
            });
        }
    }
}
