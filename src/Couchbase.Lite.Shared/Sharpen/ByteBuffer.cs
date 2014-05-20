////
//// ByteBuffer.cs
////
//// Author:
////	Zachary Gramana  <zack@xamarin.com>
////
//// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
////
//// Permission is hereby granted, free of charge, to any person obtaining
//// a copy of this software and associated documentation files (the
//// "Software"), to deal in the Software without restriction, including
//// without limitation the rights to use, copy, modify, merge, publish,
//// distribute, sublicense, and/or sell copies of the Software, and to
//// permit persons to whom the Software is furnished to do so, subject to
//// the following conditions:
//// 
//// The above copyright notice and this permission notice shall be
//// included in all copies or substantial portions of the Software.
//// 
//// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
//// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
////
///**
//* Original iOS version by Jens Alfke
//* Ported to Android by Marty Schoch, Traun Leyden
//*
//* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
//*
//* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
//* except in compliance with the License. You may obtain a copy of the License at
//*
//* http://www.apache.org/licenses/LICENSE-2.0
//*
//* Unless required by applicable law or agreed to in writing, software distributed under the
//* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
//* either express or implied. See the License for the specific language governing permissions
//* and limitations under the License.
//*/
//namespace Sharpen
//{
//	using System;
//
//	internal class ByteBuffer
//	{
//		private byte[] buffer;
//		private DataConverter c;
//		private int capacity;
//		private int index;
//		private int limit;
//		private int mark;
//		private int offset;
//		private ByteOrder order;
//
//		public ByteBuffer ()
//		{
//			this.c = DataConverter.BigEndian;
//		}
//
//		private ByteBuffer (byte[] buf, int start, int len)
//		{
//			this.buffer = buf;
//			this.offset = 0;
//			this.limit = start + len;
//			this.index = start;
//			this.mark = start;
//			this.capacity = buf.Length;
//			this.c = DataConverter.BigEndian;
//		}
//
//		public static ByteBuffer Allocate (int size)
//		{
//			return new ByteBuffer (new byte[size], 0, size);
//		}
//
//		public static ByteBuffer AllocateDirect (int size)
//		{
//			return Allocate (size);
//		}
//
//		public byte[] Array ()
//		{
//			return buffer;
//		}
//
//		public int ArrayOffset ()
//		{
//			return offset;
//		}
//
//		public int Capacity ()
//		{
//			return capacity;
//		}
//
//		private void CheckGetLimit (int inc)
//		{
//			if ((index + inc) > limit) {
//				throw new BufferUnderflowException ();
//			}
//		}
//
//		private void CheckPutLimit (int inc)
//		{
//			if ((index + inc) > limit) {
//				throw new BufferUnderflowException ();
//			}
//		}
//
//		public void Clear ()
//		{
//			index = offset;
//			limit = offset + capacity;
//		}
//
//		public void Flip ()
//		{
//			limit = index;
//			index = offset;
//		}
//
//		public byte Get ()
//		{
//			CheckGetLimit (1);
//			return buffer[index++];
//		}
//
//		public void Get (byte[] data)
//		{
//			Get (data, 0, data.Length);
//		}
//
//		public void Get (byte[] data, int start, int len)
//		{
//			CheckGetLimit (len);
//			for (int i = 0; i < len; i++) {
//				data[i + start] = buffer[index++];
//			}
//		}
//
//		public int GetInt ()
//		{
//			CheckGetLimit (4);
//			int num = c.GetInt32 (buffer, index);
//			index += 4;
//			return num;
//		}
//
//		public short GetShort ()
//		{
//			CheckGetLimit (2);
//			short num = c.GetInt16 (buffer, index);
//			index += 2;
//			return num;
//		}
//
//		public bool HasArray ()
//		{
//			return true;
//		}
//
//		public int Limit ()
//		{
//			return (limit - offset);
//		}
//
//		public void Limit (int newLimit)
//		{
//			limit = newLimit;
//		}
//
//		public void Mark ()
//		{
//			mark = index;
//		}
//
//		public void Order (ByteOrder order)
//		{
//			this.order = order;
//			if (order == ByteOrder.BIG_ENDIAN) {
//				c = DataConverter.BigEndian;
//			} else {
//				c = DataConverter.LittleEndian;
//			}
//		}
//
//		public int Position ()
//		{
//			return (index - offset);
//		}
//
//		public void Position (int pos)
//		{
//			if ((pos < offset) || (pos > limit)) {
//				throw new BufferUnderflowException ();
//			}
//			index = pos + offset;
//		}
//
//		public void Put (byte[] data)
//		{
//			Put (data, 0, data.Length);
//		}
//
//		public void Put (byte data)
//		{
//			CheckPutLimit (1);
//			buffer[index++] = data;
//		}
//
//		public void Put (byte[] data, int start, int len)
//		{
//			CheckPutLimit (len);
//			for (int i = 0; i < len; i++) {
//				buffer[index++] = data[i + start];
//			}
//		}
//
//		public void PutInt (int i)
//		{
//			Put (c.GetBytes (i));
//		}
//
//		public void PutShort (short i)
//		{
//			Put (c.GetBytes (i));
//		}
//
//		public int Remaining ()
//		{
//			return (limit - index);
//		}
//
//		public void Reset ()
//		{
//			index = mark;
//		}
//
//		public ByteBuffer Slice ()
//		{
//			ByteBuffer b = Wrap (buffer, index, buffer.Length - index);
//			b.offset = index;
//			b.limit = limit;
//			b.order = order;
//			b.c = c;
//			b.capacity = limit - index;
//			return b;
//		}
//
//		public static ByteBuffer Wrap (byte[] buf)
//		{
//			return new ByteBuffer (buf, 0, buf.Length);
//		}
//
//		public static ByteBuffer Wrap (byte[] buf, int start, int len)
//		{
//			return new ByteBuffer (buf, start, len);
//		}
//	}
//}
