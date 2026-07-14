using LoipvRemote.Protocols.Rdp;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol
{
    [TestFixture]
    public class RdpCredentialPolicyTests
    {
        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        public void ClearTextPasswordPolicyMatchesKerberosOnlyModes(
            bool useRestrictedAdmin,
            bool useRemoteCredentialGuard,
            bool expected)
        {
            bool result = RdpCredentialPolicy.ShouldAssignClearTextPassword(
                useRestrictedAdmin,
                useRemoteCredentialGuard);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
