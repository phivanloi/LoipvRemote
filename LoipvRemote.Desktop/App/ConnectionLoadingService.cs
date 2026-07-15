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
using LoipvRemote.UseCases.Configuration;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

namespace LoipvRemote.App;

/// <summary>Connection-file workflow owned by the host instead of the Runtime bridge.</summary>
[SupportedOSPlatform("windows")]
public sealed class ConnectionLoadingService(
    IConnectionWorkspace workspace,
    MessageCollector messageCollector,
    ConnectionWorkspaceAdapter connectionWorkspace,
    ConnectionImportService connectionImportService,
    Func<ContainerInfo> connectionRootProvider)
{
    private readonly IConnectionWorkspace _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
    private readonly ConnectionWorkspaceAdapter _connectionWorkspace = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
    private readonly ConnectionImportService _connectionImportService = connectionImportService ?? throw new ArgumentNullException(nameof(connectionImportService));
    private readonly Func<ContainerInfo> _connectionRootProvider = connectionRootProvider ?? throw new ArgumentNullException(nameof(connectionRootProvider));

    public void LoadConnectionsAsync()
    {
        Thread thread = new(() => LoadConnections());
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void LoadConnections(bool withDialog = false)
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

            _workspace.LoadConnections(OptionsDBsPage.Default.UseSQLServer, false, connectionFileName);
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
                    string.Format(Language.CommandExitProgram, Application.ProductName));
                CTaskDialog.ShowCommandBox(Application.ProductName ?? string.Empty, Language.LoadFromSqlFailed,
                    Language.LoadFromSqlFailedContent, MiscTools.GetExceptionMessageRecursive(exception), "", "",
                    commandButtons, false, ESysIcons.Error, ESysIcons.Error);

                switch (CTaskDialog.CommandButtonResult)
                {
                    case 0:
                        OptionsDBsPage.Default.UseSQLServer = true;
                        LoadConnections(withDialog);
                        return;
                    case 1:
                        OptionsDBsPage.Default.UseSQLServer = false;
                        LoadConnections(true);
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
                    string.Format(Language.ConnectionsFileCouldNotBeLoadedNew, connectionFileName),
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
                                _workspace.NewConnectionsFile(connectionFileName);
                                answered = true;
                                break;
                            case 1:
                                LoadConnections(true);
                                answered = true;
                                break;
                            case 2:
                                _workspace.NewConnectionsFile(connectionFileName);
                                _connectionImportService.ImportFromFile(_connectionRootProvider());
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
                            string.Format(Language.ConnectionsFileCouldNotBeLoadedNew, connectionFileName),
                            retryException,
                            MessageClass.InformationMsg);
                    }
                }

                return;
            }

            _messageCollector.AddExceptionStackTrace(
                string.Format(Language.ConnectionsFileCouldNotBeLoaded, connectionFileName), exception);
            if (connectionFileName != _workspace.GetStartupConnectionFileName())
            {
                LoadConnections(withDialog);
            }
            else
            {
                _connectionWorkspace.ShowError(
                    string.Format(Language.ErrorStartupConnectionFileLoad, Environment.NewLine, Application.ProductName,
                        _workspace.GetStartupConnectionFileName(), MiscTools.GetExceptionMessageRecursive(exception)),
                    @"Could not load startup file.");
                Application.Exit();
            }
        }
    }
}
