// 
//  MutableDocument.cs
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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;
using LiteCore.Interop;
using Debug = System.Diagnostics.Debug;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing an entry in a Couchbase Lite <see cref="Lite.Database"/>.  
    /// It consists of some metadata, and a collection of user-defined properties
    /// </summary>
    public sealed unsafe class MutableDocument : Document, IMutableDictionary
    {
        #region Constants

        private const string Tag = nameof(MutableDocument);

        #if CBL_LINQ
        private Linq.IDocumentModel _model;
        #endif
           
        #endregion

        #region Properties

        internal override uint Generation => base.Generation + Convert.ToUInt32(Changed);

#if CBL_LINQ
        internal override bool IsEmpty => _model == null && base.IsEmpty;
        #endif

        internal override bool IsMutable => true;

        private bool Changed => (_dict as MutableDictionaryObject)?.HasChanges ?? (_dict as InMemoryDictionary)?.HasChanges ?? false;

        private IMutableDictionary Dict => _dict as IMutableDictionary;

        /// <inheritdoc />
        public new IMutableFragment this[string key] => Dict?[key] ?? Fragment.Null;

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public MutableDocument() : this(default(string))
        {

        }

        /// <summary>
        /// Creates a document given an ID
        /// </summary>
        /// <param name="id">The ID for the document</param>
        public MutableDocument(string id)
            : this(null, id ?? Misc.CreateGuid(), null)
        {
            
        }

        /// <summary>
        /// Creates a document with the given properties
        /// </summary>
        /// <param name="data">The properties of the document</param>
        public MutableDocument(IDictionary<string, object> data)
            : this()
        {
            SetData(data);
        }

        /// <summary>
        /// Creates a document with the given ID and properties
        /// </summary>
        /// <param name="id">The ID for the document</param>
        /// <param name="data">The properties for the document</param>
        public MutableDocument(string id, IDictionary<string, object> data)
            : this(id)
        {
            SetData(data);
        }

        /// <summary>
        /// Creates a document with the given ID and json string
        /// </summary>
        /// <param name="id">The ID for the document</param>
        /// <param name="json">
        /// The json contains the properties for the document
        /// </param>
        public MutableDocument(string id, string json) 
            : this(id)
        {
            SetJSON(json);
        }

        internal MutableDocument([NotNull]Collection collection, [NotNull]string id)
            : base(collection, id)
        {

        }

        internal MutableDocument([NotNull]Document doc)
            : this(doc.Collection, doc.Id, doc.c4Doc?.Retain<C4DocumentWrapper>())
        {

        }

        private MutableDocument([CanBeNull] Collection collection, [NotNull]string id, [CanBeNull]C4DocumentWrapper c4Doc)
            : base(collection, id, c4Doc)
        {
            
        }

        private MutableDocument([NotNull]MutableDocument other)
            : this((Document)other)
        {
            var dict = new MutableDictionaryObject();
            if (other._dict != null) {
                foreach (var item in other._dict) {
                    dict.SetValue(item.Key, MutableCopy(item.Value));
                }
            }

            _dict = dict;
        }

        #endregion

        #region Internal Methods

        #if CBL_LINQ
        internal void SetFromModel(Linq.IDocumentModel model)
        {
            _model = model;
        }
        #endif

        #endregion

        #region Private Methods

        #if CBL_LINQ
        private FLSliceResult EncodeModel(FLEncoder* encoder)
        {
            var serializer = JsonSerializer.CreateDefault();
            using (var writer = new Internal.Serialization.JsonFLValueWriter(c4Db)) {
                serializer.Serialize(writer, _model);
                writer.Flush();
                return writer.Result;
            }
        }
        #endif

        private static object MutableCopy(object original)
        {
            switch (original) {
                case DictionaryObject dict:
                    return dict.ToMutable();
                case ArrayObject arr:
                    return arr.ToMutable();
                case IList list:
                    return new List<object>(list.Cast<object>());
                case IDictionary<string, object> netDict:
                    return new Dictionary<string, object>(netDict);
                default:
                    return original;
            }
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override MutableDocument ToMutable() => new MutableDocument(this); // MutableDocument constructor is different, so this override is needed

        internal override FLSliceResult Encode()
        {
            Debug.Assert(Database != null);

            var body = new FLSliceResult();
            Database.ThreadSafety.DoLocked(() =>
            {
                FLEncoder* encoder = null;
                try {
                    encoder = Database.SharedEncoder;
                } catch (Exception) {
                    body = new FLSliceResult(null, 0UL);
                }

                #if CBL_LINQ
                if (_model != null) {
                    return (FLSlice)EncodeModel(encoder);
                }
                #endif

                var handle = GCHandle.Alloc(this);
                Native.FLEncoder_SetExtraInfo(encoder, (void *)GCHandle.ToIntPtr(handle));

                try {
                    _dict.FLEncode(encoder);
                } catch (Exception) {
                    Native.FLEncoder_Reset(encoder);
                    throw;
                } finally {
                    handle.Free();
                }

                FLError err;
                body = NativeRaw.FLEncoder_Finish(encoder, &err);
                if (body.buf == null) {
                    throw new CouchbaseFleeceException(err);
                }
            });

            return body;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var id = new SecureLogString(Id, LogMessageSensitivity.PotentiallyInsecure);
            return $"{GetType().Name}[{id}]";
        }

        #endregion

        #region IMutableDictionary

        /// <inheritdoc />
        public new MutableArrayObject GetArray(string key)
        {
            return Dict?.GetArray(key);
        }

        /// <inheritdoc />
        public new MutableDictionaryObject GetDictionary(string key)
        {
            return Dict?.GetDictionary(key);
        }

        /// <inheritdoc />
        public IMutableDictionary Remove(string key)
        {
            Dict?.Remove(key);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetValue(string key, object value)
        {
            Dict?.SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetData(IDictionary<string, object> dictionary)
        {
            Dict?.SetData(dictionary);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetString(string key, string value)
        {
            Dict?.SetString(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetInt(string key, int value)
        {
            Dict?.SetInt(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetLong(string key, long value)
        {
            Dict?.SetLong(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetFloat(string key, float value)
        {
            Dict?.SetFloat(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetDouble(string key, double value)
        {
            Dict?.SetDouble(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetBoolean(string key, bool value)
        {
            Dict?.SetBoolean(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetBlob(string key, Blob value)
        {
            Dict?.SetBlob(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetDate(string key, DateTimeOffset value)
        {
            Dict?.SetDate(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetArray(string key, ArrayObject value)
        {
            Dict?.SetArray(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetDictionary(string key, DictionaryObject value)
        {
            Dict?.SetDictionary(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetJSON([NotNull] string json)
        {
            return Dict?.SetJSON(json);
        }

        #endregion
    }
}
