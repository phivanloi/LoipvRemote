using System;
using System.IO;
using System.Runtime.Versioning;
using LoipvRemote.App;
using LoipvRemote.Messages;
using LoipvRemote.Resources.Language;
using LoipvRemote.Tools;

namespace LoipvRemote.Config.DataProviders
{
    public class FileBackupCreator
    {
        [SupportedOSPlatform("windows")]
        public void CreateBackupFile(string fileName)
        {
            try
            {
                if (WeDontNeedToBackup(fileName))
                    return;

                PathValidator.ValidatePathOrThrow(fileName, nameof(fileName));

                string backupFileName =
                    string.Format(Properties.OptionsBackupPage.Default.BackupFileNameFormat, fileName, DateTime.Now);

                PathValidator.ValidatePathOrThrow(backupFileName, nameof(backupFileName));

                File.Copy(fileName, backupFileName);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.ConnectionsFileBackupFailed, ex,
                                                             MessageClass.WarningMsg);
                throw;
            }
        }

        private bool WeDontNeedToBackup(string filePath)
        {
            return FeatureIsTurnedOff() || FileDoesntExist(filePath);
        }

        private bool FileDoesntExist(string filePath)
        {
            return !File.Exists(filePath);
        }

        private bool FeatureIsTurnedOff()
        {
            return Properties.OptionsBackupPage.Default.BackupFileKeepCount == 0;
        }
    }
}