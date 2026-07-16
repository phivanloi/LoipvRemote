using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using LoipvRemote.Config.Import;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Messages;
using LoipvRemote.Tools;
using LoipvRemote.Resources.Language;
using LoipvRemote.UseCases.Configuration;
using System.Runtime.Versioning;

namespace LoipvRemote.App
{
    [SupportedOSPlatform("windows")]
    public sealed class ConnectionImportService(
        IConnectionTreeWorkspace workspace,
        MessageCollector messageCollector)
    {
        private readonly IConnectionTreeWorkspace _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        public async Task ImportFromFileAsync(
            ContainerInfo importDestinationContainer,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using (OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.CheckFileExists = true;
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    openFileDialog.Multiselect = true;

                    List<string> fileTypes = new();
                    fileTypes.Add(Language.FilterAllImportable);
                    fileTypes.Add("*.xml;*.rdp;*.rdg;*.dat;*.csv");
                    fileTypes.Add("LoipvRemote XML (*.xml)");
                    fileTypes.Add("*.xml");
                    fileTypes.Add("LoipvRemote CSV (*.csv)");
                    fileTypes.Add("*.csv");
                    fileTypes.Add(Language.FilterRDP);
                    fileTypes.Add("*.rdp");
                    fileTypes.Add(Language.FilterRdgFiles);
                    fileTypes.Add("*.rdg");
                    fileTypes.Add(Language.FilterPuttyConnectionManager);
                    fileTypes.Add("*.dat");
                    fileTypes.Add(Language.FilterAll);
                    fileTypes.Add("*.*");
                    fileTypes.Add(Language.FilterSecureCRT);
                    fileTypes.Add("*.crt");

                    openFileDialog.Filter = string.Join("|", fileTypes.ToArray());

                    if (openFileDialog.ShowDialog() != DialogResult.OK)
                        return;

                    await HeadlessFileImportAsync(
                        openFileDialog.FileNames,
                        importDestinationContainer,
                        fileName => MessageBox.Show(FormatText(Language.ImportFileFailedContent, fileName), Language.AskUpdatesMainInstruction,
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1),
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("Unable to import file.", ex);
            }
        }

        public async Task HeadlessFileImportAsync(
            IEnumerable<string> filePaths,
            ContainerInfo importDestinationContainer,
            Action<string>? exceptionAction = null,
            CancellationToken cancellationToken = default)
        {
            using (_workspace.BatchedSavingContext())
            {
                foreach (string fileName in filePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        object importer = BuildConnectionImporterFromFileExtension(fileName);
                        if (importer is LoipvRemoteXmlImporter xmlImporter)
                        {
                            await xmlImporter.ImportAsync(fileName, importDestinationContainer, cancellationToken)
                                .ConfigureAwait(true);
                        }
                        else if (importer is IConnectionImporter<string> syncImporter)
                        {
                            syncImporter.Import(fileName, importDestinationContainer);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Importer '{importer.GetType().FullName}' is not supported.");
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptionAction?.Invoke(fileName);
                        _messageCollector.AddExceptionMessage($"Error occurred while importing file '{fileName}'.", ex);
                    }
                }
            }
        }

        public void ImportFromActiveDirectory(string ldapPath,
                                                     ContainerInfo importDestinationContainer,
                                                     bool importSubOu)
        {
            try
            {
                using (_workspace.BatchedSavingContext())
                {
                    new ActiveDirectoryImporter(_messageCollector).Import(ldapPath, importDestinationContainer, importSubOu);
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("App.Import.ImportFromActiveDirectory() failed.", ex);
            }
        }

        public void ImportFromPortScan(IEnumerable<ScanHost> hosts,
                                              ProtocolKind protocol,
                                              ContainerInfo importDestinationContainer)
        {
            try
            {
                using (_workspace.BatchedSavingContext())
                {
                    PortScanImporter importer = new(protocol);
                    importer.Import(hosts, importDestinationContainer);
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("App.Import.ImportFromPortScan() failed.", ex);
            }
        }

        internal void ImportFromPutty(ContainerInfo selectedNodeAsContainer)
        {
            try
            {
                using (_workspace.BatchedSavingContext())
                {
                    new RegistryImporter(_messageCollector).ImportFromRegistry("Software\\SimonTatham\\PuTTY\\Sessions", selectedNodeAsContainer);
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("App.Import.ImportFromPutty() failed.", ex);
            }
        }

        private object BuildConnectionImporterFromFileExtension(string fileName)
        {
            string extension = Path.GetExtension(fileName) ?? "";
            switch (extension.ToLowerInvariant())
            {
                case ".xml":
                    return new LoipvRemoteXmlImporter(_messageCollector);
                case ".csv":
                    return new LoipvRemoteCsvImporter(_messageCollector);
                case ".rdp":
                    return new RemoteDesktopConnectionImporter();
                case ".rdg":
                    return new RemoteDesktopConnectionManagerImporter();
                case ".dat":
                    return new PuttyConnectionManagerImporter();
                case ".crt":
                    return new SecureCRTImporter(_messageCollector);
                default:
                    throw new FileFormatException("Unrecognized file format.");
            }
        }
    }
}
