using System.Net;
using System.Net.Sockets;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Connectors.OpenBao {
    public class VaultOpenbaoException(string message, string? arguments = null) : Exception(message) {
        public string Arguments { get; set; } = arguments ?? string.Empty;
    }

    public static class VaultOpenbao {
        private static string token = "";
        private static async Task<VaultClient> GetClientAsync(
            IExternalCredentialPrompt prompt,
            IExternalCredentialSettingsStore settings,
            CancellationToken cancellationToken) {
            string url = settings.GetString("OpenBao", "URL") ?? string.Empty;
            OpenBaoPromptResult? result = await prompt.PromptOpenBaoAsync(
                new OpenBaoPromptRequest(url, token), cancellationToken).ConfigureAwait(true);
            if (result is null)
                throw new VaultOpenbaoException("No credential provided");
            url = result.Url;
            if (!string.IsNullOrEmpty(result.Token))
                token = result.Token;
            IAuthMethodInfo authMethod = new TokenAuthMethodInfo(token);
            var vaultClientSettings = new VaultClientSettings(url, authMethod);
            VaultClient client = new(vaultClientSettings);
            var sysInfo = await client.V1.System.GetInitStatusAsync().ConfigureAwait(false);
            if (!sysInfo) {
                throw new VaultOpenbaoException("OpenBao connection test failed.");
            }
            settings.SetString("OpenBao", "URL", url);
            return client;
        }
        private static async Task TestMountTypeAsync(VaultClient vaultClient, string mount, int vaultOpenbaoSecretEngine) {
            string backendType = (await vaultClient.V1.System.GetSecretBackendAsync(mount).ConfigureAwait(false)).Data.Type.Type;
            switch (backendType) {
                case "kv" when vaultOpenbaoSecretEngine != 0:
                    throw new VaultOpenbaoException($"Backend of type kv does not match expected type {vaultOpenbaoSecretEngine}");
                case "ldap" when vaultOpenbaoSecretEngine != 1 && vaultOpenbaoSecretEngine != 2:
                    throw new VaultOpenbaoException($"Backend of type ldap does not match expected type {vaultOpenbaoSecretEngine}");
                case "ssh" when vaultOpenbaoSecretEngine != 3:
                    throw new VaultOpenbaoException($"Backend of type ssh does not match expected type {vaultOpenbaoSecretEngine}");
            }
        }
        public static async Task<string> ReadOtpSSHAsync(
            string mount,
            string role,
            string? username,
            string address,
            IExternalCredentialPrompt prompt,
            IExternalCredentialSettingsStore settings,
            CancellationToken cancellationToken = default) {
            VaultClient vaultClient = await GetClientAsync(prompt, settings, cancellationToken).ConfigureAwait(false);
            await TestMountTypeAsync(vaultClient, mount, 3).ConfigureAwait(false);
            if (!IPAddress.TryParse(address, out _)) {
                try {
                    var addrs = await Dns.GetHostAddressesAsync(address, cancellationToken).ConfigureAwait(false);
                    if (addrs == null || addrs.Length == 0) {
                        throw new VaultOpenbaoException($"Could not resolve address '{address}'");
                    }
                    // Prefer IPv4, otherwise take first available
                    var selected = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addrs[0];
                    address = selected.ToString();
                } catch (Exception ex) {
                    throw new VaultOpenbaoException($"Failed to resolve address '{address}'", ex.Message);
                }
            }
            var otp = await vaultClient.V1.Secrets.SSH.GetCredentialsAsync(role, address, username, mount).ConfigureAwait(false);
            return otp.Data.Key;

        }
        public static async Task<string> ReadPasswordSSHAsync(
            int secretEngine,
            string mount,
            string role,
            string username,
            IExternalCredentialPrompt prompt,
            IExternalCredentialSettingsStore settings,
            CancellationToken cancellationToken = default) {
            VaultClient vaultClient = await GetClientAsync(prompt, settings, cancellationToken).ConfigureAwait(false);
            await TestMountTypeAsync(vaultClient, mount, secretEngine).ConfigureAwait(false);
            switch (secretEngine) {
                case 0:
                    var kv = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(role, mountPoint: mount).ConfigureAwait(false);
                    return kv.Data.Data[username].ToString() ?? string.Empty;
                default:
                    throw new VaultOpenbaoException($"Backend of type {secretEngine} is not supported");
            }
        }
        public static async Task<(string Username, string Password)> ReadPasswordRdpAsync(
            int secretEngine,
            string mount,
            string role,
            string username,
            IExternalCredentialPrompt prompt,
            IExternalCredentialSettingsStore settings,
            CancellationToken cancellationToken = default) {
            VaultClient vaultClient = await GetClientAsync(prompt, settings, cancellationToken).ConfigureAwait(false);
            await TestMountTypeAsync(vaultClient, mount, secretEngine).ConfigureAwait(false);
            switch (secretEngine) {
                case 0:
                    var kv = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(role, mountPoint: mount).ConfigureAwait(false);
                    return (username, kv.Data.Data[username].ToString() ?? string.Empty);
                case 1:
                    var ldapd = await vaultClient.V1.Secrets.OpenLDAP.GetDynamicCredentialsAsync(role, mount).ConfigureAwait(false);
                    return (ldapd.Data.Username, ldapd.Data.Password);
                case 2:
                    var ldaps = await vaultClient.V1.Secrets.OpenLDAP.GetStaticCredentialsAsync(role, mount).ConfigureAwait(false);
                    return (ldaps.Data.Username, ldaps.Data.Password);
                default:
                    throw new VaultOpenbaoException($"Backend of type {secretEngine} is not supported");
            }

        }

    }
}
