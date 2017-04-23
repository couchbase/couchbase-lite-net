using System;
using System.Collections.Generic;
using System.Globalization;

using Couchbase.Lite.Internal.DB;
using Couchbase.Lite.Serialization;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class FleeceArray : IReadOnlyArray
    {
        private FLArray* _array;
        private C4Document* _document;
        private Database _database;
        private SharedStringCache _sharedKeys;

        public IReadOnlyFragment this[int index]
        {
            get {
                var value = index >= 0 && index < Count ? GetObject(index) : null;
                return new ReadOnlyFragment(value);
            }
        }

        public int Count => (int) Native.FLArray_Count(_array);

        public FleeceArray()
        {
            
        }

        public FleeceArray(FLArray* array, C4Document* document, IDatabase database)
        {
            var db = database as Database ?? throw new InvalidOperationException("Custom IDatabase not supported");
            _array = array;
            _document = document;
            _database = db;
            _sharedKeys = db.SharedStrings;
        }

        public object GetObject(int index)
        {
            return FLValueConverter.ToCouchbaseObject(Native.FLArray_Get(_array, (uint)index), _sharedKeys, _document, _database);
        }

        public string GetString(int index)
        {
            return Native.FLValue_AsString(Native.FLArray_Get(_array, (uint) index));
        }

        public int GetInt(int index)
        {
            return (int)GetLong(index);
        }

        public long GetLong(int index)
        {
            return Native.FLValue_AsInt(Native.FLArray_Get(_array, (uint) index));
        }

        public double GetDouble(int index)
        {
            return Native.FLValue_AsDouble(Native.FLArray_Get(_array, (uint)index));
        }

        public bool GetBoolean(int index)
        {
            return Native.FLValue_AsBool(Native.FLArray_Get(_array, (uint)index));
        }

        public IBlob GetBlob(int index)
        {
            return GetObject(index) as IBlob;
        }

        public DateTimeOffset GetDate(int index)
        {
            var dateString = GetString(index);
            if (dateString == null) {
                return DateTimeOffset.MinValue;
            }

            return DateTimeOffset.ParseExact(dateString, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public IReadOnlyArray GetArray(int index)
        {
            return GetObject(index) as IReadOnlyArray;
        }

        public IReadOnlySubdocument GetSubdocument(int index)
        {
            return GetObject(index) as IReadOnlySubdocument;
        }

        public IList<object> ToArray()
        {
            var array = new List<object>(Count);
            for (int i = 0; i < Count; i++) {
                var value = Native.FLArray_Get(_array, (uint) i);
                array.Add(FLValueConverter.ToObject(value, _sharedKeys));
            }

            return array;
        }
    }
}
