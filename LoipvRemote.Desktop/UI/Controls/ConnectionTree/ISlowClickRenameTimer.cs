using System;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Controls.ConnectionTree
{
    [SupportedOSPlatform("windows")]
    public interface ISlowClickRenameTimer : IDisposable
    {
        int Interval { get; set; }
        bool Enabled { get; }
        event EventHandler Tick;
        void Start();
        void StopTimer();
    }
}
