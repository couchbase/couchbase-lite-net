//
// JSFilterCompiler.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
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

using Jint;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Listener
{
    /// <summary>
    /// A class for compiling javascript functions into filter functions
    /// </summary>
    public sealed class JSFilterCompiler : IFilterCompiler
    {

        #region IFilterCompiler
#pragma warning disable 1591

        public FilterDelegate CompileFilter(string filterSource, string language)
        {
            if(!language.Equals("javascript")) {
                return null;
            }

            filterSource = filterSource.Replace("function", "function _f1");
            return (rev, filterParams) =>
            {
                var engine = new Engine().SetValue("log", new Action<object>((line) => Log.To.Router.I("JSFilterCompiler", line.ToString())));
                var retVal = engine.Execute(filterSource).Invoke("_f1", new NoThrowDictionary(rev.Properties), 
                    new NoThrowDictionary(filterParams));
                
                if(retVal.IsNull() || retVal.IsUndefined()) {
                    return false;
                }

                if(retVal.IsBoolean()) {
                    return retVal.AsBoolean();
                }

                return true;
            };
        }

#pragma warning restore 1591
        #endregion

        private class NoThrowDictionary : IDictionary<string, object>
        {
            private readonly IDictionary<string, object> _dict;

            public NoThrowDictionary(IDictionary<string, object> source)
            {
                _dict = source;
            }

            #region IDictionary

            public bool ContainsKey(string key)
            {
                return _dict.ContainsKey(key);
            }

            public void Add(string key, object value)
            {
                _dict.Add(key, value);
            }

            public bool Remove(string key)
            {
                return _dict.Remove(key);
            }

            public bool TryGetValue(string key, out object value)
            {
                return _dict.TryGetValue(key, out value);
            }

            public object this[string key]
            {
                get
                {
                    var retVal = default(object);
                    if (!TryGetValue(key, out retVal)) {
                        return null;
                    }

                    return retVal;
                }
                set
                {
                    _dict[key] = value;
                }
            }

            public ICollection<string> Keys
            {
                get
                {
                    return _dict.Keys;
                }
            }

            public ICollection<object> Values
            {
                get
                {
                    return _dict.Values;
                }
            }

            #endregion

            #region ICollection

            public void Add(KeyValuePair<string, object> item)
            {
                ((ICollection<KeyValuePair<string, object>>)_dict).Add(item);
            }

            public void Clear()
            {
                _dict.Clear();
            }

            public bool Contains(KeyValuePair<string, object> item)
            {
                return ((ICollection<KeyValuePair<string, object>>)_dict).Contains(item);
            }

            public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
            {
                ((ICollection<KeyValuePair<string, object>>)_dict).CopyTo(array, arrayIndex);
            }

            public bool Remove(KeyValuePair<string, object> item)
            {
                return ((ICollection<KeyValuePair<string, object>>)_dict).Remove(item);
            }

            public int Count
            {
                get
                {
                    return _dict.Count;
                }
            }

            public bool IsReadOnly
            {
                get
                {
                    return false;
                }
            }

            #endregion

            #region IEnumerable

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<string, object>>)_dict).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)_dict).GetEnumerator();
            }

            #endregion
        }

    }
}

