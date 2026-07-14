using LoipvRemote.Connectors.Abstractions;

namespace LoipvRemote.Connectors.OpenBao;

public sealed class OpenBaoCredentialConnector : IContextualExternalCredentialConnector
{
    private const int SshOtpEngine = 3;

    public string Provider => "VaultOpenbao";

    public ExternalCredential Resolve(string secretReference) =>
        throw new NotSupportedException("OpenBao credentials require mount, role, protocol and secret-engine context.");

    public ExternalCredential Resolve(ExternalCredentialRequest request)
    {
        string username = request.Username;
        string password;
        if (request.Protocol == ExternalCredentialProtocol.Ssh)
        {
            if (request.SecretEngine == SshOtpEngine)
                VaultOpenbao.ReadOtpSSH(request.Mount, request.Role, username, request.Host, out password);
            else
                VaultOpenbao.ReadPasswordSSH(request.SecretEngine, request.Mount, request.Role, username, out password);
        }
        else
        {
            VaultOpenbao.ReadPasswordRDP(request.SecretEngine, request.Mount, request.Role, ref username, out password);
        }

        return new ExternalCredential(username, password, string.Empty, string.Empty);
    }
}
