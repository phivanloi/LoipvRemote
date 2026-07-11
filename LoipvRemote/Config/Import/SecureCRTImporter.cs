using LoipvRemote.App;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers;
using LoipvRemote.Config.Serializers.MiscSerializers;
using LoipvRemote.Container;
using LoipvRemote.Messages;
using LoipvRemote.Tree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public class SecureCRTImporter : IConnectionImporter<string>
    {
        public void Import(string fileName, ContainerInfo destinationContainer)
        {
            if (fileName == null)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "Unable to import file. File path is null.");
                return;
            }

            if (!File.Exists(fileName))
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    $"Unable to import file. File does not exist. Path: {fileName}");


            FileDataProvider dataProvider = new(fileName);
            string content = dataProvider.Load();
            SecureCRTFileDeserializer deserializer = new();
            ConnectionTreeModel connectionTreeModel = deserializer.Deserialize(content);

            ContainerInfo rootImportContainer = new() { Name = Path.GetFileNameWithoutExtension(fileName) };
            rootImportContainer.AddChildRange(connectionTreeModel.RootNodes.First().Children.ToArray());
            destinationContainer.AddChild(rootImportContainer);
        }
    }


}
