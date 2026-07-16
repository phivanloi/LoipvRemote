using System;
using LoipvRemote.Tree;

namespace LoipvRemote.Config.Connections
{
    public class ConnectionsSavedEvent
    {
        public ConnectionTreeModel ModelThatWasSaved { get; }
        public bool PreviouslyUsingDatabase { get; }
        public bool UsingDatabase { get; }
        public string ConnectionFileName { get; }

        public ConnectionsSavedEvent(ConnectionTreeModel modelThatWasSaved,
                                         bool previouslyUsingDatabase,
                                         bool usingDatabase,
                                         string connectionFileName)
        {
            ArgumentNullException.ThrowIfNull(modelThatWasSaved);

            ModelThatWasSaved = modelThatWasSaved;
            PreviouslyUsingDatabase = previouslyUsingDatabase;
            UsingDatabase = usingDatabase;
            ConnectionFileName = connectionFileName;
        }
    }
}
