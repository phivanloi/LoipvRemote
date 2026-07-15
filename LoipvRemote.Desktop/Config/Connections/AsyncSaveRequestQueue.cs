using System;

namespace LoipvRemote.Config.Connections
{
    /// <summary>
    /// Coalesces property-change save requests while one background worker is active.
    /// </summary>
    internal sealed class AsyncSaveRequestQueue
    {
        private readonly object _syncRoot = new();
        private bool _workerActive;
        private bool _pending;
        private string _propertyName = string.Empty;

        /// <returns><c>true</c> when the caller must start a worker.</returns>
        internal bool Queue(string propertyName)
        {
            lock (_syncRoot)
            {
                _pending = true;
                _propertyName = propertyName ?? string.Empty;

                if (_workerActive)
                    return false;

                _workerActive = true;
                return true;
            }
        }

        internal bool TryTake(out string propertyName)
        {
            lock (_syncRoot)
            {
                propertyName = _propertyName;
                if (!_pending)
                    return false;

                _pending = false;
                return true;
            }
        }

        /// <returns><c>true</c> when a new request arrived while saving.</returns>
        internal bool CompleteSaveAndHasPendingRequest()
        {
            lock (_syncRoot)
            {
                if (_pending)
                    return true;

                _workerActive = false;
                return false;
            }
        }
    }
}
