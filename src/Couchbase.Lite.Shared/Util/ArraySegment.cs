//
// ArraySegment.cs
//
// Authors:
//  Ben Maurer (bmaurer@ximian.com)
//  Jensen Somers <jensen.somers@gmail.com>
//  Marek Safar (marek.safar@gmail.com)
//
// Copyright (C) 2004 Novell
// Copyright (C) 2012 Xamarin, Inc (http://www.xamarin.com)
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

using System;
using System.Collections;
using System.Collections.Generic;

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// A class that encapsulates a portion of an array
    /// </summary>
    [Serializable]
    public struct ArraySegment<T> : IList<T>, IEquatable<ArraySegment<T>>
    {

        #region Properties

        /// <summary>
        /// Gets the original array used to generate this segment
        /// </summary>
        public T [] Array { get; private set; }

        /// <summary>
        /// Gets the offset used to generate this segment
        /// </summary>
        public int Offset { get; private set; }

        /// <summary>
        /// Gets the count used to generate this segment
        /// </summary>
        /// <value>The count.</value>
        public int Count { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="array">The array to reference</param>
        /// <param name="offset">The offset to start referencing from.</param>
        /// <param name="count">The number of items to reference after offset.</param>
        public ArraySegment (T[] array, int offset, int count) : this()
        {
            if (array == null)
                throw new ArgumentNullException ("array");

            if (offset < 0)
                throw new ArgumentOutOfRangeException ("offset", "Non-negative number required.");

            if (count < 0)
                throw new ArgumentOutOfRangeException ("count", "Non-negative number required.");

            if (offset > array.Length)
                throw new ArgumentException ("out of bounds");

            // now offset is valid, or just beyond the end.
            // Check count -- do it this way to avoid overflow on 'offset + count'
            if (array.Length - offset < count)
                throw new ArgumentException ("out of bounds", "offset");

            Array = array;
            Offset = offset;
            Count = count;
        }

        /// <summary>
        /// Constructor, takes the whole array
        /// </summary>
        /// <param name="array">Array.</param>
        public ArraySegment (T [] array) : this()
        {
            if (array == null) {
                throw new ArgumentNullException("array");
            }

            Array = array;
            Offset = 0;
            Count = array.Length;
        }

        #endregion

        #region Overrides
        #pragma warning disable 1591

        public override bool Equals (Object obj)
        {
            if (obj is ArraySegment<T>) {
                return Equals((ArraySegment<T>) obj);
            }
            return false;
        }
            
        public override int GetHashCode ()
        {
            return ((Array.GetHashCode() ^ Offset) ^ Count);
        }

        #pragma warning restore 1591
        #endregion

        #region Operators

        /// <param name="a">The alpha component.</param>
        /// <param name="b">The blue component.</param>
        public static bool operator ==(ArraySegment<T> a, ArraySegment<T> b)
        {
            return a.Equals(b);
        }

        /// <param name="a">The alpha component.</param>
        /// <param name="b">The blue component.</param>
        public static bool operator !=(ArraySegment<T> a, ArraySegment<T> b)
        {
            return !(a.Equals(b));
        }

        #endregion

        #region IEquatable

        bool IEquatable<ArraySegment<T>>.Equals(ArraySegment<T> obj)
        {
            if ((Array == obj.Array) && (Offset == obj.Offset) && (Count == obj.Count))
                return true;
            return false;
        }

        #endregion

        #region IList

        bool ICollection<T>.IsReadOnly
        {
            get
            {
                return true;
            }
        }

        T IList<T>.this[int index] {
            get {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException ("index");

                return Array[Offset + index];
            }
            set {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException ("index");

                Array[Offset + index] = value;
            }
        }

        void ICollection<T>.Add (T item)
        {
            throw new NotSupportedException ();
        }

        void ICollection<T>.Clear ()
        {
            throw new NotSupportedException ();
        }

        bool ICollection<T>.Remove (T item)
        {
            throw new NotSupportedException ();
        }

        void IList<T>.Insert (int index, T item)
        {
            throw new NotSupportedException ();
        }

        void IList<T>.RemoveAt (int index)
        {
            throw new NotSupportedException ();
        }

        bool ICollection<T>.Contains (T item)
        {
            return System.Array.IndexOf (Array, item, Offset, Count) >= 0;
        }

        void ICollection<T>.CopyTo (T[] array, int arrayIndex)
        {
            System.Array.Copy (Array, Offset, array, arrayIndex, Count);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator ()
        {
            for (int i = 0; i < Count; ++i)
                yield return Array[Offset + i];
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return ((IEnumerable<T>) this).GetEnumerator ();
        }

        int IList<T>.IndexOf (T item)
        {
            var res = System.Array.IndexOf (Array, item, Offset, Count);
            return res < 0 ? -1 : res - Offset;
        }

        #endregion
    }
}