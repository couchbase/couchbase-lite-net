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

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed class Subdocument : ReadOnlySubdocument, ISubdocument
    {
        #region Properties

        public new IFragment this[string key] => Dictionary[key];
        internal DictionaryObject Dictionary { get; }

        #endregion

        #region Constructors

        public Subdocument()
            : this(new FleeceDictionary())
        {
            
        }

        public Subdocument(IDictionary<string, object> dictionary)
            :this()
        {
            Set(dictionary);
        }

        public Subdocument(IReadOnlyDictionary data)
            : base(data)
        {
            Dictionary = new DictionaryObject(data);
        }

        #endregion

        #region Overrides

        public override ICollection<string> AllKeys()
        {
            return Dictionary.AllKeys();
        }

        public override bool Contains(string key)
        {
            return Dictionary.Contains(key);
        }

        public override IBlob GetBlob(string key)
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

        public new IArray GetArray(string key)
        {
            return Dictionary.GetArray(key);
        }

        public new ISubdocument GetSubdocument(string key)
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

