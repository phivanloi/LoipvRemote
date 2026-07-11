using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace LoipvRemote.Connection.Protocol.PowerShell
{
    public static class PowerShellPasswordPipe
    {
        public static string Start(string password)
        {
            string pipeName = $"LoipvRemotePowerShellSecretPipe{Guid.NewGuid():N}";
            Thread thread = new(() =>
            {
                try
                {
                    WritePassword(pipeName, password);
                }
                catch (OperationCanceledException)
                {
                    // The PowerShell child process did not connect before the timeout.
                }
                catch (IOException)
                {
                    // The child process exited before consuming the one-time secret.
                }
            }) { IsBackground = true };
            thread.Start();
            return pipeName;
        }

        private static void WritePassword(string pipeName, string password)
        {
            PipeSecurity pipeSecurity = new();
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            SecurityIdentifier sid = identity.Owner ?? identity.User
                ?? throw new InvalidOperationException("Unable to determine current user SID.");
            pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            pipeSecurity.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));

            using NamedPipeServerStream server = NamedPipeServerStreamAcl.Create(
                pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, pipeSecurity);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            server.WaitForConnectionAsync(timeout.Token).GetAwaiter().GetResult();
            using StreamWriter writer = new(server, new UTF8Encoding(false), 1024, leaveOpen: true);
            writer.Write(password);
            writer.Flush();
        }
    }
}
