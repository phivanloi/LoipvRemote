using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Config.Connections;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Csv;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Credential;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.Messages;
using LoipvRemote.Security;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Adapters;
using LoipvRemote.Tools;
using LoipvRemote.UseCases.Configuration;


namespace LoipvRemote.App
{
    [SupportedOSPlatform("windows")]
    public sealed class ConnectionExportService(
        ConnectionWorkspaceAdapter connectionWorkspace,
        MessageCollector messageCollector,
        IConnectionTreeWorkspace workspace,
        ICredentialRepositoryList credentialRepositoryList,
        ExternalToolsService externalToolsService)
    {
        private readonly ConnectionWorkspaceAdapter _connectionWorkspace = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
        private readonly IConnectionTreeWorkspace _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        private readonly ICredentialRepositoryList _credentialRepositoryList = credentialRepositoryList ?? throw new ArgumentNullException(nameof(credentialRepositoryList));
        private readonly ExternalToolsService _externalToolsService = externalToolsService ?? throw new ArgumentNullException(nameof(externalToolsService));

        public void ExportToFile(ConnectionInfo selectedNode, ConnectionTreeModel connectionTreeModel)
        {
            try
            {
                SaveFilter saveFilter = new();

                using (FrmExport exportForm = new())
                {
                    if (selectedNode?.GetTreeNodeType() == TreeNodeType.Container)
                        // node type is Container, so the cast is guaranteed to succeed
                        exportForm.SelectedFolder = (ContainerInfo)selectedNode;
                    else if (selectedNode?.GetTreeNodeType() == TreeNodeType.Connection)
                    {
                        if (selectedNode.Parent.GetTreeNodeType() == TreeNodeType.Container)
                            exportForm.SelectedFolder = selectedNode.Parent;
                        exportForm.SelectedConnection = selectedNode;
                    }

                    if (_connectionWorkspace.ShowDialog(exportForm) != DialogResult.OK)
                        return;

                    ConnectionInfo? exportTarget;
                    switch (exportForm.Scope)
                    {
                        case FrmExport.ExportScope.SelectedFolder:
                            exportTarget = exportForm.SelectedFolder;
                            break;
                        case FrmExport.ExportScope.SelectedConnection:
                            exportTarget = exportForm.SelectedConnection;
                            break;
                        default:
                            exportTarget = connectionTreeModel.RootNodes.First(node => node is RootNodeInfo);
                            break;
                    }

                    if (exportTarget == null)
                        return;

                    saveFilter.SaveUsername = exportForm.IncludeUsername;
                    saveFilter.SavePassword = exportForm.IncludePassword;
                    saveFilter.SaveDomain = exportForm.IncludeDomain;
                    saveFilter.SaveInheritance = exportForm.IncludeInheritance;
                    saveFilter.SaveCredentialId = exportForm.IncludeAssignedCredential;

                    SaveExportFile(exportForm.FileName, exportForm.SaveFormat, saveFilter, exportTarget);
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("App.Export.ExportToFile() failed.", ex);
            }
        }

        private void SaveExportFile(string fileName,
                                    SaveFormat saveFormat,
                                    SaveFilter saveFilter,
                                    ConnectionInfo exportTarget)
        {
            try
            {
                switch (saveFormat)
                {
                    case SaveFormat.Xml:
                        SaveCanonicalXmlFile(fileName, exportTarget);
                        break;
                    case SaveFormat.Csv:
                        CsvConnectionsSerializer serializer =
                            new CsvConnectionsSerializer(saveFilter, _credentialRepositoryList);
                        string serializedData = serializer.Serialize(exportTarget);
                        new FileDataProvider(fileName).Save(serializedData);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(saveFormat), saveFormat, null);
                }

            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace($"Export.SaveExportFile(\"{fileName}\") failed.", ex);
            }
            finally
            {
                _workspace.EnableRemoteSynchronization();
            }
        }

        private void SaveCanonicalXmlFile(string fileName, ConnectionInfo exportTarget)
        {
            ConnectionTreeDefinition definition;
            if (exportTarget is ContainerInfo container)
            {
                definition = ConnectionDefinitionMapper.ToDomainTree(
                    [container],
                    externalApplicationResolver: ResolveExternalApplication);
            }
            else
            {
                ConnectionDefinition connection = ConnectionDefinitionMapper.ToDomain(
                    exportTarget,
                    externalApplicationResolver: ResolveExternalApplication);
                definition = new ConnectionTreeDefinition([], [connection]);
            }

            new XmlConnectionDefinitionStore(fileName)
                .SaveAsync(definition)
                .GetAwaiter()
                .GetResult();
        }

        private ExternalApplicationDefinition? ResolveExternalApplication(ConnectionInfo connection) =>
            _externalToolsService.GetExtAppByName(connection.ExtApp)?.ToDefinition(connection);
    }
}
