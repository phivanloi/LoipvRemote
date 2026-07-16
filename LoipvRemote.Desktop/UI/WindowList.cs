using System;
using System.Collections;
using System.Collections.Generic;
using LoipvRemote.UI.Window;

namespace LoipvRemote.UI
{
    /// <summary>
    /// Tracks open desktop windows and removes disposed instances before access.
    /// </summary>
    public class WindowList : IList<BaseWindow>
    {
        private readonly List<BaseWindow> _items = [];

        public BaseWindow this[int index]
        {
            get
            {
                CleanUp();
                return _items[index];
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _items[index] = value;
            }
        }

        public int Count
        {
            get
            {
                CleanUp();
                return _items.Count;
            }
        }

        public bool IsReadOnly => false;

        public void Add(BaseWindow uiWindow)
        {
            ArgumentNullException.ThrowIfNull(uiWindow);
            _items.Add(uiWindow);
        }

        public void AddRange(IEnumerable<BaseWindow> uiWindows)
        {
            ArgumentNullException.ThrowIfNull(uiWindows);
            foreach (BaseWindow uiWindow in uiWindows)
                Add(uiWindow);
        }

        public void Clear() => _items.Clear();

        public bool Contains(BaseWindow item) => _items.Contains(item);

        public void CopyTo(BaseWindow[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public IEnumerator<BaseWindow> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(BaseWindow item) => _items.IndexOf(item);

        public void Insert(int index, BaseWindow item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _items.Insert(index, item);
        }

        public bool Remove(BaseWindow item) => _items.Remove(item);

        public void RemoveAt(int index) => _items.RemoveAt(index);

        public BaseWindow? FromString(string uiWindow)
        {
            ArgumentNullException.ThrowIfNull(uiWindow);
            CleanUp();
            string escapedWindowText = uiWindow.Replace("&", "&&");
            foreach (BaseWindow window in _items)
            {
                if (window.Text == escapedWindowText)
                    return window;
            }

            return null;
        }

        private void CleanUp()
        {
            for (int index = _items.Count - 1; index >= 0; index--)
            {
                if (_items[index].IsDisposed)
                    _items.RemoveAt(index);
            }
        }

    }
}
