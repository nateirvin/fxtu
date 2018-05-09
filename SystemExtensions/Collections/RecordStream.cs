using System.Collections.Generic;
using System.Data;

namespace System.Collections
{
    public class RecordStream<T> : IEnumerator<T>
    {
        private readonly IDataReader _reader;
        private readonly Func<IDataReader, T> _conversionFunction;

        private RecordStream(IDataReader reader, Func<IDataReader, T> conversionFunction)
        {
            _reader = reader;
            _conversionFunction = conversionFunction;
        }

        public bool MoveNext()
        {
            return _reader.Read();
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public T Current
        {
            get { return _conversionFunction(_reader); }
        }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            _reader.Dispose();
        }

        private Wrapper GetWrapper()
        {
            return new Wrapper(this);
        }

        private class Wrapper : IEnumerable<T>
        {
            private readonly RecordStream<T> _parent;

            public Wrapper(RecordStream<T> parent)
            {
                _parent = parent;
            }
            
            public IEnumerator<T> GetEnumerator()
            {
                return _parent;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public static IEnumerable<T> CreateStream(IDataReader reader, Func<IDataReader, T> conversionFunction)
        {
            return new RecordStream<T>(reader, conversionFunction).GetWrapper();
        }
    }
}