namespace LoipvRemote.Connectors.Abstractions;

/// <summary>UI-independent input required to authenticate against Passwordstate.</summary>
public sealed record PasswordstatePromptRequest(
    string ServerUrl,
    string ApiKey,
    string OneTimePassword,
    bool UseSso);

public sealed record PasswordstatePromptResult(
    string ServerUrl,
    string ApiKey,
    string OneTimePassword,
    bool UseSso);

/// <summary>UI-independent input required to authenticate against Delinea Secret Server.</summary>
public sealed record DelineaPromptRequest(
    string ServerUrl,
    string Username,
    string Password,
    string OneTimePassword,
    bool UseSso);

public sealed record DelineaPromptResult(
    string ServerUrl,
    string Username,
    string Password,
    string OneTimePassword,
    bool UseSso);

/// <summary>UI-independent input required to authenticate against OpenBao.</summary>
public sealed record OpenBaoPromptRequest(string Url, string Token);

public sealed record OpenBaoPromptResult(string Url, string Token);

/// <summary>UI-independent input required to authenticate against AWS.</summary>
public sealed record AwsPromptRequest(string AccessKeyId, string SecretKey);

public sealed record AwsPromptResult(string AccessKeyId, string SecretKey);

/// <summary>Provides credential prompts without coupling connector runtime to a UI toolkit.</summary>
public interface IExternalCredentialPrompt
{
    Task<PasswordstatePromptResult?> PromptPasswordstateAsync(
        PasswordstatePromptRequest request,
        CancellationToken cancellationToken = default);

    Task<DelineaPromptResult?> PromptDelineaAsync(
        DelineaPromptRequest request,
        CancellationToken cancellationToken = default);

    Task<OpenBaoPromptResult?> PromptOpenBaoAsync(
        OpenBaoPromptRequest request,
        CancellationToken cancellationToken = default);

    Task<AwsPromptResult?> PromptAwsAsync(
        AwsPromptRequest request,
        CancellationToken cancellationToken = default);
}
