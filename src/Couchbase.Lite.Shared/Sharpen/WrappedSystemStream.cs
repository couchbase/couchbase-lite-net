//
// WrappedSystemStream.cs
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

	internal class WrappedSystemStream : Stream
	{
		private InputStream ist;
		private OutputStream ost;
		int position;
		int markedPosition;

		public WrappedSystemStream (InputStream ist)
		{
			this.ist = ist;
		}

		public WrappedSystemStream (OutputStream ost)
		{
			this.ost = ost;
		}
		
		public InputStream InputStream {
			get { return ist; }
		}

		public OutputStream OutputStream {
			get { return ost; }
		}

		public override void Close ()
		{
			if (this.ist != null) {
				this.ist.Close ();
			}
			if (this.ost != null) {
				this.ost.Close ();
			}
		}

		public override void Flush ()
		{
			this.ost.Flush ();
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			int res = this.ist.Read (buffer, offset, count);
			if (res != -1) {
				position += res;
				return res;
			} else
				return 0;
		}

		public override int ReadByte ()
		{
			int res = this.ist.Read ();
			if (res != -1)
				position++;
			return res;
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			if (origin == SeekOrigin.Begin)
				Position = offset;
			else if (origin == SeekOrigin.Current)
				Position = Position + offset;
			else if (origin == SeekOrigin.End)
				Position = Length + offset;
			return Position;
		}

		public override void SetLength (long value)
		{
			throw new NotSupportedException ();
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			this.ost.Write (buffer, offset, count);
			position += count;
		}

		public override void WriteByte (byte value)
		{
			this.ost.Write (value);
			position++;
		}

		public override bool CanRead {
			get { return (this.ist != null); }
		}

		public override bool CanSeek {
			get { return true; }
		}

		public override bool CanWrite {
			get { return (this.ost != null); }
		}

		public override long Length {
			get {
                return ist.Wrapped.Length;
			}
		}
		
		internal void OnMark (int nb)
		{
			markedPosition = position;
			ist.Mark (nb);
		}
		
		public override long Position {
			get {
				if (ist != null && ist.CanSeek ())
					return ist.Position;
				else
					return position;
			}
			set {
				if (value == position)
					return;
				else if (value == markedPosition)
					ist.Reset ();
				else if (ist != null && ist.CanSeek ()) {
					ist.Position = value;
				}
				else
					throw new NotSupportedException ();
			}
		}
	}
}
