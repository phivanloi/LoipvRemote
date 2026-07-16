using System;
using System.Text;
using LoipvRemote.Protocols.Putty.Monitoring;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Monitoring
{
    [TestFixture]
    public sealed class PuttyHostKeyTrustStoreTests
    {
        [Test]
        public void ConvertsEd25519PublicKeyIntoPuttyCacheFormat()
        {
            byte[] point = new byte[32];
            point[0] = 1; // Edwards25519 identity: x = 0, y = 1.

            string? cacheValue = PuttyHostKeyTrustStore.BuildEd25519CacheValue(
                "ssh-ed25519", BuildHostKey("ssh-ed25519", point));

            Assert.That(cacheValue, Is.EqualTo("0x0,0x1"));
        }

        [Test]
        public void RejectsMalformedOrMismatchedEd25519HostKey()
        {
            Assert.That(PuttyHostKeyTrustStore.BuildEd25519CacheValue("ssh-ed25519", Array.Empty<byte>()), Is.Null);
            Assert.That(PuttyHostKeyTrustStore.BuildEd25519CacheValue("ssh-rsa", BuildHostKey("ssh-ed25519", new byte[32])), Is.Null);
        }

        [Test]
        public void ConvertsRsaPublicKeyIntoPuttyCacheFormat()
        {
            byte[] exponent = [0x01, 0x00, 0x01];
            byte[] modulus = [0x00, 0xab, 0xcd, 0xef];

            string? cacheValue = PuttyHostKeyTrustStore.BuildRsaCacheValue(
                BuildHostKey("ssh-rsa", exponent, modulus));

            Assert.That(cacheValue, Is.EqualTo("0x10001,0xabcdef"));
        }

        [Test]
        public void TrustsOnlyMatchingPuTTYCachedHostKeyThroughRegistryBoundary()
        {
            byte[] exponent = [0x01, 0x00, 0x01];
            byte[] modulus = [0x00, 0xab, 0xcd, 0xef];
            byte[] hostKey = BuildHostKey("ssh-rsa", exponent, modulus);
            var registry = new FakeRegistryValueReader("0x10001,0xabcdef");

            bool trusted = new PuttyHostKeyTrustStore(registry)
                .IsTrusted("host.example", 22, "rsa-sha2-512", hostKey);

            Assert.Multiple(() =>
            {
                Assert.That(trusted, Is.True);
                Assert.That(registry.LastSubKeyPath, Is.EqualTo(@"Software\SimonTatham\PuTTY\SshHostKeys"));
                Assert.That(registry.LastValueName, Is.EqualTo("rsa2@22:host.example"));
            });
        }

        private static byte[] BuildHostKey(string algorithm, byte[] publicKey)
        {
            byte[] algorithmBytes = Encoding.ASCII.GetBytes(algorithm);
            byte[] result = new byte[4 + algorithmBytes.Length + 4 + publicKey.Length];
            WriteLength(result, 0, algorithmBytes.Length);
            Buffer.BlockCopy(algorithmBytes, 0, result, 4, algorithmBytes.Length);
            int publicKeyOffset = 4 + algorithmBytes.Length;
            WriteLength(result, publicKeyOffset, publicKey.Length);
            Buffer.BlockCopy(publicKey, 0, result, publicKeyOffset + 4, publicKey.Length);
            return result;
        }

        private static byte[] BuildHostKey(string algorithm, byte[] exponent, byte[] modulus)
        {
            byte[] algorithmBytes = Encoding.ASCII.GetBytes(algorithm);
            byte[] result = new byte[4 + algorithmBytes.Length + 4 + exponent.Length + 4 + modulus.Length];
            WriteLength(result, 0, algorithmBytes.Length);
            Buffer.BlockCopy(algorithmBytes, 0, result, 4, algorithmBytes.Length);
            int exponentOffset = 4 + algorithmBytes.Length;
            WriteLength(result, exponentOffset, exponent.Length);
            Buffer.BlockCopy(exponent, 0, result, exponentOffset + 4, exponent.Length);
            int modulusOffset = exponentOffset + 4 + exponent.Length;
            WriteLength(result, modulusOffset, modulus.Length);
            Buffer.BlockCopy(modulus, 0, result, modulusOffset + 4, modulus.Length);
            return result;
        }

        private static void WriteLength(byte[] destination, int offset, int length)
        {
            destination[offset] = (byte)(length >> 24);
            destination[offset + 1] = (byte)(length >> 16);
            destination[offset + 2] = (byte)(length >> 8);
            destination[offset + 3] = (byte)length;
        }

        private sealed class FakeRegistryValueReader(string? value) : IPuttyHostKeyRegistry
        {
            public string? LastSubKeyPath { get; private set; }
            public string? LastValueName { get; private set; }

            public string? GetCurrentUserString(string subKeyPath, string valueName)
            {
                LastSubKeyPath = subKeyPath;
                LastValueName = valueName;
                return value;
            }
        }
    }
}
