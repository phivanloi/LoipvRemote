using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using LoipvRemote.Connection;
using LoipvRemote.Infrastructure.Windows.Registry;


namespace LoipvRemote.Config.Putty
{
    [SupportedOSPlatform("windows")]
    public class PuttySessionsRegistryProvider : AbstractPuttySessionsProvider, IDisposable
    {
        private readonly PuttyRegistrySessionStore _store = new();

        #region Public Methods

        public override string[] GetSessionNames(bool raw = false)
        {
            return PuttyRegistrySessionStore.GetSessionNames(raw);
        }

        public override PuttySessionInfo? GetSession(string sessionName)
        {
            PuttyRegistrySession? session = PuttyRegistrySessionStore.GetSession(sessionName);
            if (session is null) return null;

            PuttySessionInfo sessionInfo = new()
            {
                PuttySession = session.Name,
                Name = session.Name,
                Hostname = session.Hostname,
                Username = session.Username
            };

            switch (session.Protocol.ToLowerInvariant())
            {
                case "raw":
                    sessionInfo.Protocol = ProtocolKind.Raw;
                    break;
                case "rlogin":
                    sessionInfo.Protocol = ProtocolKind.Rlogin;
                    break;
                case "serial":
                    return null;
                case "ssh":
                    /* Per PUTTY.H in PuTTYNG & PuTTYNG Upstream (PuTTY proper currently)
                     * expect 0 for SSH1, 3 for SSH2 ONLY
                     * 1 for SSH1 with a 2 fallback
                     * 2 for SSH2 with a 1 fallback
                     *
                     * default to SSH2 if any other value is received
                     */
                    sessionInfo.Protocol = session.SshVersion is 0 or 1 ? ProtocolKind.Ssh1 : ProtocolKind.Ssh2;
                    break;
                case "telnet":
                    sessionInfo.Protocol = ProtocolKind.Telnet;
                    break;
                default:
                    return null;
            }

            if (session.Port == 0)
                sessionInfo.SetDefaultPort();
            else
                sessionInfo.Port = session.Port;

            return sessionInfo;
        }

        public override void StartWatcher()
        {
            try
            {
                _store.Changed += OnRegistryChanged;
                _store.StartWatcher();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"PuttySessions.Watcher.StartWatching() failed.{Environment.NewLine}{ex}");
                _store.StopWatcher();
            }
        }

        public override void StopWatcher()
        {
            _store.Changed -= OnRegistryChanged;
            _store.StopWatcher();
        }

        public void Dispose()
        {
            StopWatcher();
            _store.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        private void OnRegistryChanged(object? sender, EventArgs e)
        {
            RaiseSessionChangedEvent(new PuttySessionChangedEventArgs());
        }
    }
}
