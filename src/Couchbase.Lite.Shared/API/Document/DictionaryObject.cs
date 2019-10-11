// 
//  DictionaryObject.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Couchbase.Lite.Fleece;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Support;

using JetBrains.Annotations;

using Newtonsoft.Json;

namespace Couchbase.Lite
{
    [ExcludeFromCodeCoverage]
    internal sealed class IDictionaryObjectConverter : JsonConverter
    {
        #region Overrides

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dict = value as IDictionaryObject;
            writer.WriteStartObject();
            foreach (var pair in dict) {
                writer.WritePropertyName(pair.Key);
                serializer.Serialize(writer, pair.Value);
            }
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var dict = new MutableDictionaryObject();
            if (reader.TokenType == JsonToken.StartObject) {
                reader.Read();
            }

            while (reader.TokenType != JsonToken.EndObject && reader.Read()) {
                var key = reader.Value as string;
                if (key == null) {
                    throw new InvalidDataException(CouchbaseLiteErrorMessage.InvalidValueToBeDeserialized);
                }

                reader.Read();
                var value = reader.Value;
                dict.SetValue(key, value);
            }

            return dict;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(IDictionaryObject).IsAssignableFrom(objectType);
        }

        #endregion
    }

    /// <summary>
    /// A class representing a key-value collection that is read only
    /// </summary>
    public class DictionaryObject : IDictionaryObject, IDisposable
    {
        #region Variables

        [NotNull]
        internal readonly FleeceMutableDictionary _dict = new FleeceMutableDictionary();

        [NotNull] internal readonly ThreadSafety _threadSafety;
        private List<string> _keys;

        #endregion

        #region Properties

        /// <inheritdoc />
        public IFragment this[string key] => new Fragment(this, key);

        /// <inheritdoc />
        public int Count => _threadSafety.DoLocked(() => _dict.Count);

        /// <inheritdoc />
        public ICollection<string> Keys
        {
            get {
                if (_keys == null) {
                    _threadSafety.DoLocked(() =>
                    {
                        // Double check pattern
                        if (_keys == null) {
                            _keys = new List<string>(_dict.Count);
                            foreach (var item in _dict.AllItems()) {
                                _keys.Add(item.Key);
                            }
                        }
                    });
                }

                return _keys;
            }
        }

        #endregion

        #region Constructors

        internal DictionaryObject()
        {
            _threadSafety = SetupThreadSafety();
            _threadSafety.DoLocked(() => _dict.NewFLMutableDict());
        }

        internal DictionaryObject(MValue mv, MCollection parent)
        {
            _dict.InitInSlot(mv, parent);
            _threadSafety = SetupThreadSafety();
        }

        internal DictionaryObject(FleeceMutableDictionary dict, bool isMutable)
        {
            _dict.InitAsCopyOf(dict, isMutable);
            _threadSafety = SetupThreadSafety();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a copy of this object that can be mutated
        /// </summary>
        /// <returns>A mutable copy of the dictionary</returns>
        [NotNull]
        public MutableDictionaryObject ToMutable()
        {
            return _threadSafety.DoLocked(() => new MutableDictionaryObject(_dict, true));
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Signal that the keys of this object have changed (not possible for
        /// this class, but a subclass might)
        /// </summary>
        protected void KeysChanged()
        {
            _threadSafety.DoLocked(() => _keys = null);
        }

        #endregion

        #region Internal Methods

        [NotNull]
        internal virtual DictionaryObject ToImmutable()
        {
            return this;
        }

        [NotNull]
        internal MCollection ToMCollection()
        {
            return _dict;
        }

        #endregion

        #region Private Methods

        private static object GetObject([NotNull]FleeceMutableDictionary dict, [NotNull]string key, IThreadSafety threadSafety = null) => (threadSafety ?? NullThreadSafety.Instance).DoLocked(() => dict.Get(key).AsObject(dict));

        private static T GetObject<T>([NotNull]FleeceMutableDictionary dict, [NotNull]string key, IThreadSafety threadSafety = null) where T : class => GetObject(dict, key, threadSafety) as T;

        [NotNull]
        private ThreadSafety SetupThreadSafety()
        {
            Database db = null;
            if (_dict.Context != null && _dict.Context != MContext.Null) {
                db = (_dict.Context as DocContext)?.Db;
            }

            return db?.ThreadSafety ?? new ThreadSafety();
        }

        #endregion

        #region IDictionaryObject

        /// <inheritdoc />
        public bool Contains(string key) => _threadSafety.DoLocked(() => !_dict.Get(key).IsEmpty);

        /// <inheritdoc />
        public ArrayObject GetArray(string key) => GetObject<ArrayObject>(_dict, key, _threadSafety);

        /// <inheritdoc />
        public Blob GetBlob(string key) => GetObject<Blob>(_dict, key, _threadSafety);

        /// <inheritdoc />
        public bool GetBoolean(string key) => DataOps.ConvertToBoolean(GetObject(_dict, key, _threadSafety));

        /// <inheritdoc />
        public DateTimeOffset GetDate(string key) => DataOps.ConvertToDate(GetObject(_dict, key, _threadSafety));

        /// <inheritdoc />
        public DictionaryObject GetDictionary(string key) => GetObject<DictionaryObject>(_dict, key, _threadSafety);

        /// <inheritdoc />
        public double GetDouble(string key) => DataOps.ConvertToDouble(GetObject(_dict, key, _threadSafety));

        /// <inheritdoc />
        public float GetFloat(string key) => DataOps.ConvertToFloat(GetObject(_dict, key, _threadSafety));

        /// <inheritdoc />
        public int GetInt(string key) => DataOps.ConvertToInt(GetObject(_dict, key, _threadSafety));

        /// <inheritdoc />
        public long GetLong(string key) => DataOps.ConvertToLong(GetObject(_dict, key, _threadSafety));

        /// <inheritdoc />
        public object GetValue(string key) => GetObject(_dict, key, _threadSafety);

        /// <inheritdoc />
        public string GetString(string key) => GetObject<string>(_dict, key, _threadSafety);

        /// <inheritdoc />
        public Dictionary<string, object> ToDictionary()
        {
            var result = new Dictionary<string, object>(_dict.Count);
            _threadSafety.DoLocked(() =>
            {
                foreach (var item in _dict.AllItems()) {
                    result[item.Key] = DataOps.ToNetObject(item.Value?.AsObject(_dict));
                }
            });

            return result;
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,object>>

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public virtual IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return new Enumerator(_dict);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _threadSafety.DoLocked(() =>
            {
                _dict?.Dispose();
            });
        }

        #endregion

        #region Nested

        private class Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            #region Variables

            [NotNull]
            private readonly IEnumerator<KeyValuePair<string, MValue>> _inner;

            [NotNull]
            private readonly FleeceMutableDictionary _parent;

            #endregion

            #region Properties

            object IEnumerator.Current => Current;

            public KeyValuePair<string, object> Current => new KeyValuePair<string, object>(_inner.Current.Key,
                _inner.Current.Value?.AsObject(_parent));

            #endregion

            #region Constructors

            public Enumerator([NotNull]FleeceMutableDictionary parent)
            {
                _parent = parent;
                _inner = parent.AllItems().GetEnumerator();
            }

            #endregion

            #region IDisposable

            public void Dispose()
            {
                _inner.Dispose();
            }

            #endregion

            #region IEnumerator

            public bool MoveNext() => _inner.MoveNext();

            public void Reset() => _inner.Reset();

            #endregion
        }

        #endregion
    }
}
