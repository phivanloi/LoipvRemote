using System;
using System.Text;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Connection.Protocol.PowerShell;
using mRemoteNG.Connection.Protocol.Terminal;
using NUnit.Framework;

namespace mRemoteNGTests.Connection.Protocol
{
    [TestFixture]
    public class ProcessLaunchSecurityTests
    {
        [Test]
        public void TerminalRemoteConnection_DoesNotUseCommandProcessor()
        {
            TerminalProcessStartInfo result = TerminalProcessStartInfoBuilder.Build(
                "server & calc.exe", "admin", 22, "cmd.exe");

            Assert.Multiple(() =>
            {
                Assert.That(result.FileName, Is.EqualTo("ssh.exe"));
                Assert.That(result.Arguments, Does.Not.Contain("/K"));
                Assert.That(result.Arguments, Does.Contain("server & calc.exe"));
            });
        }

        [Test]
        public void Quote_EscapesQuotesAndTrailingBackslashes()
        {
            Assert.That(ProcessArgumentEscaper.Quote("user name\\\"x\\"),
                        Is.EqualTo("\"user name\\\\\\\"x\\\\\""));
        }

        [Test]
        public void PowerShellArguments_DoNotContainUntrustedValuesOrCommandSyntax()
        {
            const string payload = "'; Start-Process calc.exe; #'";
            string arguments = PowerShellCommandBuilder.BuildEncodedArguments("param($Hostname)", payload, payload, payload, 3);
            string encoded = arguments[(arguments.LastIndexOf(' ') + 1)..];
            string decoded = Encoding.Unicode.GetString(Convert.FromBase64String(encoded));

            Assert.Multiple(() =>
            {
                Assert.That(arguments, Does.Not.Contain(payload));
                Assert.That(decoded, Does.Not.Contain(payload));
                Assert.That(arguments, Does.StartWith("-NoExit -NoProfile -EncodedCommand "));
            });
        }
    }
}
