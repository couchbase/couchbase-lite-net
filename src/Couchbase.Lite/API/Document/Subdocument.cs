// 
// Subdocument.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections.Generic;
using Couchbase.Lite.Internal.Doc;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    [JsonConverter(typeof(DictionaryObjectConverter))]
    public sealed class Subdocument : ReadOnlySubdocument, IDictionaryObject
    {
        #region Properties

        public override int Count => Dictionary.Count;

        public new Fragment this[string key] => Dictionary[key];

        public override ICollection<string> Keys => Dictionary.Keys;

        internal DictionaryObject Dictionary { get; }

        internal override bool IsEmpty => Dictionary.IsEmpty;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new blank <see cref="Subdocument"/>
        /// </summary>
        /// <returns>A constructed <see cref="Subdocument"/> object</returns>
        public Subdocument()
            : this(new FleeceDictionary())
        {
            
        }

        public Subdocument(IDictionary<string, object> dictionary)
            :this()
        {
            Set(dictionary);
        }

        internal Subdocument(IReadOnlyDictionary data)
            : base(data)
        {
            Dictionary = new DictionaryObject(data);
        }

        #endregion

        #region Overrides

        public override bool Contains(string key)
        {
            return Dictionary.Contains(key);
        }

        public override Blob GetBlob(string key)
        {
            return Dictionary.GetBlob(key);
        }

        public override bool GetBoolean(string key)
        {
            return Dictionary.GetBoolean(key);
        }

        public override DateTimeOffset GetDate(string key)
        {
            return Dictionary.GetDate(key);
        }

        public override double GetDouble(string key)
        {
            return Dictionary.GetDouble(key);
        }

        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return Dictionary.GetEnumerator();
        }

        public override int GetInt(string key)
        {
            return Dictionary.GetInt(key);
        }

        public override long GetLong(string key)
        {
            return Dictionary.GetLong(key);
        }

        public override object GetObject(string key)
        {
            return Dictionary.GetObject(key);
        }

        public override string GetString(string key)
        {
            return Dictionary.GetString(key);
        }

        public override IDictionary<string, object> ToDictionary()
        {
            return Dictionary.ToDictionary();
        }

        #endregion

        #region IDictionaryObject

        public new ArrayObject GetArray(string key)
        {
            return Dictionary.GetArray(key);
        }

        public new Subdocument GetSubdocument(string key)
        {
            return Dictionary.GetSubdocument(key);
        }

        public IDictionaryObject Remove(string key)
        {
            return Dictionary.Remove(key);
        }

        public IDictionaryObject Set(string key, object value)
        {
            return Dictionary.Set(key, value);
        }

        public IDictionaryObject Set(IDictionary<string, object> dictionary)
        {
            return Dictionary.Set(dictionary);
        }

        #endregion
    }
}

