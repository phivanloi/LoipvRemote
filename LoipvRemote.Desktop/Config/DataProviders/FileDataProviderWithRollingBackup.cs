using System.Runtime.Versioning;

namespace LoipvRemote.Config.DataProviders
{
    [SupportedOSPlatform("windows")]
    public class FileDataProviderWithRollingBackup(string filePath) : FileDataProvider(filePath)
    {
        public override void Save(string content)
        {
            FileBackupCreator.CreateBackupFile(FilePath);
            base.Save(content);
        }
    }
}
