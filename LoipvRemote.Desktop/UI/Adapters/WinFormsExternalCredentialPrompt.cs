using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Desktop.UI.Connectors.AWS;
using LoipvRemote.Desktop.UI.Connectors.Delinea;
using LoipvRemote.Desktop.UI.Connectors.OpenBao;
using LoipvRemote.Desktop.UI.Connectors.Passwordstate;

namespace LoipvRemote.Desktop.UI.Adapters;

/// <summary>WinForms implementation of the connector credential prompt boundary.</summary>
public sealed class WinFormsExternalCredentialPrompt : IExternalCredentialPrompt
{
    public Task<PasswordstatePromptResult?> PromptPasswordstateAsync(
        PasswordstatePromptRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using CPSConnectionForm form = new();
        form.tbServerURL.Text = request.ServerUrl;
        form.tbAPIKey.Text = request.ApiKey;
        form.tbOTP.Text = request.OneTimePassword;
        form.cbUseSSO.Checked = request.UseSso;
        PasswordstatePromptResult? result = form.ShowDialog() == DialogResult.OK
            ? new PasswordstatePromptResult(form.tbServerURL.Text, form.tbAPIKey.Text, form.tbOTP.Text, form.cbUseSSO.Checked)
            : null;
        return Task.FromResult(result);
    }

    public Task<DelineaPromptResult?> PromptDelineaAsync(
        DelineaPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using SSConnectionForm form = new();
        form.tbSSURL.Text = request.ServerUrl;
        form.tbUsername.Text = request.Username;
        form.tbPassword.Text = request.Password;
        form.tbOTP.Text = request.OneTimePassword;
        form.cbUseSSO.Checked = request.UseSso;
        DelineaPromptResult? result = form.ShowDialog() == DialogResult.OK
            ? new DelineaPromptResult(form.tbSSURL.Text, form.tbUsername.Text, form.tbPassword.Text, form.tbOTP.Text, form.cbUseSSO.Checked)
            : null;
        return Task.FromResult(result);
    }

    public Task<OpenBaoPromptResult?> PromptOpenBaoAsync(
        OpenBaoPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using VaultOpenbaoConnectionForm form = new();
        form.tbUrl.Text = request.Url;
        form.tbToken.Text = request.Token;
        OpenBaoPromptResult? result = form.ShowDialog() == DialogResult.OK
            ? new OpenBaoPromptResult(form.tbUrl.Text, form.tbToken.Text)
            : null;
        return Task.FromResult(result);
    }

    public Task<AwsPromptResult?> PromptAwsAsync(
        AwsPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using AWSConnectionForm form = new();
        form.tbAccesKeyID.Text = request.AccessKeyId;
        form.tbAccesKey.Text = request.SecretKey;
        AwsPromptResult? result = form.ShowDialog() == DialogResult.OK
            ? new AwsPromptResult(form.tbAccesKeyID.Text, form.tbAccesKey.Text)
            : null;
        return Task.FromResult(result);
    }
}
