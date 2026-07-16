using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Connectors.Passwordstate;

public class PasswordstateInterface
{
    private readonly IExternalCredentialPrompt _prompt;
    private readonly IExternalCredentialSettingsStore _settings;

    public PasswordstateInterface(
        IExternalCredentialPrompt prompt,
        IExternalCredentialSettingsStore settings)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    private static class CPSConnectionData
    {
        public static string ssUsername = "";
        public static string ssPassword = "";
        public static string ssUrl = "";
        public static string ssOTP = "";
        public static DateTime ssOTPTimeStampExpiration;

        public static bool ssSSO;
        public static bool initdone;

        //token 
        //public static string ssTokenBearer = "";
        //public static DateTime ssTokenExpiresOn = DateTime.UtcNow;
        //public static string ssTokenRefresh = "";

        public static async Task InitAsync(
            IExternalCredentialPrompt prompt,
            IExternalCredentialSettingsStore settings,
            CancellationToken cancellationToken)
        {
            // 2024-05-04 passwordstate currently does not support auth tokens, so we need to re-enter otp codes frequently
            if (!string.IsNullOrEmpty(ssOTP) && DateTime.Now > ssOTPTimeStampExpiration)
            {
                ssOTP = "";
                initdone = false;
            }

            if (initdone == true)
                return;

            try
            {
                string? url = settings.GetString("Passwordstate", "URL");
                if (url == null || !url.Contains("://"))
                    url = "https://cred.domain.local/SecretServer";

                ssSSO = settings.GetBoolean("Passwordstate", "SSO");
                
                while (true)
                {
                    PasswordstatePromptResult? result = await prompt.PromptPasswordstateAsync(
                        new PasswordstatePromptRequest(url, ssPassword, ssOTP, ssSSO),
                        cancellationToken).ConfigureAwait(true);
                    if (result is null)
                        return;

                    // store values to memory
                    ssPassword = result.ApiKey;
                    ssUrl = result.ServerUrl;
                    url = ssUrl;
                    ssSSO = result.UseSso;
                    ssOTP = result.OneTimePassword;
                    ssOTPTimeStampExpiration = DateTime.Now.AddSeconds(30);
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


                settings.SetString("Passwordstate", "URL", ssUrl);
                settings.SetBoolean("Passwordstate", "SSO", ssSSO);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    private static Task<bool> TestCredentialsAsync(CancellationToken cancellationToken)
    {
        return ConnectionTestAsync(cancellationToken);
    }
    private static async Task<bool> ConnectionTestAsync(CancellationToken cancellationToken)
    {
        if (CPSConnectionData.ssSSO)
        {
            string url = $"{CPSConnectionData.ssUrl}/winapi/passwordlists/";

            using HttpClient client = new HttpClient(new HttpClientHandler() { UseDefaultCredentials = true });
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", "LoipvRemote");
            client.DefaultRequestHeaders.Add("OTP", CPSConnectionData.ssOTP);

            var json = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            JsonNode? data = JsonSerializer.Deserialize<JsonNode>(json);
            if (data == null)
                return false;
            return true;
        }
        else
        {
            string url = $"{CPSConnectionData.ssUrl}/api/passwordlists/";
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", "LoipvRemote");
            client.DefaultRequestHeaders.Add("APIKey", CPSConnectionData.ssPassword);
            client.DefaultRequestHeaders.Add("OTP", CPSConnectionData.ssOTP);

            var json = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            JsonNode? data = JsonSerializer.Deserialize<JsonNode>(json);
            if (data == null)
                return false;
            return true;
        }
    }

    private static async Task<JsonNode?> FetchDataWinAuthAsync(int secretID, CancellationToken cancellationToken)
    {
        string url = $"{CPSConnectionData.ssUrl}/winapi/passwords/{secretID}";

        using HttpClient client = new HttpClient(new HttpClientHandler() { UseDefaultCredentials = true });
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "LoipvRemote");
        client.DefaultRequestHeaders.Add("OTP", CPSConnectionData.ssOTP);

        var json = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        JsonNode? data = JsonSerializer.Deserialize<JsonNode>(json);
        if (data == null)
            return null;
        JsonNode? element = data[0];
        return element;
    }
    private static async Task<JsonNode?> FetchDataAPIKeyAuthAsync(int secretID, CancellationToken cancellationToken)
    {
        string url = $"{CPSConnectionData.ssUrl}/api/passwords/{secretID}";

        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "LoipvRemote");
        client.DefaultRequestHeaders.Add("APIKey", CPSConnectionData.ssPassword);
        client.DefaultRequestHeaders.Add("OTP", CPSConnectionData.ssOTP);

        var json = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        JsonNode? data = JsonSerializer.Deserialize<JsonNode>(json);
        if (data == null)
            return null;
        JsonNode? element = data[0];
        return element;
    }

    private static async Task<ExternalCredential> FetchSecretAsync(int secretID, CancellationToken cancellationToken)
    {
        // clear return variables
        string secretDomain = "";
        string secretUsername = "";
        string secretPassword = "";
        string privatekey = "";
        string privatekeypassphrase = "";
        JsonNode? element = null;

        if (CPSConnectionData.ssSSO)
            element = await FetchDataWinAuthAsync(secretID, cancellationToken).ConfigureAwait(false);
        else
            element = await FetchDataAPIKeyAuthAsync(secretID, cancellationToken).ConfigureAwait(false);

        if (element == null)
            return new ExternalCredential(secretUsername, secretPassword, secretDomain, privatekey);

        var dom = element["Domain"];
        if (dom != null) secretDomain = dom.ToString();

        var user = element["UserName"];
        if (user != null) secretUsername = user.ToString();

        var pw = element["Password"];
        if (pw != null) secretPassword = pw.ToString();

        var privkey = element["GenericField1"];
        if (privkey != null) privatekey = privkey.ToString();

        var phrase = element["GenericField3"];
        if (phrase != null) privatekeypassphrase = phrase.ToString();

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
        PemReader pemReader = new PemReader(textReader, new PasswordFinder(password));

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
        PemReader pr = new PemReader(new StringReader(pem));
        AsymmetricCipherKeyPair KeyPair = (AsymmetricCipherKeyPair)pr.ReadObject();
        RSAParameters rsaParams = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)KeyPair.Private);
        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(rsaParams);

        if (rsa.KeySize < 2048)
        {
            rsa.Dispose();
            throw new CryptographicException("The imported RSA private key must be at least 2048 bits.");
        }
        return rsa;
    }
    #endregion


    // input: must be the secret id to fetch
    public async Task<ExternalCredential> FetchSecretFromServerAsync(
        string secretID,
        CancellationToken cancellationToken = default)
    {
        // get secret id
        int sid = int.Parse(secretID, System.Globalization.CultureInfo.InvariantCulture);

        // init connection credentials, display popup if necessary
        await CPSConnectionData.InitAsync(_prompt, _settings, cancellationToken).ConfigureAwait(true);

        // get the secret
        return await FetchSecretAsync(sid, cancellationToken).ConfigureAwait(false);
    }
}
