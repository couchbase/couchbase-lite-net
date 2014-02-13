//
// RandomAccessFile.cs
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
	using System.IO;

	internal class RandomAccessFile
	{
		private readonly FileStream stream;

		public RandomAccessFile (FilePath file, string mode) : this(file.GetPath (), mode)
		{
		}

		public RandomAccessFile (string file, string mode)
		{
			if (mode.IndexOf ('w') != -1)
				stream = new FileStream (file, System.IO.FileMode.OpenOrCreate, FileAccess.ReadWrite);
			else
				stream = new FileStream (file, System.IO.FileMode.Open, FileAccess.Read);
		}

		public void Close ()
		{
			stream.Close ();
		}

		public FileChannel GetChannel ()
		{
			return new FileChannel (this.stream);
		}

		public long GetFilePointer ()
		{
			return stream.Position;
		}

		public long Length ()
		{
			return stream.Length;
		}

        public int Read()
        {
            return stream.ReadByte ();
        }

		public int Read (byte[] buffer)
		{
			int r = stream.Read (buffer, 0, buffer.Length);
			return r > 0 ? r : -1;
		}

		public int Read (byte[] buffer, int start, int size)
		{
			return stream.Read (buffer, start, size);
		}

		public void ReadFully (byte[] buffer, int start, int size)
		{
			while (size > 0) {
				int num = stream.Read (buffer, start, size);
				if (num == 0) {
					throw new EOFException ();
				}
				size -= num;
				start += num;
			}
		}

		public void Seek (long pos)
		{
			stream.Position = pos;
		}

		public void SetLength (long len)
		{
			stream.SetLength (len);
		}

		public void Write (int value)
		{
			stream.Write (BitConverter.GetBytes (value), 0, 4);
		}
		
		public void Write (byte[] buffer)
		{
			stream.Write (buffer, 0, buffer.Length);
		}

		public void Write (byte[] buffer, int start, int size)
		{
			stream.Write (buffer, start, size);
		}
	}
}
