using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Web;
using LoipvRemote.Connectors.Abstractions;

namespace LoipvRemote.Connectors.OnePassword;

public class OnePasswordCliException(string message, string arguments) : Exception(message)
{
	public string Arguments { get; set; } = arguments;
}

public class OnePasswordCli
{
	private const string OnePasswordCliExecutable = "op.exe";

	// Username / password purpose metadata is used on Login category item fields
	private const string UserNamePurpose = "USERNAME";
	private const string PasswordPurpose = "PASSWORD";
	
	// Server category items (and perhaps others) do have a built-in username/password field but don't have the `purpose` set
	// and because it's a built-in field this can't be set afterwards.
	// We use the label for as fallback because that can be user-modified to fit this convention in all cases.
	private const string UserNameLabel = "username";
	private const string PasswordLabel = "password";
	
	
	private const string StringType = "STRING";
	private const string SshKeyType = "SSHKEY";
	private const string DomainLabel = "domain";

	private sealed record VaultUrl(string Label, string Href);

	private sealed record VaultField(string Id, string Label, string Type, string Purpose, string Value);

	private sealed record VaultItem(VaultUrl[]? Urls, VaultField[]? Fields);

	private static readonly JsonSerializerOptions JsonSerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public static async Task<ExternalCredential> ReadPasswordAsync(
		string input,
		CancellationToken cancellationToken = default)
	{
		var inputUrl = new Uri(input);
		var vault = WebUtility.UrlDecode(inputUrl.Host);
		var queryParams = HttpUtility.ParseQueryString(inputUrl.Query);
		var account = queryParams["account"];
		var item = WebUtility.UrlDecode(inputUrl.AbsolutePath.TrimStart('/'));
		return await ItemGetAsync(item, vault, account, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<ExternalCredential> ItemGetAsync(
		string item,
		string? vault,
		string? account,
		CancellationToken cancellationToken)
    {
        var args = new List<string> { "item", "get", item };

        if (!string.IsNullOrEmpty(account))
        {
            args.Add("--account");
            args.Add(account);
        }

        if (!string.IsNullOrEmpty(vault))
        {
            args.Add("--vault");
            args.Add(vault);
        }

        args.Add("--format");
        args.Add("json");

		string commandLine = OnePasswordCliExecutable + " " + string.Join(' ', args);
            
		(int exitCode, string output, string error) = await RunCommandAsync(
			OnePasswordCliExecutable,
			args,
			cancellationToken).ConfigureAwait(false);
		if (exitCode != 0)
		{
			throw new OnePasswordCliException($"Error running op item get: {error}",
                commandLine);
        }

        var items = JsonSerializer.Deserialize<VaultItem>(output, JsonSerializerOptions) ??
                    throw new OnePasswordCliException("1Password returned null",
                        commandLine);
		string username = FindField(items, UserNamePurpose, UserNameLabel);
		string password = FindField(items, PasswordPurpose, PasswordLabel);
		string privateKey = items.Fields?.FirstOrDefault(x => x.Type == SshKeyType)?.Value ?? string.Empty;
		string domain = items.Fields?.FirstOrDefault(x => x.Type == StringType && x.Label == DomainLabel)?.Value ?? string.Empty;
		if (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(privateKey))
		{
				throw new OnePasswordCliException("No secret found in 1Password. At least fields with labels username/password or a SshKey are expected.", commandLine);
		}

		return new ExternalCredential(username, password, domain, privateKey);
    }

    private static string FindField(VaultItem items, string purpose, string fallbackLabel)
    {
        return items.Fields?.FirstOrDefault(x => x.Purpose == purpose)?.Value ??
			items.Fields?.FirstOrDefault(x => x.Type == StringType && string.Equals(x.Id, fallbackLabel, StringComparison.OrdinalIgnoreCase))?.Value ??
			items.Fields?.FirstOrDefault(x => x.Type == StringType && string.Equals(x.Label, fallbackLabel, StringComparison.OrdinalIgnoreCase))?.Value ??
		 	string.Empty;
    }

	private static async Task<(int ExitCode, string Output, string Error)> RunCommandAsync(
		string command,
		IReadOnlyCollection<string> arguments,
		CancellationToken cancellationToken)
	{
		var processStartInfo = new ProcessStartInfo
		{
			FileName = command,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		foreach (var argument in arguments)
		{
			processStartInfo.ArgumentList.Add(argument);
		}

		using var process = new Process();
		process.StartInfo = processStartInfo;
		process.Start();
		Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
		string output = await outputTask.ConfigureAwait(false);
		string error = await errorTask.ConfigureAwait(false);
		return (process.ExitCode, output, error);
	}
}
