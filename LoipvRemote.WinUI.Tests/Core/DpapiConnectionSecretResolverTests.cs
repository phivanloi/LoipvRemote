using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using LoipvRemote.Application.Credentials;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Core;

public sealed class DpapiConnectionSecretResolverTests
{
    [Test]
    public void ResolvePrefersTheNamedLocalDpapiCredential()
    {
        Guid credentialId = Guid.NewGuid();
        var resolver = new DpapiConnectionSecretResolver(new FakeSecretStore(), new FakeLocalCredentialStore(credentialId));
        var connection = new ConnectionDefinition(Guid.NewGuid(), "SSH", "ssh.example", 22, ProtocolKind.Ssh2, CredentialReference.LocalDpapi(credentialId));

        Assert.That(resolver.Resolve(connection, "Password"), Is.EqualTo("shared-password"));
    }

    private sealed class FakeSecretStore : IStringSecretStore
    {
        public string Protect(string plaintext, string purpose) => plaintext;
        public string Unprotect(string protectedValue, string purpose) => protectedValue;
    }

    private sealed class FakeLocalCredentialStore(Guid credentialId) : ILocalCredentialStore
    {
        public Task<IReadOnlyList<LocalCredentialDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LocalCredentialDefinition>>([]);

        public Task SaveAsync(LocalCredentialDefinition credential, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid credentialId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string? ResolvePassword(CredentialReference reference) =>
            reference.Identifier == credentialId.ToString("D") ? "shared-password" : null;
    }
}
