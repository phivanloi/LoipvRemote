using System;
using System.Linq;

namespace LoipvRemote.Tools
{
    public class DisposableOptional<T>(T value) : Optional<T>(value), IDisposable
        where T : IDisposable
    {
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing || !this.Any())
                return;

            this.First().Dispose();
        }
    }
}