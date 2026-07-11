using mRemoteNG.UI.Window;
using NUnit.Framework;

namespace mRemoteNGTests.UI.Window
{
    [TestFixture]
    public class TerminalTitleFormatterTests
    {
        [Test]
        public void Format_RemovesControlCharactersAndEscapesAmpersands()
        {
            string result = TerminalTitleFormatter.Format("host\r\n& admin", "Server");

            Assert.That(result, Is.EqualTo("host&& admin (Server)"));
        }

        [Test]
        public void Format_EmptyTitleFallsBackToConnectionName()
        {
            Assert.That(TerminalTitleFormatter.Format("\r\n", "Server & DB"),
                Is.EqualTo("Server && DB"));
        }

        [Test]
        public void Format_LimitsUntrustedTerminalTitleLength()
        {
            string result = TerminalTitleFormatter.Format(new string('x', 500), "Server");

            Assert.That(result, Has.Length.EqualTo(209));
        }
    }
}
