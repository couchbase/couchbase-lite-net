/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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

using System;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Util
{
	/// <summary>A resizable byte array.</summary>
	/// <remarks>A resizable byte array.</remarks>
	/// <since>4.0</since>
	[System.Serializable]
	public sealed class ByteArrayBuffer
	{
		private const long serialVersionUID = 4359112959524048036L;

		private byte[] buffer;

		private int len;

		/// <summary>
		/// Creates an instance of
		/// <see cref="ByteArrayBuffer">ByteArrayBuffer</see>
		/// with the given initial
		/// capacity.
		/// </summary>
		/// <param name="capacity">the capacity</param>
		public ByteArrayBuffer(int capacity) : base()
		{
			Args.NotNegative(capacity, "Buffer capacity");
			this.buffer = new byte[capacity];
		}

		private void Expand(int newlen)
		{
			byte[] newbuffer = new byte[Math.Max(this.buffer.Length << 1, newlen)];
			System.Array.Copy(this.buffer, 0, newbuffer, 0, this.len);
			this.buffer = newbuffer;
		}

		/// <summary>
		/// Appends <code>len</code> bytes to this buffer from the given source
		/// array starting at index <code>off</code>.
		/// </summary>
		/// <remarks>
		/// Appends <code>len</code> bytes to this buffer from the given source
		/// array starting at index <code>off</code>. The capacity of the buffer
		/// is increased, if necessary, to accommodate all <code>len</code> bytes.
		/// </remarks>
		/// <param name="b">the bytes to be appended.</param>
		/// <param name="off">the index of the first byte to append.</param>
		/// <param name="len">the number of bytes to append.</param>
		/// <exception cref="System.IndexOutOfRangeException">
		/// if <code>off</code> if out of
		/// range, <code>len</code> is negative, or
		/// <code>off</code> + <code>len</code> is out of range.
		/// </exception>
		public void Append(byte[] b, int off, int len)
		{
			if (b == null)
			{
				return;
			}
			if ((off < 0) || (off > b.Length) || (len < 0) || ((off + len) < 0) || ((off + len
				) > b.Length))
			{
				throw new IndexOutOfRangeException("off: " + off + " len: " + len + " b.length: "
					 + b.Length);
			}
			if (len == 0)
			{
				return;
			}
			int newlen = this.len + len;
			if (newlen > this.buffer.Length)
			{
				Expand(newlen);
			}
			System.Array.Copy(b, off, this.buffer, this.len, len);
			this.len = newlen;
		}

		/// <summary>Appends <code>b</code> byte to this buffer.</summary>
		/// <remarks>
		/// Appends <code>b</code> byte to this buffer. The capacity of the buffer
		/// is increased, if necessary, to accommodate the additional byte.
		/// </remarks>
		/// <param name="b">the byte to be appended.</param>
		public void Append(int b)
		{
			int newlen = this.len + 1;
			if (newlen > this.buffer.Length)
			{
				Expand(newlen);
			}
			this.buffer[this.len] = unchecked((byte)b);
			this.len = newlen;
		}

		/// <summary>
		/// Appends <code>len</code> chars to this buffer from the given source
		/// array starting at index <code>off</code>.
		/// </summary>
		/// <remarks>
		/// Appends <code>len</code> chars to this buffer from the given source
		/// array starting at index <code>off</code>. The capacity of the buffer
		/// is increased if necessary to accommodate all <code>len</code> chars.
		/// <p>
		/// The chars are converted to bytes using simple cast.
		/// </remarks>
		/// <param name="b">the chars to be appended.</param>
		/// <param name="off">the index of the first char to append.</param>
		/// <param name="len">the number of bytes to append.</param>
		/// <exception cref="System.IndexOutOfRangeException">
		/// if <code>off</code> if out of
		/// range, <code>len</code> is negative, or
		/// <code>off</code> + <code>len</code> is out of range.
		/// </exception>
		public void Append(char[] b, int off, int len)
		{
			if (b == null)
			{
				return;
			}
			if ((off < 0) || (off > b.Length) || (len < 0) || ((off + len) < 0) || ((off + len
				) > b.Length))
			{
				throw new IndexOutOfRangeException("off: " + off + " len: " + len + " b.length: "
					 + b.Length);
			}
			if (len == 0)
			{
				return;
			}
			int oldlen = this.len;
			int newlen = oldlen + len;
			if (newlen > this.buffer.Length)
			{
				Expand(newlen);
			}
			for (int i1 = off; i2 < newlen; i1++, i2++)
			{
				this.buffer[i2] = unchecked((byte)b[i1]);
			}
			this.len = newlen;
		}

		/// <summary>
		/// Appends <code>len</code> chars to this buffer from the given source
		/// char array buffer starting at index <code>off</code>.
		/// </summary>
		/// <remarks>
		/// Appends <code>len</code> chars to this buffer from the given source
		/// char array buffer starting at index <code>off</code>. The capacity
		/// of the buffer is increased if necessary to accommodate all
		/// <code>len</code> chars.
		/// <p>
		/// The chars are converted to bytes using simple cast.
		/// </remarks>
		/// <param name="b">the chars to be appended.</param>
		/// <param name="off">the index of the first char to append.</param>
		/// <param name="len">the number of bytes to append.</param>
		/// <exception cref="System.IndexOutOfRangeException">
		/// if <code>off</code> if out of
		/// range, <code>len</code> is negative, or
		/// <code>off</code> + <code>len</code> is out of range.
		/// </exception>
		public void Append(CharArrayBuffer b, int off, int len)
		{
			if (b == null)
			{
				return;
			}
			Append(b.Buffer(), off, len);
		}

		/// <summary>Clears content of the buffer.</summary>
		/// <remarks>Clears content of the buffer. The underlying byte array is not resized.</remarks>
		public void Clear()
		{
			this.len = 0;
		}

		/// <summary>Converts the content of this buffer to an array of bytes.</summary>
		/// <remarks>Converts the content of this buffer to an array of bytes.</remarks>
		/// <returns>byte array</returns>
		public byte[] ToByteArray()
		{
			byte[] b = new byte[this.len];
			if (this.len > 0)
			{
				System.Array.Copy(this.buffer, 0, b, 0, this.len);
			}
			return b;
		}

		/// <summary>
		/// Returns the <code>byte</code> value in this buffer at the specified
		/// index.
		/// </summary>
		/// <remarks>
		/// Returns the <code>byte</code> value in this buffer at the specified
		/// index. The index argument must be greater than or equal to
		/// <code>0</code>, and less than the length of this buffer.
		/// </remarks>
		/// <param name="i">the index of the desired byte value.</param>
		/// <returns>the byte value at the specified index.</returns>
		/// <exception cref="System.IndexOutOfRangeException">
		/// if <code>index</code> is
		/// negative or greater than or equal to
		/// <see cref="Length()">Length()</see>
		/// .
		/// </exception>
		public int ByteAt(int i)
		{
			return this.buffer[i];
		}

		/// <summary>Returns the current capacity.</summary>
		/// <remarks>
		/// Returns the current capacity. The capacity is the amount of storage
		/// available for newly appended bytes, beyond which an allocation
		/// will occur.
		/// </remarks>
		/// <returns>the current capacity</returns>
		public int Capacity()
		{
			return this.buffer.Length;
		}

		/// <summary>Returns the length of the buffer (byte count).</summary>
		/// <remarks>Returns the length of the buffer (byte count).</remarks>
		/// <returns>the length of the buffer</returns>
		public int Length()
		{
			return this.len;
		}

		/// <summary>Ensures that the capacity is at least equal to the specified minimum.</summary>
		/// <remarks>
		/// Ensures that the capacity is at least equal to the specified minimum.
		/// If the current capacity is less than the argument, then a new internal
		/// array is allocated with greater capacity. If the <code>required</code>
		/// argument is non-positive, this method takes no action.
		/// </remarks>
		/// <param name="required">the minimum required capacity.</param>
		/// <since>4.1</since>
		public void EnsureCapacity(int required)
		{
			if (required <= 0)
			{
				return;
			}
			int available = this.buffer.Length - this.len;
			if (required > available)
			{
				Expand(this.len + required);
			}
		}

		/// <summary>Returns reference to the underlying byte array.</summary>
		/// <remarks>Returns reference to the underlying byte array.</remarks>
		/// <returns>the byte array.</returns>
		public byte[] Buffer()
		{
			return this.buffer;
		}

		/// <summary>Sets the length of the buffer.</summary>
		/// <remarks>
		/// Sets the length of the buffer. The new length value is expected to be
		/// less than the current capacity and greater than or equal to
		/// <code>0</code>.
		/// </remarks>
		/// <param name="len">the new length</param>
		/// <exception cref="System.IndexOutOfRangeException">
		/// if the
		/// <code>len</code> argument is greater than the current
		/// capacity of the buffer or less than <code>0</code>.
		/// </exception>
		public void SetLength(int len)
		{
			if (len < 0 || len > this.buffer.Length)
			{
				throw new IndexOutOfRangeException("len: " + len + " < 0 or > buffer len: " + this
					.buffer.Length);
			}
			this.len = len;
		}

		/// <summary>
		/// Returns <code>true</code> if this buffer is empty, that is, its
		/// <see cref="Length()">Length()</see>
		/// is equal to <code>0</code>.
		/// </summary>
		/// <returns>
		/// <code>true</code> if this buffer is empty, <code>false</code>
		/// otherwise.
		/// </returns>
		public bool IsEmpty()
		{
			return this.len == 0;
		}

		/// <summary>
		/// Returns <code>true</code> if this buffer is full, that is, its
		/// <see cref="Length()">Length()</see>
		/// is equal to its
		/// <see cref="Capacity()">Capacity()</see>
		/// .
		/// </summary>
		/// <returns>
		/// <code>true</code> if this buffer is full, <code>false</code>
		/// otherwise.
		/// </returns>
		public bool IsFull()
		{
			return this.len == this.buffer.Length;
		}

		/// <summary>
		/// Returns the index within this buffer of the first occurrence of the
		/// specified byte, starting the search at the specified
		/// <code>beginIndex</code> and finishing at <code>endIndex</code>.
		/// </summary>
		/// <remarks>
		/// Returns the index within this buffer of the first occurrence of the
		/// specified byte, starting the search at the specified
		/// <code>beginIndex</code> and finishing at <code>endIndex</code>.
		/// If no such byte occurs in this buffer within the specified bounds,
		/// <code>-1</code> is returned.
		/// <p>
		/// There is no restriction on the value of <code>beginIndex</code> and
		/// <code>endIndex</code>. If <code>beginIndex</code> is negative,
		/// it has the same effect as if it were zero. If <code>endIndex</code> is
		/// greater than
		/// <see cref="Length()">Length()</see>
		/// , it has the same effect as if it were
		/// <see cref="Length()">Length()</see>
		/// . If the <code>beginIndex</code> is greater than
		/// the <code>endIndex</code>, <code>-1</code> is returned.
		/// </remarks>
		/// <param name="b">the byte to search for.</param>
		/// <param name="from">the index to start the search from.</param>
		/// <param name="to">the index to finish the search at.</param>
		/// <returns>
		/// the index of the first occurrence of the byte in the buffer
		/// within the given bounds, or <code>-1</code> if the byte does
		/// not occur.
		/// </returns>
		/// <since>4.1</since>
		public int IndexOf(byte b, int from, int to)
		{
			int beginIndex = from;
			if (beginIndex < 0)
			{
				beginIndex = 0;
			}
			int endIndex = to;
			if (endIndex > this.len)
			{
				endIndex = this.len;
			}
			if (beginIndex > endIndex)
			{
				return -1;
			}
			for (int i = beginIndex; i < endIndex; i++)
			{
				if (this.buffer[i] == b)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// Returns the index within this buffer of the first occurrence of the
		/// specified byte, starting the search at <code>0</code> and finishing
		/// at
		/// <see cref="Length()">Length()</see>
		/// . If no such byte occurs in this buffer within
		/// those bounds, <code>-1</code> is returned.
		/// </summary>
		/// <param name="b">the byte to search for.</param>
		/// <returns>
		/// the index of the first occurrence of the byte in the
		/// buffer, or <code>-1</code> if the byte does not occur.
		/// </returns>
		/// <since>4.1</since>
		public int IndexOf(byte b)
		{
			return IndexOf(b, 0, this.len);
		}
	}
}
