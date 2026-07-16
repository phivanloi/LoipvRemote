using System;
using System.Collections.Generic;
using System.Linq;

namespace LoipvRemote.Tools
{
    public static class Extensions
    {
        public static OptionalValue<T> Maybe<T>(this T value)
        {
            return new OptionalValue<T>(value);
        }

        public static OptionalValue<TResult> MaybeParse<T, TResult>(this T value, Func<T, TResult> parseFunc)
        {
            try
            {
                return new OptionalValue<TResult>(parseFunc(value));
            }
            catch
            {
                return new OptionalValue<TResult>();
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if the given value is
        /// null. Otherwise, return the value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="argName">
        /// The name of the argument
        /// </param>
        public static T ThrowIfNull<T>(this T value, string argName)
        {
            if (value == null)
                throw new ArgumentNullException(argName);
            return value;
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if the value
        /// is null or an empty string. Otherwise, returns the value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="argName"></param>
        public static string ThrowIfNullOrEmpty(this string value, string argName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Value cannot be null or empty", argName);
            return value;
        }

        /// <summary>
        /// Perform an action for each item in the given collection. The item
        /// is the pass along the processing chain.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            collection = collection.ToList();

            foreach (T item in collection)
                action(item);

            return collection;
        }
    }
}
