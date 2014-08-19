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
    public class LazyJsonArray<T> : AbstractList<T>
    {
        private bool parsed = false;

        private byte[] json;

        private IList<T> cache = new AList<T>();

        private IEnumerator<T> cacheIterator;

        public LazyJsonArray(byte[] json)
        {
            if (json[0] != '[')
            {
                throw new ArgumentException("data must represent a JSON array");
            }
            this.json = json;
        }

        public override IEnumerator<T> GetEnumerator()
        {
            if (parsed)
            {
                return cache.GetEnumerator();
            }
            else
            {
                ParseJson();
                return cache.GetEnumerator();
            }
        }

        public override T Get(int index)
        {
            if (parsed)
            {
                return cache[index];
            }
            else
            {
                ParseJson();
                return cache[index];
            }
        }

        public override int Count
        {
            get
            {
                if (parsed)
                {
                    return cache.Count;
                }
                else
                {
                    ParseJson();
                    return cache.Count;
                }
            }
        }

        // the following methods in AbstractList use #iterator(). Overwrite them to make sure they use the
        // cached version
        public override bool Contains(object o)
        {
            if (parsed)
            {
                return cache.Contains(o);
            }
            else
            {
                ParseJson();
                return cache.Contains(o);
            }
        }

        public override object[] ToArray()
        {
            if (parsed)
            {
                return Sharpen.Collections.ToArray(cache);
            }
            else
            {
                ParseJson();
                return Sharpen.Collections.ToArray(cache);
            }
        }

        public override T[] ToArray<S>(S[] a)
        {
            if (parsed)
            {
                return Sharpen.Collections.ToArray(cache, a);
            }
            else
            {
                ParseJson();
                return Sharpen.Collections.ToArray(cache, a);
            }
        }

        public override int GetHashCode()
        {
            if (parsed)
            {
                return cache.GetHashCode();
            }
            else
            {
                ParseJson();
                return cache.GetHashCode();
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
                IList<T> parsedvalues = (IList<T>)Manager.GetObjectMapper().ReadValue<object>(json
                    );
                //Merge parsed values into List, overwriting the values for duplicate keys
                Sharpen.Collections.AddAll(parsedvalues, cache);
                cache = parsedvalues;
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
