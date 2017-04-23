using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Couchbase.Lite.Internal.DB;
using Couchbase.Lite.Serialization;
using Couchbase.Lite.Util;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class FleeceDictionary : IReadOnlyDictionary
    {
        private FLDict* _dict;
        private C4Document* _document;
        private Database _database;
        private SharedStringCache _sharedKeys;

        public IReadOnlyFragment this[string key] => new ReadOnlyFragment(GetObject(key));

        public int Count => (int)Native.FLDict_Count(_dict);

        public FleeceDictionary()
        {
            
        }

        public FleeceDictionary(FLDict* dict, C4Document* document, IDatabase database)
        {
            var db = database as Database ?? throw new InvalidOperationException("Custom IDatabase not supported");
            _dict = dict;
            _document = document;
            _database = db;
            _sharedKeys = _database.SharedStrings;
        }

        private FLValue* FleeceValueForKey(string key)
        {
            return _sharedKeys.GetDictValue(_dict, key);
        }

        public object GetObject(string key)
        {
            return FLValueConverter.ToCouchbaseObject(FleeceValueForKey(key), _sharedKeys, _document, _database);
        }

        public string GetString(string key)
        {
            return Native.FLValue_AsString(FleeceValueForKey(key));
        }

        public int GetInt(string key)
        {
            return (int) GetLong(key);
        }

        public long GetLong(string key)
        {
            return Native.FLValue_AsInt(FleeceValueForKey(key));
        }

        public double GetDouble(string key)
        {
            return Native.FLValue_AsDouble(FleeceValueForKey(key));
        }

        public bool GetBoolean(string key)
        {
            return Native.FLValue_AsBool(FleeceValueForKey(key));
        }

        public IBlob GetBlob(string key)
        {
            return GetObject(key) as IBlob;
        }

        public DateTimeOffset GetDate(string key)
        {
            var dateString = GetString(key);
            if (dateString == null) {
                return DateTimeOffset.MinValue;
            }

            return DateTimeOffset.ParseExact(dateString, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public IReadOnlyArray GetArray(string key)
        {
            return GetObject(key) as IReadOnlyArray;
        }

        public IReadOnlySubdocument GetSubdocument(string key)
        {
            return GetObject(key) as IReadOnlySubdocument;
        }

        public IDictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>((int)Native.FLDict_Count(_dict));
            FLDictIterator iter;
            Native.FLDictIterator_Begin(_dict, &iter);
            string key;
            while (null != (key = _sharedKeys.GetDictIterKey(&iter))) {
                var value = FleeceValueForKey(key);
                var typedObject = FLValueConverter.ToTypedObject(value, _sharedKeys, _database);
                if (typedObject != null) {
                    dict[key] = typedObject;
                } else {
                    dict[key] = FLValueConverter.ToObject(value, _sharedKeys);
                }

                Native.FLDictIterator_Next(&iter);
            }

            return dict;
        }

        public bool Contains(string key)
        {
            var type = Native.FLValue_GetType(FleeceValueForKey(key));
            return type != FLValueType.Undefined;
        }

        public ICollection<string> AllKeys()
        {
            var keys = new List<string>();
            if (_dict != null) {
                FLDictIterator iter;
                Native.FLDictIterator_Begin(_dict, &iter);
                string key;
                while (null != (key = _sharedKeys.GetDictIterKey(&iter))) {
                    keys.Add(key);
                    Native.FLDictIterator_Next(&iter);
                }
            }

            return keys;
        }
    }
}
