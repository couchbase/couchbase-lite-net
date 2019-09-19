// 
//  ArrayObject.cs
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

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Support;

using JetBrains.Annotations;

using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a readonly ordered collection of objects
    /// </summary>
    public unsafe class ArrayObject : IArray
    {
        #region Variables

        internal FLArray* BaseArray { get; private set; }
        internal FLMutableArray ar;

        //[NotNull]
        //internal readonly MArray _array = new MArray();

        [NotNull] internal readonly ThreadSafety _threadSafety;

        #endregion

        #region Properties

        /// <inheritdoc />
        public int Count => _threadSafety.DoLocked(() => (int)Native.FLArray_Count(BaseArray));

        /// <inheritdoc />
        public IFragment this[int index] => index >= Count ? Fragment.Null : new Fragment(this, index);

        #endregion

        #region Constructors

        internal ArrayObject()
        {
            _threadSafety = SetupThreadSafety();
        }

        internal ArrayObject([NotNull]FLArray* array, bool isMutable)
        {
            ar = Native.FLArray_MutableCopy(array, FLCopyFlags.DefaultCopy);
            _threadSafety = SetupThreadSafety();
        }

        internal ArrayObject([NotNull]ArrayObject original, bool mutable)
            : this(original.BaseArray, mutable)
        {
            _threadSafety = SetupThreadSafety();
        }

        //internal ArrayObject(MValue mv, MCollection parent)
        //{
        //    _array.InitInSlot(mv, parent);
        //    _threadSafety = SetupThreadSafety();
        //}

        #endregion

        #region Public Methods

        /// <summary>
        /// Similar to the LINQ method, but returns all objects converted to standard
        /// .NET types
        /// </summary>
        /// <returns>A list of standard .NET typed objects in the array</returns>
        public List<object> ToList()
        {
            var count = Count;
            var result = new List<object>(count);
            _threadSafety.DoLocked(() =>
            {
                for (var i = 0; i < count; i++) {
                    result.Add(DataOps.ToNetObject(GetObject(BaseArray, i)));
                }
            });

            return result;
        }

        /// <summary>
        /// Creates a copy of this object that can be mutated
        /// </summary>
        /// <returns>A mutable copy of the array</returns>
        [NotNull]
        public MutableArrayObject ToMutable()
        {
            return _threadSafety.DoLocked(() => new MutableArrayObject(BaseArray, true));
        }

        #endregion

        #region Internal Methods

        [NotNull]
        internal virtual ArrayObject ToImmutable()
        {
            return this;
        }

        [NotNull]
        //internal MCollection ToMCollection()
        //{
        //    return BaseArray;
        //}

        #endregion

        #region Private Methods

        private static MValue Get([NotNull]FLArray* array, int index, IThreadSafety threadSafety = null)
        {
            return (threadSafety ?? NullThreadSafety.Instance).DoLocked(() =>
            {
                var val = new MValue(Native.FLArray_Get(array, (uint)index));//array.Get(index);
                if (val.IsEmpty) {
                    throw new IndexOutOfRangeException();
                }
                return val;
            });
        }

        private static object GetObject([NotNull]FLArray* array, int index, IThreadSafety threadSafety = null) => Get(array, index, threadSafety).AsObject(array);

        private static T GetObject<T>([NotNull]FLArray* array, int index, IThreadSafety threadSafety = null) where T : class => GetObject(array, index, threadSafety) as T;

        [NotNull]
        private ThreadSafety SetupThreadSafety()
        {
            Database db = null;
            //todo: find a way to get db :P
            //if (_array.Context != null && _array.Context != MContext.Null) {
            //    db = (_array.Context as DocContext)?.Db;
            //}

            return db?.ThreadSafety ?? new ThreadSafety();
        }

        #endregion

        #region IArray

        /// <inheritdoc />
        public ArrayObject GetArray(int index) => GetObject<ArrayObject>(BaseArray, index, _threadSafety);

        /// <inheritdoc />
        public Blob GetBlob(int index) => GetObject<Blob>(BaseArray, index, _threadSafety);

        /// <inheritdoc />
        public bool GetBoolean(int index) => DataOps.ConvertToBoolean(GetObject(BaseArray, index, _threadSafety));

        /// <inheritdoc />
        public DateTimeOffset GetDate(int index) => DataOps.ConvertToDate(GetObject(BaseArray, index, _threadSafety));

        /// <inheritdoc />
        public DictionaryObject GetDictionary(int index) => GetObject<DictionaryObject>(BaseArray, index, _threadSafety);

        /// <inheritdoc />
        public double GetDouble(int index) => DataOps.ConvertToDouble(GetObject(BaseArray, index, _threadSafety));

        /// <inheritdoc />
        public float GetFloat(int index) => DataOps.ConvertToFloat(GetObject(BaseArray, index, _threadSafety));

        /// <inheritdoc />
        public int GetInt(int index) => DataOps.ConvertToInt(GetObject(BaseArray, index, _threadSafety));

        /// <inheritdoc />
        public long GetLong(int index) => DataOps.ConvertToLong(GetObject(BaseArray, index, _threadSafety));

        /// <inheritdoc />
        public object GetValue(int index) => GetObject(BaseArray, index, _threadSafety);

        /// <inheritdoc />
        public string GetString(int index) => GetObject<string>(BaseArray, index, _threadSafety);

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<object>

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public virtual IEnumerator<object> GetEnumerator() => BaseArray.GetEnumerator();

        #endregion
    }

    [ExcludeFromCodeCoverage]
    internal sealed class IArrayConverter : JsonConverter
    {
        #region Overrides

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var arr = value as IArray;
            writer.WriteStartArray();
            foreach (var item in arr) {
                serializer.Serialize(writer, item);
            }

            writer.WriteEndArray();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var arr = new MutableArrayObject();
            while (reader.Read()) {
                arr.AddValue(serializer.Deserialize(reader));
            }

            return arr;
        }

        public override bool CanConvert(Type objectType) => typeof(IArray).IsAssignableFrom(objectType);

        #endregion
    }
}
