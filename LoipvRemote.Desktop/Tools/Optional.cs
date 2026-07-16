using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LoipvRemote.Tools
{
    /// <summary>
    /// Represents a value that may or may not have been assigned.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    public class OptionalValue<T> : IEnumerable<T>, IComparable<OptionalValue<T>>
    {
        private readonly T[] _values;

        public OptionalValue()
        {
            _values = Array.Empty<T>();
        }

        public OptionalValue(T value)
        {
            _values = value != null ? [value] : Array.Empty<T>();
        }

        public override string ToString() =>
            _values.Length == 0 || _values[0] is null ? string.Empty : _values[0]!.ToString() ?? string.Empty;

        public static implicit operator OptionalValue<T>(T value) => new(value);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_values).GetEnumerator();

        public int CompareTo(OptionalValue<T>? other)
        {
            if (other is null)
                return 1;

            bool hasValue = _values.Length > 0;
            bool otherHasValue = other._values.Length > 0;
            if (!hasValue)
                return otherHasValue ? -1 : 0;
            if (!otherHasValue)
                return 1;
            if (_values[0] is IComparable<T> comparable)
                return comparable.CompareTo(other._values[0]);

            throw new ArgumentException($"Cannot compare objects. OptionalValue type {typeof(T).FullName} is not comparable to itself");
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is OptionalValue<T> other)
                return Equals(other);
            if (obj is T value)
                return _values.Length > 0 && EqualityComparer<T>.Default.Equals(_values[0], value);
            return false;
        }

        private bool Equals(OptionalValue<T>? other)
        {
            if (other is null || _values.Length != other._values.Length)
                return false;
            return _values.Length == 0 || EqualityComparer<T>.Default.Equals(_values[0], other._values[0]);
        }

        public override int GetHashCode() =>
            _values.Length == 0 ? 0 : EqualityComparer<T>.Default.GetHashCode(_values[0]!);

        public static bool operator ==(OptionalValue<T>? left, OptionalValue<T>? right) => Equals(left, right);
        public static bool operator !=(OptionalValue<T>? left, OptionalValue<T>? right) => !Equals(left, right);
        public static bool operator <(OptionalValue<T>? left, OptionalValue<T>? right) => Compare(left, right) < 0;
        public static bool operator <=(OptionalValue<T>? left, OptionalValue<T>? right) => Compare(left, right) <= 0;
        public static bool operator >(OptionalValue<T>? left, OptionalValue<T>? right) => Compare(left, right) > 0;
        public static bool operator >=(OptionalValue<T>? left, OptionalValue<T>? right) => Compare(left, right) >= 0;

        private static int Compare(OptionalValue<T>? left, OptionalValue<T>? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            return left.CompareTo(right);
        }
    }
}
