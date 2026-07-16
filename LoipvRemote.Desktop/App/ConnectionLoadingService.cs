using LoipvRemote.App.Info;
using LoipvRemote.Container;
using LoipvRemote.Connection;
using LoipvRemote.Messages;
using LoipvRemote.Properties;
using LoipvRemote.Resources.Language;
using LoipvRemote.Tools;
using LoipvRemote.UI;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.TaskDialog;
using System.IO;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace LoipvRemote.App;

/// <summary>Connection-file workflow owned by the host instead of the Runtime bridge.</summary>
[SupportedOSPlatform("windows")]
public sealed class ConnectionLoadingService(
    IConnectionTreeWorkspace workspace,
    MessageCollector messageCollector,
    ConnectionWorkspaceAdapter connectionWorkspace,
    ConnectionImportService connectionImportService,
    Func<ContainerInfo> connectionRootProvider)
{
    private readonly IConnectionTreeWorkspace _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
    private readonly ConnectionWorkspaceAdapter _connectionWorkspace = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
    private readonly ConnectionImportService _connectionImportService = connectionImportService ?? throw new ArgumentNullException(nameof(connectionImportService));
    private readonly Func<ContainerInfo> _connectionRootProvider = connectionRootProvider ?? throw new ArgumentNullException(nameof(connectionRootProvider));

    public Task LoadConnectionsAsync() => LoadConnectionsAsync(false);

    public async Task LoadConnectionsAsync(bool withDialog = false)
    {
        string connectionFileName = string.Empty;

        try
        {
            _workspace.DisableRemoteSynchronization();

            if (withDialog)
            {
                OpenFileDialog loadDialog = DialogFactory.BuildLoadConnectionsDialog();
                if (loadDialog.ShowDialog() != DialogResult.OK)
                    return;

                connectionFileName = loadDialog.FileName;
                OptionsDBsPage.Default.UseSQLServer = false;
                OptionsDBsPage.Default.Save();
            }
            else if (!OptionsDBsPage.Default.UseSQLServer)
            {
                connectionFileName = _workspace.GetStartupConnectionFileName();
            }

            await _workspace.LoadConnectionsAsync(OptionsDBsPage.Default.UseSQLServer, false, connectionFileName).ConfigureAwait(true);
            if (OptionsDBsPage.Default.UseSQLServer)
                _workspace.LastSqlUpdate = DateTime.Now.ToUniversalTime();
            else
                _workspace.LastFileUpdate = File.GetLastWriteTime(connectionFileName);

            _workspace.EnableRemoteSynchronization();
        }
        catch (Exception exception)
        {
            ProgramRoot.CloseSplash();

            if (OptionsDBsPage.Default.UseSQLServer)
            {
                _messageCollector.AddExceptionMessage(Language.LoadFromSqlFailed, exception);
                string commandButtons = string.Join("|", Language._TryAgain, Language.CommandOpenConnectionFile,
                    FormatText(Language.CommandExitProgram, Application.ProductName));
                CTaskDialog.ShowCommandBox(Application.ProductName ?? string.Empty, Language.LoadFromSqlFailed,
                    Language.LoadFromSqlFailedContent, MiscTools.GetExceptionMessageRecursive(exception), "", "",
                    commandButtons, false, ESysIcons.Error, ESysIcons.Error);

                switch (CTaskDialog.CommandButtonResult)
                {
                    case 0:
                        OptionsDBsPage.Default.UseSQLServer = true;
                        await LoadConnectionsAsync(withDialog);
                        return;
                    case 1:
                        OptionsDBsPage.Default.UseSQLServer = false;
                        await LoadConnectionsAsync(true);
                        return;
                    default:
                        OptionsDBsPage.Default.UseSQLServer = false;
                        OptionsDBsPage.Default.Save();
                        Application.Exit();
                        return;
                }
            }

            if (exception is FileNotFoundException && !withDialog)
            {
                _messageCollector.AddExceptionMessage(
                    FormatText(Language.ConnectionsFileCouldNotBeLoadedNew, connectionFileName),
                    exception,
                    MessageClass.InformationMsg);

                string[] commandButtons =
                [
                    Language.ConfigurationCreateNew,
                    Language.ConfigurationCustomPath,
                    Language.ConfigurationImportFile,
                    Language.Exit
                ];

                bool answered = false;
                while (!answered)
                {
                    try
                    {
                        CTaskDialog.ShowTaskDialogBox(GeneralAppInfo.ProductName ?? string.Empty,
                            Language.ConnectionFileNotFound, "", "", "", "", "",
                            string.Join(" | ", commandButtons), ETaskDialogButtons.None, ESysIcons.Question,
                            ESysIcons.Question);

                        switch (CTaskDialog.CommandButtonResult)
                        {
                            case 0:
                                await _workspace.NewConnectionsFileAsync(connectionFileName);
                                answered = true;
                                break;
                            case 1:
                                await LoadConnectionsAsync(true);
                                answered = true;
                                break;
                            case 2:
                                await _workspace.NewConnectionsFileAsync(connectionFileName);
                                await _connectionImportService.ImportFromFileAsync(_connectionRootProvider());
                                answered = true;
                                break;
                            case 3:
                                Application.Exit();
                                answered = true;
                                break;
                        }
                    }
                    catch (Exception retryException)
                    {
                        _messageCollector.AddExceptionMessage(
                            FormatText(Language.ConnectionsFileCouldNotBeLoadedNew, connectionFileName),
                            retryException,
                            MessageClass.InformationMsg);
                    }
                }

                return;
            }

            _messageCollector.AddExceptionStackTrace(
                FormatText(Language.ConnectionsFileCouldNotBeLoaded, connectionFileName), exception);
            if (connectionFileName != _workspace.GetStartupConnectionFileName())
            {
                await LoadConnectionsAsync(withDialog);
            }
            else
            {
                _connectionWorkspace.ShowError(
                    FormatText(Language.ErrorStartupConnectionFileLoad, Environment.NewLine, Application.ProductName,
                        _workspace.GetStartupConnectionFileName(), MiscTools.GetExceptionMessageRecursive(exception)),
                    @"Could not load startup file.");
                Application.Exit();
            }
        }
    }
}
