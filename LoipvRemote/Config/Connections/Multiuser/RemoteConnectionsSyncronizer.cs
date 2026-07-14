using LoipvRemote.Connection;
using System;
using System.Runtime.Versioning;
using System.Timers;

// ReSharper disable ArrangeAccessorOwnerBody

namespace LoipvRemote.Config.Connections.Multiuser
{
    [SupportedOSPlatform("windows")]
    public class RemoteConnectionsSyncronizer : IConnectionsUpdateChecker
    {
        private readonly System.Timers.Timer _updateTimer;
        private readonly IConnectionsUpdateChecker _updateChecker;
        private readonly ConnectionsService _connectionsService;

        public double TimerIntervalInMilliseconds
        {
            get { return _updateTimer.Interval; }
        }

        public RemoteConnectionsSyncronizer(
            IConnectionsUpdateChecker updateChecker,
            ConnectionsService connectionsService)
        {
            _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
            _connectionsService = connectionsService ?? throw new ArgumentNullException(nameof(connectionsService));
            _updateTimer = new System.Timers.Timer(3000);
            SetEventListeners();
        }

        private void SetEventListeners()
        {
            _updateChecker.UpdateCheckStarted += OnUpdateCheckStarted;
            _updateChecker.UpdateCheckFinished += OnUpdateCheckFinished;
            _updateChecker.ConnectionsUpdateAvailable += (sender, args) => ConnectionsUpdateAvailable?.Invoke(sender, args);
            _updateTimer.Elapsed += (sender, args) => _updateChecker.IsUpdateAvailableAsync();
            ConnectionsUpdateAvailable += Load;
        }

        private void Load(object sender, ConnectionsUpdateAvailableEventArgs args)
        {
            _connectionsService.LoadConnections(true, false, "");
            args.Handled = true;
        }

        public void Enable()
        {
            _updateTimer.Start();
        }

        public void Disable()
        {
            _updateTimer.Stop();
        }

        public bool IsUpdateAvailable()
        {
            return _updateChecker.IsUpdateAvailable();
        }

        public void IsUpdateAvailableAsync()
        {
            _updateChecker.IsUpdateAvailableAsync();
        }


        private void OnUpdateCheckStarted(object sender, EventArgs eventArgs)
        {
            _updateTimer.Stop();
            UpdateCheckStarted?.Invoke(sender, eventArgs);
        }

        private void OnUpdateCheckFinished(object sender, ConnectionsUpdateCheckFinishedEventArgs eventArgs)
        {
            _updateTimer.Start();
            UpdateCheckFinished?.Invoke(sender, eventArgs);
        }

        public event EventHandler UpdateCheckStarted;
        public event UpdateCheckFinishedEventHandler UpdateCheckFinished;
        public event ConnectionsUpdateAvailableEventHandler ConnectionsUpdateAvailable;


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool itIsSafeToAlsoFreeManagedObjects)
        {
            if (!itIsSafeToAlsoFreeManagedObjects) return;
            _updateTimer.Dispose();
            _updateChecker.Dispose();
        }
    }
}
