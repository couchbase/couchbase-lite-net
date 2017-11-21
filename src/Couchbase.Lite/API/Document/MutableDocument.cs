// 
//  MutableDocument.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
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
using System.Linq;

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing an entry in a Couchbase Lite <see cref="Lite.Database"/>.  
    /// It consists of some metadata, and a collection of user-defined properties
    /// </summary>
    public sealed unsafe class MutableDocument : Document, IMutableDictionary
    {
        #region Constants

        //private const string Tag = nameof(Document);

        private static readonly Dictionary<Guid, MutableDocument> _NativeCacheMap = new Dictionary<Guid, MutableDocument>();

        #endregion

        #region Properties

        internal override uint Generation => base.Generation + Convert.ToUInt32(Changed);

        internal override bool IsMutable => true;

        private bool Changed => (_dict as MutableDictionary)?.HasChanges ?? (_dict as InMemoryDictionary)?.HasChanges ?? false;

        private IMutableDictionary Dict => _dict as IMutableDictionary;

        internal static IReadOnlyDictionary<Guid, MutableDocument> NativeCacheMap => _NativeCacheMap;

        /// <inheritdoc />
        public new MutableFragment this[string key] => Dict[key];

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
        /// <param name="documentID">The ID for the document</param>
        public MutableDocument(string documentID)
            : this(null, documentID ?? Misc.CreateGuid(), null)
        {
            
        }

        /// <summary>
        /// Creates a document with the given properties
        /// </summary>
        /// <param name="dictionary">The properties of the document</param>
        public MutableDocument(IDictionary<string, object> dictionary)
            : this()
        {
            Set(dictionary);
        }

        /// <summary>
        /// Creates a document with the given ID and properties
        /// </summary>
        /// <param name="documentID">The ID for the document</param>
        /// <param name="dictionary">The properties for the document</param>
        public MutableDocument(string documentID, IDictionary<string, object> dictionary)
            : this(documentID)
        {
            Set(dictionary);
        }

        internal MutableDocument(Database database, string documentID, bool mustExist)
            : base(database, documentID, mustExist)
        {

        }

        internal MutableDocument(Document doc)
            : this(doc?.Database, doc?.Id, doc?.c4Doc?.Retain<C4DocumentWrapper>())
        {

        }

        private MutableDocument(Database database, string documentID, C4DocumentWrapper c4Doc)
            : base(database, documentID, c4Doc)
        {
            
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override MutableDocument ToMutable()
        {
            // TODO: Assign _root and _data from original MutableDocument
            // TODO: Copy _dict from the original MutableDocument
            return this;
        }

        internal override FLSlice Encode()
        {
            var encoder = Database.SharedEncoder;
            var guid = Guid.NewGuid();
            _NativeCacheMap[guid] = this;
            Native.FLEncoder_SetExtraInfo(encoder, &guid);

            try {
                _dict.FLEncode(encoder);
            } catch (Exception) {
                Native.FLEncoder_Reset(encoder);
                throw;
            } finally {
                _NativeCacheMap.Remove(guid);
            }

            FLError err;
            var body = NativeRaw.FLEncoder_Finish(encoder, &err);
            if (body.buf == null) {
                throw new LiteCoreException(new C4Error(err));
            }

            return (FLSlice)body;
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
        public new IMutableArray GetArray(string key)
        {
            return Dict.GetArray(key);
        }

        /// <inheritdoc />
        public new IMutableDictionary GetDictionary(string key)
        {
            return Dict.GetDictionary(key);
        }

        /// <inheritdoc />
        public IMutableDictionary Remove(string key)
        {
            Dict.Remove(key);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, object value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(IDictionary<string, object> dictionary)
        {
            Dict.Set(dictionary);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, string value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, int value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, long value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, float value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, double value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, bool value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, Blob value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, DateTimeOffset value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, MutableArray value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, MutableDictionary value)
        {
            Dict.Set(key, value);
            return this;
        }

        #endregion
    }
}
