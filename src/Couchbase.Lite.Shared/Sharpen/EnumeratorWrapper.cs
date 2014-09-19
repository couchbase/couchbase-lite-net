//
// EnumeratorWrapper.cs
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
/*
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

    internal class EnumeratorWrapper<T> : Iterator<T>
    {
        object collection;
        IEnumerator<T> e;
        T lastVal;
        bool more;
        bool copied;

        public EnumeratorWrapper (object collection, IEnumerator<T> e)
        {
            this.e = e;
            this.collection = collection;
            this.more = e.MoveNext ();
        }

        public override bool HasNext ()
        {
            return this.more;
        }

        public override T Next ()
        {
            if (!more)
                throw new NoSuchElementException ();
            lastVal = e.Current;
            more = e.MoveNext ();
            return lastVal;
        }

        public override void Remove ()
        {
            ICollection<T> col = this.collection as ICollection<T>;
            if (col == null) {
                throw new NotSupportedException ();
            }
            if (more && !copied) {
                // Read the remaining elements, since the current enumerator
                // will be invalid after removing the element
                List<T> remaining = new List<T> ();
                do {
                    remaining.Add (e.Current);
                } while (e.MoveNext ());
                e = remaining.GetEnumerator ();
                e.MoveNext ();
                copied = true;
            }
            col.Remove (lastVal);
        }
    }
}
