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
        IConnectionWorkspace workspace,
        MessageCollector messageCollector)
    {
        private readonly IConnectionWorkspace _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        public void ImportFromFile(ContainerInfo importDestinationContainer)
        {
            try
            {
                using (OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.CheckFileExists = true;
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    openFileDialog.Multiselect = true;

                    List<string> fileTypes = new();
                    fileTypes.AddRange(new[] {Language.FilterAllImportable, "*.xml;*.rdp;*.rdg;*.dat;*.csv"});
                    fileTypes.AddRange(new[] {"LoipvRemote XML (*.xml)", "*.xml"});
                    fileTypes.AddRange(new[] {"LoipvRemote CSV (*.csv)", "*.csv"});
                    fileTypes.AddRange(new[] {Language.FilterRDP, "*.rdp"});
                    fileTypes.AddRange(new[] {Language.FilterRdgFiles, "*.rdg"});
                    fileTypes.AddRange(new[] {Language.FilterPuttyConnectionManager, "*.dat"});
                    fileTypes.AddRange(new[] {Language.FilterAll, "*.*"});
                    fileTypes.AddRange(new[] { Language.FilterSecureCRT, "*.crt" });

                    openFileDialog.Filter = string.Join("|", fileTypes.ToArray());

                    if (openFileDialog.ShowDialog() != DialogResult.OK)
                        return;

					HeadlessFileImport(
						openFileDialog.FileNames,
						importDestinationContainer,
						fileName => MessageBox.Show(string.Format(Language.ImportFileFailedContent, fileName), Language.AskUpdatesMainInstruction,
							MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1));
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("Unable to import file.", ex);
            }
        }

        public void HeadlessFileImport(
	        IEnumerable<string> filePaths,
	        ContainerInfo importDestinationContainer,
	        Action<string>? exceptionAction = null)
        {
	        using (_workspace.BatchedSavingContext())
	        {
		        foreach (string fileName in filePaths)
		        {
			        try
			        {
                        IConnectionImporter<string> importer = BuildConnectionImporterFromFileExtension(fileName);
				        importer.Import(fileName, importDestinationContainer);
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

        private IConnectionImporter<string> BuildConnectionImporterFromFileExtension(string fileName)
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
