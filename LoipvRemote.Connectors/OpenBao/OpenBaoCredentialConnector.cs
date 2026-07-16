using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Connectors.OpenBao;

public sealed class OpenBaoCredentialConnector : IContextualExternalCredentialConnector
{
    private const int SshOtpEngine = 3;
    private readonly IExternalCredentialPrompt _prompt;
    private readonly IExternalCredentialSettingsStore _settings;

    public OpenBaoCredentialConnector(
        IExternalCredentialPrompt prompt,
        IExternalCredentialSettingsStore settings)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string Provider => "VaultOpenbao";

    public Task<ExternalCredential> ResolveAsync(
        string secretReference,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("OpenBao credentials require mount, role, protocol and secret-engine context.");

    public async Task<ExternalCredential> ResolveAsync(
        ExternalCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        string username = request.Username;
        string password;
        if (request.Protocol == ExternalCredentialProtocol.Ssh)
        {
            if (request.SecretEngine == SshOtpEngine)
                password = await VaultOpenbao.ReadOtpSSHAsync(
                    request.Mount, request.Role, username, request.Host, _prompt, _settings, cancellationToken);
            else
                password = await VaultOpenbao.ReadPasswordSSHAsync(
                    request.SecretEngine, request.Mount, request.Role, username, _prompt, _settings, cancellationToken);
        }
        else
        {
            (username, password) = await VaultOpenbao.ReadPasswordRdpAsync(
                request.SecretEngine, request.Mount, request.Role, username, _prompt, _settings, cancellationToken);
        }

        return new ExternalCredential(username, password, string.Empty, string.Empty);
    }
}
