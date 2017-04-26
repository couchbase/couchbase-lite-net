// 
// ReadOnlyDictionary.cs
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
using System.Collections;
using System.Collections.Generic;

using Couchbase.Lite.Support;

namespace Couchbase.Lite
{
    public class ReadOnlyDictionary : IReadOnlyDictionary
    {
        #region Variables

        internal readonly ThreadSafety _threadSafety = new ThreadSafety();

        #endregion

        #region Properties

        public virtual int Count => Data.Count;

        public ReadOnlyFragment this[string key] => new ReadOnlyFragment(GetObject(key));

        public virtual ICollection<string> Keys => Data.Keys;

        internal IReadOnlyDictionary Data { get; set; }

        internal virtual bool IsEmpty => Data?.Count == 0;

        #endregion

        #region Constructors

        internal ReadOnlyDictionary(IReadOnlyDictionary data)
        {
            Data = data;
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,object>>

        public virtual IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return Data.GetEnumerator();
        }

        #endregion

        #region IReadOnlyDictionary

        public virtual bool Contains(string key)
        {
            return Data.Contains(key);
        }

        public IReadOnlyArray GetArray(string key)
        {
            return Data.GetArray(key);
        }

        public virtual Blob GetBlob(string key)
        {
            return Data.GetBlob(key);
        }

        public virtual bool GetBoolean(string key)
        {
            return Data.GetBoolean(key);
        }

        public virtual DateTimeOffset GetDate(string key)
        {
            return Data.GetDate(key);
        }

        public virtual double GetDouble(string key)
        {
            return Data.GetDouble(key);
        }

        public virtual int GetInt(string key)
        {
            return Data.GetInt(key);
        }

        public virtual long GetLong(string key)
        {
            return Data.GetLong(key);
        }

        public virtual object GetObject(string key)
        {
            return Data.GetObject(key);
        }

        public virtual string GetString(string key)
        {
            return Data.GetString(key);
        }

        public ReadOnlySubdocument GetSubdocument(string key)
        {
            return Data.GetSubdocument(key);
        }

        public virtual IDictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();
            foreach (var pair in Data) {
                switch(pair.Value) {
                    case IReadOnlyDictionary d:
                        dict[pair.Key] = d.ToDictionary();
                        break;
                    case IReadOnlyArray a:
                        dict[pair.Key] = a.ToList();
                        break;
                    default:
                        dict[pair.Key] = pair.Value;
                        break;
                }
            }

            return dict;
        }

        #endregion

    }
}
