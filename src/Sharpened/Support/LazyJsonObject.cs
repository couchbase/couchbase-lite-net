// 
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//using System;
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
    public class LazyJsonObject<K, V> : AbstractMap<K, V>
    {
        private bool parsed = false;

        private byte[] json;

        private IDictionary<K, V> cache = new Dictionary<K, V>();

        public LazyJsonObject(byte[] json)
        {
            if (json[0] != '{')
            {
                throw new ArgumentException("data must represent a JSON Object");
            }
            this.json = json;
        }

        public override V Put(K key, V value)
        {
            //value for key takes priority over json properties even if
            //json has not been parsed yet
            return cache.Put(key, value);
        }

        public override V Get(object key)
        {
            if (cache.ContainsKey(key))
            {
                return cache.Get(key);
            }
            else
            {
                ParseJson();
                return cache.Get(key);
            }
        }

        public override V Remove(object key)
        {
            if (cache.ContainsKey(key))
            {
                return Sharpen.Collections.Remove(cache, key);
            }
            else
            {
                ParseJson();
                return Sharpen.Collections.Remove(cache, key);
            }
        }

        public override void Clear()
        {
            cache.Clear();
        }

        public override bool ContainsKey(object key)
        {
            if (cache.ContainsKey(key))
            {
                return cache.ContainsKey(key);
            }
            else
            {
                ParseJson();
                return cache.ContainsKey(key);
            }
        }

        public override bool ContainsValue(object value)
        {
            if (cache.ContainsValue(value))
            {
                return cache.ContainsValue(value);
            }
            else
            {
                ParseJson();
                return cache.ContainsValue(value);
            }
        }

        public override ICollection<K> Keys
        {
            get
            {
                ParseJson();
                return cache.Keys;
            }
        }

        public override int Count
        {
            get
            {
                ParseJson();
                return cache.Count;
            }
        }

        public override ICollection<KeyValuePair<K, V>> EntrySet()
        {
            ParseJson();
            return cache.EntrySet();
        }

        public override ICollection<V> Values
        {
            get
            {
                ParseJson();
                return cache.Values;
            }
        }

        private void ParseJson()
        {
            if (parsed)
            {
                return;
            }
            try
            {
                IDictionary<K, V> parsedprops = (IDictionary<K, V>)Manager.GetObjectMapper().ReadValue
                    <object>(json);
                //Merge parsed properties into map, overwriting the values for duplicate keys
                parsedprops.PutAll(cache);
                cache = parsedprops;
            }
            catch (Exception e)
            {
                Log.E(Database.Tag, this.GetType().FullName + ": Failed to parse Json data: ", e);
            }
            finally
            {
                parsed = true;
                json = null;
            }
        }
    }
}
