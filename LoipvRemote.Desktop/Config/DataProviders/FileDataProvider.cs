using System;
using System.IO;
using System.Runtime.Versioning;
using System.Diagnostics;
using LoipvRemote.Tools;

namespace LoipvRemote.Config.DataProviders
{
    [SupportedOSPlatform("windows")]
    public class FileDataProvider : IDataProvider<string>
    {
        private string _filePath;

        [SupportedOSPlatform("windows")]
        public string FilePath
        {
            get => _filePath;
            set
            {
                PathValidator.ValidatePathOrThrow(value, nameof(FilePath));
                _filePath = value;
            }
        }

        public FileDataProvider(string filePath)
        {
            PathValidator.ValidatePathOrThrow(filePath, nameof(filePath));
            _filePath = filePath;
        }

        public virtual string Load()
        {
            string fileContents = "";
            try
            {
                if (!File.Exists(FilePath))
                {
                    CreateMissingDirectories();
                    File.WriteAllLines(FilePath, new[] { $@"<?xml version=""1.0"" encoding=""UTF-8""?>", $@"<LocalConnections/>" });
                }
                fileContents = File.ReadAllText(FilePath);
            }
            catch (FileNotFoundException ex)
            {
                Trace.TraceWarning($"Could not load file. File does not exist '{FilePath}'.{Environment.NewLine}{ex}");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to load file {FilePath}.{Environment.NewLine}{ex}");
            }

            return fileContents;
        }

        public virtual void Save(string content)
        {
            string? temporaryFile = null;
            try
            {
                CreateMissingDirectories();
                temporaryFile = $"{FilePath}.{Guid.NewGuid():N}.tmp";
                File.WriteAllText(temporaryFile, content);
                File.Move(temporaryFile, FilePath, true);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to save file {FilePath}.{Environment.NewLine}{ex}");
            }
            finally
            {
                if (temporaryFile != null && File.Exists(temporaryFile))
                    File.Delete(temporaryFile);
            }
        }

        public virtual void MoveTo(string newPath)
        {
            try
            {
                PathValidator.ValidatePathOrThrow(newPath, nameof(newPath));
                File.Move(FilePath, newPath);
                FilePath = newPath;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to move file {FilePath} to {newPath}.{Environment.NewLine}{ex}");
            }
        }

        private void CreateMissingDirectories()
        {
            string? dirname = Path.GetDirectoryName(FilePath);
            if (dirname == null) return;
            Directory.CreateDirectory(dirname);
        }
    }
}
