using System;
using System.Security.Cryptography;
using System.Text;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using NUnit.Framework;

namespace LoipvRemoteTests.Infrastructure.Windows.Dpapi;

public class WindowsDpapiSecretProtectorTests
{
    [Test]
    public void ProtectAndUnprotect_RoundTripsWithinCurrentUserAndPurpose()
    {
        var protector = new WindowsDpapiSecretProtector();
        byte[] plaintext = Encoding.UTF8.GetBytes("local-only-secret");

        byte[] protectedData = protector.Protect(plaintext, "connection-password");
        byte[] restored = protector.Unprotect(protectedData, "connection-password");

        Assert.Multiple(() =>
        {
            Assert.That(protectedData, Is.Not.EqualTo(plaintext));
            Assert.That(restored, Is.EqualTo(plaintext));
        });
    }

    [Test]
    public void UnprotectRejectsDifferentPurpose()
    {
        var protector = new WindowsDpapiSecretProtector();
        byte[] protectedData = protector.Protect(Encoding.UTF8.GetBytes("local-only-secret"), "connection-password");

        Assert.That(
            () => protector.Unprotect(protectedData, "proxy-password"),
            Throws.InstanceOf<CryptographicException>());
    }

    [Test]
    public void UnprotectRejectsTamperedCiphertext()
    {
        var protector = new WindowsDpapiSecretProtector();
        byte[] protectedData = protector.Protect(Encoding.UTF8.GetBytes("local-only-secret"), "connection-password");
        protectedData[^1] ^= 0x01;

        Assert.That(
            () => protector.Unprotect(protectedData, "connection-password"),
            Throws.InstanceOf<CryptographicException>());
    }

    [Test]
    public void RejectsBlankPurpose()
    {
        var protector = new WindowsDpapiSecretProtector();

        Assert.That(() => protector.Protect([1], string.Empty), Throws.ArgumentException);
    }
}
