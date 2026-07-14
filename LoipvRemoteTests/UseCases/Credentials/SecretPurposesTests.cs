using LoipvRemote.UseCases.Credentials;
using NUnit.Framework;

namespace LoipvRemoteTests.UseCases.Credentials;

public class SecretPurposesTests
{
    [Test]
    public void KeepsStableValuesForExistingProtectedSettings()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SecretPurposes.DefaultCredentialPassword, Is.EqualTo("default-credential-password"));
            Assert.That(SecretPurposes.SqlPassword, Is.EqualTo("sql-password"));
        });
    }
}
