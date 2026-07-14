using System;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using NUnit.Framework;

namespace LoipvRemoteTests.Infrastructure.Windows.Dpapi;

public class DpapiStringSecretStoreTests
{
    [Test]
    public void ProtectAndUnprotect_RoundTripsVersionedText()
    {
        var store = new DpapiStringSecretStore(new WindowsDpapiSecretProtector());

        string protectedValue = store.Protect("local-only-secret", "default-credential-password");
        string restored = store.Unprotect(protectedValue, "default-credential-password");

        Assert.Multiple(() =>
        {
            Assert.That(protectedValue, Does.StartWith("dpapi:v1:"));
            Assert.That(protectedValue, Does.Not.Contain("local-only-secret"));
            Assert.That(restored, Is.EqualTo("local-only-secret"));
        });
    }

    [Test]
    public void UnprotectRejectsValuesWithoutTheDpapiVersionPrefix()
    {
        var store = new DpapiStringSecretStore(new WindowsDpapiSecretProtector());

        Assert.That(
            () => store.Unprotect("legacy-ciphertext", "default-credential-password"),
            Throws.InstanceOf<FormatException>());
    }
}
