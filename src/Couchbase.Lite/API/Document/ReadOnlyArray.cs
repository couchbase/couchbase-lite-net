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
using System.Collections;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    public class ReadOnlyArray : IReadOnlyArray
    {
        #region Properties

        public virtual int Count => Data.Count;

        public ReadOnlyFragment this[int index]
        {
            get {
                var value = index >= 0 && index < Count ? GetObject(index) : null;
                return new ReadOnlyFragment(value);
            }
        }

        internal IReadOnlyArray Data { get; set; }

        #endregion

        #region Constructors

        internal ReadOnlyArray(IReadOnlyArray data)
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

        #region IEnumerable<object>

        public virtual IEnumerator<object> GetEnumerator()
        {
            return Data.GetEnumerator();
        }

        #endregion

        #region IReadOnlyArray

        public IReadOnlyArray GetArray(int index)
        {
            return Data.GetArray(index);
        }

        public virtual Blob GetBlob(int index)
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

        public ReadOnlySubdocument GetSubdocument(int index)
        {
            return Data.GetSubdocument(index);
        }

        public virtual IList<object> ToList()
        {
            var array = new List<object>();
            foreach(var value in Data) {
                switch (value) {
                    case IReadOnlyDictionary d:
                        array.Add(d.ToDictionary());
                        break;
                    case IReadOnlyArray a:
                        array.Add(a.ToList());
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
