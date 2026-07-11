using System;
using LoipvRemote.Connection;


namespace LoipvRemote.Config.Putty
{
    public class PuttySessionChangedEventArgs(PuttySessionInfo sessionChanged = null) : EventArgs
    {
        public PuttySessionInfo Session { get; set; } = sessionChanged;
    }
}