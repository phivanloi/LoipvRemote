using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using SecretServerAuthentication.DSS;
using SecretServerRestClient.DSS;
using System.Security.Cryptography;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Connectors.Delinea;

public class SecretServerInterface
{
    private readonly IExternalCredentialPrompt _prompt;
    private readonly IExternalCredentialSettingsStore _settings;

    public SecretServerInterface(
        IExternalCredentialPrompt prompt,
        IExternalCredentialSettingsStore settings)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    private static class SSConnectionData
    {
        public static string ssUsername = "";
        public static string ssPassword = "";
        public static string ssUrl = "";
        public static string ssOTP = "";
        public static bool ssSSO;
        public static bool initdone;
        public static IExternalCredentialPrompt? Prompt;
        public static IExternalCredentialSettingsStore? Settings;

        //token 
        public static string ssTokenBearer = "";
        public static DateTime ssTokenExpiresOn = DateTime.UtcNow;
        public static string ssTokenRefresh = "";

        public static async Task InitAsync(
            IExternalCredentialPrompt prompt,
            IExternalCredentialSettingsStore settings,
            CancellationToken cancellationToken)
        {
            Prompt = prompt;
            Settings = settings;
            if (initdone == true)
                return;

                try
                {
                    string? un = settings.GetString("Delinea", "Username");

                string? url = settings.GetString("Delinea", "URL");
                if (url == null || !url.Contains("://"))
                    url = "https://cred.domain.local/SecretServer";

                ssSSO = settings.GetBoolean("Delinea", "SSO");
                
                while (true)
                {
                    DelineaPromptResult? result = await prompt.PromptDelineaAsync(
                        new DelineaPromptRequest(url, un ?? "", ssPassword, ssOTP, ssSSO),
                        cancellationToken).ConfigureAwait(true);
                    if (result is null)
                        return;

                    // store values to memory
                    ssUsername = result.Username;
                    ssPassword = result.Password;
                    ssUrl = result.ServerUrl;
                    url = ssUrl;
                    ssSSO = result.UseSso;
                    ssOTP = result.OneTimePassword;
                    // check connection first
                    try
                    {
                        if (await TestCredentialsAsync(cancellationToken).ConfigureAwait(true))
                        {
                            initdone = true;
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        // Prompt again without coupling the connector runtime to a UI toolkit.
                    }
                }


                // write values to registry
                settings.SetString("Delinea", "Username", ssUsername);
                settings.SetString("Delinea", "URL", ssUrl);
                settings.SetBoolean("Delinea", "SSO", ssSSO);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    private static async Task<bool> TestCredentialsAsync(CancellationToken cancellationToken)
    {
        if (SSConnectionData.ssSSO)
        {
            // checking creds doesn't really make sense here, as we can't modify them anyway if something is wrong
            return true;
        }
        else
        {

            if (!String.IsNullOrEmpty(await GetTokenAsync(cancellationToken).ConfigureAwait(false)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    private static async Task<SecretsServiceClient> ConstructSecretsServiceClientAsync(CancellationToken cancellationToken)
    {
        string baseURL = SSConnectionData.ssUrl;
        if (SSConnectionData.ssSSO)
        {
            // REQUIRES IIS CONFIG! https://docs.thycotic.com/ss/11.0.0/api-scripting/webservice-iwa-powershell
            var handler = new HttpClientHandler() { UseDefaultCredentials = true };
            var httpClient = new HttpClient(handler);
            {
                // Call REST API:
                return new SecretsServiceClient($"{baseURL}/winauthwebservices/api", httpClient);
            }
        }
        else
        {
            var httpClient = new HttpClient();
            {

                var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
                // Set credentials (token):
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Call REST API:
                return new SecretsServiceClient($"{baseURL}/api", httpClient);
            }
        }

    }
    private static async Task<ExternalCredential> FetchSecretAsync(int secretID, CancellationToken cancellationToken)
    {
        var client = await ConstructSecretsServiceClientAsync(cancellationToken).ConfigureAwait(false);
        SecretModel secret = await client.GetSecretAsync(false, true, secretID, null, cancellationToken).ConfigureAwait(false);

        // clear return variables
        string secretDomain = "";
        string secretUsername = "";
        string secretPassword = "";
        string privatekey = "";
        string privatekeypassphrase = "";

        // parse data and extract what we need
        foreach (var item in secret.Items)
        {
            if (string.Equals(item.FieldName, "domain", StringComparison.OrdinalIgnoreCase))
                secretDomain = item.ItemValue;
            else if (string.Equals(item.FieldName, "username", StringComparison.OrdinalIgnoreCase))
                secretUsername = item.ItemValue;
            else if (string.Equals(item.FieldName, "password", StringComparison.OrdinalIgnoreCase))
                secretPassword = item.ItemValue;
            else if (string.Equals(item.FieldName, "private key", StringComparison.OrdinalIgnoreCase))
            {
                client.ReadResponseNoJSONConvert = true;
                privatekey = await client.GetFieldAsync(false, false, secretID, "private-key", cancellationToken).ConfigureAwait(false);
                client.ReadResponseNoJSONConvert = false;
            }
            else if (string.Equals(item.FieldName, "private key passphrase", StringComparison.OrdinalIgnoreCase))
                privatekeypassphrase = item.ItemValue;
        }

        // need to decode the private key?
        if (!string.IsNullOrEmpty(privatekeypassphrase))
        {
            try
            {
                var key = DecodePrivateKey(privatekey, privatekeypassphrase);
                privatekey = key;
            }
            catch(Exception)
            {

            }
        }

        // conversion to putty format necessary?
        if (!string.IsNullOrEmpty(privatekey) && !privatekey.StartsWith("PuTTY-User-Key-File-2", StringComparison.Ordinal))
        {
            try
            {
                RSACryptoServiceProvider key = ImportPrivateKey(privatekey);
                privatekey = PuttyKeyFileGenerator.ToPuttyPrivateKey(key);
            }
            catch (Exception)
            {

            }
        }

        return new ExternalCredential(secretUsername, secretPassword, secretDomain, privatekey);
    }

        #region PUTTY KEY HANDLING
        // decode rsa private key with encryption password
        private static string DecodePrivateKey(string encryptedPrivateKey, string password)
        {
            TextReader textReader = new StringReader(encryptedPrivateKey);
            PemReader pemReader = new(textReader, new PasswordFinder(password));

        AsymmetricCipherKeyPair keyPair = (AsymmetricCipherKeyPair)pemReader.ReadObject();

        TextWriter textWriter = new StringWriter();
        var pemWriter = new PemWriter(textWriter);
        pemWriter.WriteObject(keyPair.Private);
        pemWriter.Writer.Flush();

        return ""+textWriter.ToString();
    }
    private sealed class PasswordFinder(string password) : IPasswordFinder
    {
        private string password = password;

        public char[] GetPassword()
        {
            return password.ToCharArray();
        }
    }

        // read private key pem string to rsacryptoserviceprovider
        public static RSACryptoServiceProvider ImportPrivateKey(string pem)
        {
            PemReader pr = new(new StringReader(pem));
            AsymmetricCipherKeyPair KeyPair = (AsymmetricCipherKeyPair)pr.ReadObject();
            RSAParameters rsaParams = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)KeyPair.Private);
            RSACryptoServiceProvider rsa = new();
            rsa.ImportParameters(rsaParams);
            if (rsa.KeySize < 2048)
            {
                rsa.Dispose();
                throw new CryptographicException("The imported RSA private key must be at least 2048 bits.");
            }
            return rsa;
        }
        #endregion


    #region TOKEN
    private static async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        // if there is no token, fetch a fresh one
        if (String.IsNullOrEmpty(SSConnectionData.ssTokenBearer))
        {
            return await GetTokenFreshAsync(cancellationToken).ConfigureAwait(false);
        }
        // if there is a token, check if it is valid
        if (SSConnectionData.ssTokenExpiresOn >= DateTime.UtcNow)
        {
            return SSConnectionData.ssTokenBearer;
        }
        else
        {
            // try using refresh token
            using (var httpClient = new HttpClient())
            {
                var tokenClient = new OAuth2ServiceClient(SSConnectionData.ssUrl, httpClient);
                TokenResponse token = new();
                try
                {
                    token = await tokenClient.AuthorizeAsync(
                        Grant_type.Refresh_token,
                        null,
                        null,
                        SSConnectionData.ssTokenRefresh,
                        cancellationToken,
                        null).ConfigureAwait(false);
                    var tokenResult = token.Access_token;

                    SSConnectionData.ssTokenBearer = tokenResult;
                    SSConnectionData.ssTokenRefresh = token.Refresh_token;
                    SSConnectionData.ssTokenExpiresOn = token.Expires_on;
                    return tokenResult;
                }
                catch (Exception)
                {
                    // refresh token failed. clean memory and start fresh
                    SSConnectionData.ssTokenBearer = "";
                    SSConnectionData.ssTokenRefresh = "";
                    SSConnectionData.ssTokenExpiresOn = DateTime.Now;
                    // if OTP is required we need to ask user for a new OTP
                    if (!String.IsNullOrEmpty(SSConnectionData.ssOTP))
                    {
                        SSConnectionData.initdone = false;
                        // the call below executes a connection test, which fetches a valid token
                        await SSConnectionData.InitAsync(
                            SSConnectionData.Prompt ?? throw new InvalidOperationException("Credential prompt is not configured."),
                            SSConnectionData.Settings ?? throw new InvalidOperationException("Credential settings are not configured."),
                            cancellationToken).ConfigureAwait(true);
                        // we now have a fresh token in memory. return it to caller
                        return SSConnectionData.ssTokenBearer;
                    }
                    else
                    {
                        // no user interaction required. get a fresh token and return it to caller
                        return await GetTokenFreshAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
    }
    static async Task<string> GetTokenFreshAsync(CancellationToken cancellationToken)
    {
        using (var httpClient = new HttpClient())
        {
            // Authenticate:
            var tokenClient = new OAuth2ServiceClient(SSConnectionData.ssUrl, httpClient);
            // call below will throw an exception if the creds are invalid
            var token = await tokenClient.AuthorizeAsync(
                Grant_type.Password,
                SSConnectionData.ssUsername,
                SSConnectionData.ssPassword,
                null,
                cancellationToken,
                SSConnectionData.ssOTP).ConfigureAwait(false);
            // here we can be sure the creds are ok - return success state                   
            var tokenResult = token.Access_token;

            SSConnectionData.ssTokenBearer = tokenResult;
            SSConnectionData.ssTokenRefresh = token.Refresh_token;
            SSConnectionData.ssTokenExpiresOn = token.Expires_on;
            return tokenResult;
        }
    }
    #endregion


    // input must be the secret id to fetch
    public async Task<ExternalCredential> FetchSecretFromServerAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        // get secret id
        int secretID = int.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

        // init connection credentials, display popup if necessary
        await SSConnectionData.InitAsync(_prompt, _settings, cancellationToken).ConfigureAwait(true);

        // get the secret
        return await FetchSecretAsync(secretID, cancellationToken).ConfigureAwait(false);
    }
}
