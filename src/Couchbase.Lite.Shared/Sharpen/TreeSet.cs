//
// TreeSet.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/
namespace Sharpen
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class TreeSet<T> : AbstractSet<T>
    {
        private SortedDictionary<T, int> dict;

        public TreeSet ()
        {
            this.dict = new SortedDictionary<T, int> ();
        }

        public TreeSet (IEnumerable<T> items)
        {
            this.dict = new SortedDictionary<T, int> ();
            foreach (var i in items)
                AddItem (i);
        }

        public override bool AddItem (T element)
        {
            if (!this.dict.ContainsKey (element)) {
                this.dict[element] = 0;
                return true;
            }
            return false;
        }

        public override void Clear ()
        {
            this.dict.Clear ();
        }

        private int Compare (T a, T b)
        {
            return Comparer<T>.Default.Compare (a, b);
        }

        public override bool Contains (object item)
        {
            return this.dict.ContainsKey ((T)item);
        }

        public T First ()
        {
            if (this.dict.Count == 0) {
                throw new NoSuchMethodException ();
            }
            return this.dict.Keys.First<T> ();
        }

        public ICollection<T> HeadSet (T toElement)
        {
            List<T> list = new List<T> ();
            foreach (T t in this) {
                if (this.Compare (t, toElement) >= 0)
                    return list;
                list.Add (t);
            }
            return list;
        }

        public override Sharpen.Iterator<T> Iterator ()
        {
            return new EnumeratorWrapper<T> (this.dict.Keys, this.dict.Keys.GetEnumerator ());
        }

        public override bool Remove (object element)
        {
            return this.dict.Remove ((T)element);
        }

        public override int Count {
            get { return this.dict.Count; }
        }
        
        public override string ToString ()
        {
            return "[" + string.Join (", ", this.Select (d => d.ToString ()).ToArray ()) + "]";
        }
    }
}
