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
        private readonly IConnectionTreeWorkspace _workspace;

        public double TimerIntervalInMilliseconds
        {
            get { return _updateTimer.Interval; }
        }

        public RemoteConnectionsSyncronizer(
            IConnectionsUpdateChecker updateChecker,
            IConnectionTreeWorkspace workspace)
        {
            _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _updateTimer = new System.Timers.Timer(3000);
            SetEventListeners();
        }

        private void SetEventListeners()
        {
            _updateChecker.UpdateCheckStarted += OnUpdateCheckStarted;
            _updateChecker.UpdateCheckFinished += OnUpdateCheckFinished;
            _updateChecker.ConnectionsUpdateAvailable += (sender, args) => ConnectionsUpdateAvailable?.Invoke(sender, args);
            _updateTimer.Elapsed += OnUpdateTimerElapsed;
            ConnectionsUpdateAvailable += Load;
        }

        private void Load(object? sender, ConnectionsUpdateAvailableEventArgs args)
        {
            _ = LoadConnectionsAsync(args);
        }

        private async Task LoadConnectionsAsync(ConnectionsUpdateAvailableEventArgs args)
        {
            try
            {
                await _workspace.LoadConnectionsAsync(true, false, "").ConfigureAwait(false);
            }
            finally
            {
                args.Handled = true;
            }
        }

        public void Enable()
        {
            _updateTimer.Start();
        }

        public void Disable()
        {
            _updateTimer.Stop();
        }

        private void OnUpdateTimerElapsed(object? sender, ElapsedEventArgs args)
        {
            _ = CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                await IsUpdateAvailableAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The checker emits its own diagnostic and always raises the completion event.
            }
        }

        public Task<bool> IsUpdateAvailableAsync(CancellationToken cancellationToken = default) =>
            _updateChecker.IsUpdateAvailableAsync(cancellationToken);


        private void OnUpdateCheckStarted(object? sender, EventArgs eventArgs)
        {
            _updateTimer.Stop();
            UpdateCheckStarted?.Invoke(sender, eventArgs);
        }

        private void OnUpdateCheckFinished(object? sender, ConnectionsUpdateCheckFinishedEventArgs eventArgs)
        {
            _updateTimer.Start();
            UpdateCheckFinished?.Invoke(sender, eventArgs);
        }

        public event EventHandler? UpdateCheckStarted;
        public event EventHandler<ConnectionsUpdateCheckFinishedEventArgs>? UpdateCheckFinished;
        public event EventHandler<ConnectionsUpdateAvailableEventArgs>? ConnectionsUpdateAvailable;


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
