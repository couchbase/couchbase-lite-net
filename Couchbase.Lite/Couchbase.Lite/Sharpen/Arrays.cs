//
// Arrays.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
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
using System.Threading.Tasks;

namespace Sharpen
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	internal class Arrays
	{
		public static List<T> AsList<T> (params T[] array)
		{
			return array.ToList<T> ();
		}

		public static bool Equals<T> (T[] a1, T[] a2)
		{
			if (a1.Length != a2.Length) {
				return false;
			}
			for (int i = 0; i < a1.Length; i++) {
				if (!a1[i].Equals (a2[i])) {
					return false;
				}
			}
			return true;
		}

		public static void Fill<T> (T[] array, T val)
		{
			Fill<T> (array, 0, array.Length, val);
		}

		public static void Fill<T> (T[] array, int start, int end, T val)
		{
			for (int i = start; i < end; i++) {
				array[i] = val;
			}
		}

		public static void Sort (string[] array)
		{
			Array.Sort (array, (s1,s2) => string.CompareOrdinal (s1,s2));
		}

		public static void Sort<T> (T[] array)
		{
			Array.Sort<T> (array);
		}

		public static void Sort<T> (T[] array, IComparer<T> c)
		{
			Array.Sort<T> (array, c);
		}

		public static void Sort<T> (T[] array, int start, int count)
		{
			Array.Sort<T> (array, start, count);
		}

		public static void Sort<T> (T[] array, int start, int count, IComparer<T> c)
		{
			Array.Sort<T> (array, start, count, c);
		}

        public static int HashCode<T> (T[] array) {
            var hashCode = 1;
            foreach(T item in array) {
                hashCode = 31 * hashCode + ((item == null) ? 0 : item.GetHashCode());
            }
            return hashCode;
        }

        public static T[] CopyTo<T> (T[] original, Int32 length)
        {
            var copy = new T[length];
            Array.Copy(original, copy, length < original.Length ? length : original.Length);
            return copy;
        }

        public static T[] CopyTo<T> (T[] original, Int64 length)
        {
            var copy = new T[length];
            Array.Copy(original, copy, length < original.Length ? length : original.LongLength);
            return copy;
        }
	}
}
