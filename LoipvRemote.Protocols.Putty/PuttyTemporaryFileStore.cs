using System.Text;

namespace LoipvRemote.Protocols.Putty;

public static class PuttyTemporaryFileStore
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static string WritePrivateKey(string privateKey) => Write(".ppk", writer => writer.Write(privateKey));

    public static string WriteOpeningCommand(string openingCommand) =>
        Write(".txt", writer => writer.WriteLine(openingCommand.TrimEnd()));

    private static string Write(string extension, Action<StreamWriter> write)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);
            try
            {
                using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using StreamWriter writer = new(stream, Utf8NoBom);
                write(writer);
                File.SetAttributes(path, FileAttributes.Temporary);
                return path;
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }

        throw new IOException("Unable to create a unique temporary PuTTY file.");
    }
}
