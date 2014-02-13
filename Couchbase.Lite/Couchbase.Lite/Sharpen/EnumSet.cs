//
// EnumSet.cs
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
namespace Sharpen
{
	using System;
	using System.Collections.Generic;

	public class EnumSet
	{
		public static EnumSet<U> Of<U> (U e)
		{
			EnumSet<U> @set = new EnumSet<U> ();
			@set.AddItem (e);
			return @set;
		}
		
		public static EnumSet<U> Of<U> (params U[] es)
		{
			return CopyOf (es);
		}
		
		public static EnumSet<T> CopyOf<T> (ICollection<T> c)
		{
			EnumSet<T> @set = new EnumSet<T> ();
			foreach (T e in c)
				@set.AddItem (e);
			return @set;
		}
	    
        public static EnumSet<T> NoneOf<T> (T es) {
            return new EnumSet<T>();
        }

        public static EnumSet<T> NoneOf<T> () {
            return EnumSet.NoneOf<T>(default(T));
        }
    }

	public class EnumSet<T> : AbstractSet<T>
	{
		// Fields
		private HashSet<T> hset;

		// Methods
		public EnumSet ()
		{
			this.hset = new HashSet<T> ();
		}

		public override bool AddItem (T item)
		{
			return this.hset.Add (item);
		}

		public override void Clear ()
		{
			this.hset.Clear ();
		}
		
		public virtual EnumSet<T> Clone ()
		{
			EnumSet<T> @set = new EnumSet<T> ();
			@set.hset = new HashSet<T> (this.hset);
			return @set;
		}

		public override bool Contains (object o)
		{
			return this.hset.Contains ((T)o);
		}

		public override Iterator<T> Iterator ()
		{
			return new EnumeratorWrapper<T> (this.hset, this.hset.GetEnumerator ());
		}

		public static EnumSet<U> Of<U> (U e)
		{
			EnumSet<U> @set = new EnumSet<U> ();
			@set.AddItem (e);
			return @set;
		}

		public override bool Remove (object item)
		{
			return this.hset.Remove ((T)item);
		}

		// Properties
		public override int Count {
			get { return this.hset.Count; }
		}
	}
}
