// 
// IReadOnlyDictionary.cs
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

namespace Couchbase.Lite
{
    public interface IReadOnlyDictionary : IReadOnlyDictionaryFragment, IEnumerable<KeyValuePair<string, object>>
    {
        #region Properties

        int Count { get; }

        ICollection<string> Keys { get; }

        #endregion

        #region Public Methods

        bool Contains(string key);

        IReadOnlyArray GetArray(string key);

        Blob GetBlob(string key);

        bool GetBoolean(string key);

        DateTimeOffset GetDate(string key);

        double GetDouble(string key);

        int GetInt(string key);

        long GetLong(string key);

        object GetObject(string key);

        string GetString(string key);

        ReadOnlySubdocument GetSubdocument(string key);

        IDictionary<string, object> ToDictionary();

        #endregion
    }
}
