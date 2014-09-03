//
// Collections.cs
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
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    
    internal static class Collections<T>
    {
        static readonly IList<T> empty = new T [0];
        public static IList<T> EMPTY_SET {
            get { return empty; }
        }
        
    }
    
    internal static class Collections
    {
        public static bool AddAll<T> (ICollection<T> list, IEnumerable toAdd)
        {
            foreach (T t in toAdd)
                list.Add (t);
            return true;
        }

        public static V Remove<K, V> (IDictionary<K, V> map, K toRemove)
        {
            V local;
            if (map.TryGetValue (toRemove, out local)) {
                map.Remove (toRemove);
                return local;
            }
            return default(V);
        }

        public static object[] ToArray (ArrayList list)
        {
            return list.ToArray ();
        }

        public static T[] ToArray<T> (ICollection<T> list)
        {
            T[] array = new T[list.Count];
            list.CopyTo (array, 0);
            return array;
        }

        public static U[] ToArray<T,U> (ICollection<T> list, U[] res) where T:U
        {
            if (res.Length < list.Count)
                res = new U [list.Count];
            
            int n = 0;
            foreach (T t in list)
                res [n++] = t;
            
            if (res.Length > list.Count)
                res [list.Count] = default (T);
            return res;
        }
        
        public static IDictionary<K,V> EmptyMap<K,V> ()
        {
            return new Dictionary<K,V> ();
        }

        public static IList<T> EmptyList<T> ()
        {
            return Collections<T>.EMPTY_SET;
        }

        public static ICollection<T> EmptySet<T> ()
        {
            return Collections<T>.EMPTY_SET;
        }

        public static IList<T> NCopies<T> (int n, T elem)
        {
            List<T> list = new List<T> (n);
            while (n-- > 0) {
                list.Add (elem);
            }
            return list;
        }

        public static void Reverse<T> (IList<T> list)
        {
            int end = list.Count - 1;
            int index = 0;
            while (index < end) {
                T tmp = list [index];
                list [index] = list [end];
                list [end] = tmp;
                ++index;
                --end;
            }
        }

        public static ICollection<T> Singleton<T> (T item)
        {
            List<T> list = new List<T> (1);
            list.Add (item);
            return list;
        }

        public static IList<T> SingletonList<T> (T item)
        {
            List<T> list = new List<T> (1);
            list.Add (item);
            return list;
        }

        public static IList<T> SynchronizedList<T> (IList<T> list)
        {
            return new Sharpen.SynchronizedList<T> (list);
        }

        public static ICollection<T> UnmodifiableCollection<T> (ICollection<T> list)
        {
            return list;
        }

        public static IList<T> UnmodifiableList<T> (IList<T> list)
        {
            return new ReadOnlyCollection<T> (list);
        }

        public static ICollection<T> UnmodifiableSet<T> (ICollection<T> list)
        {
            return list;
        }
        
        public static IDictionary<K,V> UnmodifiableMap<K,V> (IDictionary<K,V> dict)
        {
            return dict;
        }
    }
}
