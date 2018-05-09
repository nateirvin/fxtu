using System.Collections.Generic;
using System.Linq;

namespace System.Collections
{
    public static class CollectionExtensions
    {
        public static IEnumerable<T> ToEnumerable<T>(this IEnumerator<T> enumerator)
        {
            return new EnumeratorWrapper(enumerator).Cast<T>();
        }
    }

    internal class EnumeratorWrapper : IEnumerable
    {
        private readonly IEnumerator _enumerator;

        public EnumeratorWrapper(IEnumerator enumerator)
        {
            _enumerator = enumerator;
        }

        public IEnumerator GetEnumerator()
        {
            return _enumerator;
        }
    }
}