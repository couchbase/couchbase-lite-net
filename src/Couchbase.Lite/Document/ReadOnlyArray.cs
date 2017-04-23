// 
// ReadOnlyArray.cs
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
    internal class ReadOnlyArray : IReadOnlyArray
    {
        #region Properties

        public virtual int Count => Data.Count;

        public IReadOnlyFragment this[int index]
        {
            get {
                var value = index >= 0 && index < Count ? GetObject(index) : null;
                return new ReadOnlyFragment(value);
            }
        }

        internal IReadOnlyArray Data { get; set; }

        #endregion

        #region Constructors

        public ReadOnlyArray(IReadOnlyArray data)
        {
            Data = data;
        }

        #endregion

        #region IReadOnlyArray

        public IReadOnlyArray GetArray(int index)
        {
            return Data.GetArray(index);
        }

        public virtual IBlob GetBlob(int index)
        {
            return Data.GetBlob(index);
        }

        public virtual bool GetBoolean(int index)
        {
            return Data.GetBoolean(index);
        }

        public virtual DateTimeOffset GetDate(int index)
        {
            return Data.GetDate(index);
        }

        public virtual double GetDouble(int index)
        {
            return Data.GetDouble(index);
        }

        public virtual int GetInt(int index)
        {
            return Data.GetInt(index);
        }

        public virtual long GetLong(int index)
        {
            return Data.GetLong(index);
        }

        public virtual object GetObject(int index)
        {
            return Data.GetObject(index);
        }

        public virtual string GetString(int index)
        {
            return Data.GetString(index);
        }

        public IReadOnlySubdocument GetSubdocument(int index)
        {
            return Data.GetSubdocument(index);
        }

        public virtual IList<object> ToArray()
        {
            var array = new List<object>();
            for (var i = 0; i < Count; i++) {
                var value = GetObject(i);
                switch (value) {
                    case IReadOnlyDictionary d:
                        array.Add(d.ToDictionary());
                        break;
                    case IReadOnlyArray a:
                        array.Add(a.ToArray());
                        break;
                    default:
                        array.Add(value);
                        break;
                }
            }

            return array;
        }

        #endregion
    }
}
