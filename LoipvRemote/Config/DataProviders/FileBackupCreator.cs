using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
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
                // Validate before checking existence. Otherwise a traversal payload
                // that happens not to exist bypasses validation entirely.
                PathValidator.ValidatePathOrThrow(fileName, nameof(fileName));

                if (WeDontNeedToBackup(fileName))
                    return;

                string backupFileName =
                    string.Format(Properties.OptionsBackupPage.Default.BackupFileNameFormat, fileName, DateTime.Now);

                PathValidator.ValidatePathOrThrow(backupFileName, nameof(backupFileName));

                File.Copy(fileName, backupFileName);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"{Language.ConnectionsFileBackupFailed}{Environment.NewLine}{ex}");
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
