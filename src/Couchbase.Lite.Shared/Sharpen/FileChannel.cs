//
// FileChannel.cs
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
    using System.IO;

    internal class FileChannel
    {
        private FileStream s;
        byte[] buffer;
        bool isOpen;

        internal FileChannel (FileStream s)
        {
            this.s = s;
            isOpen = true;
        }
        
        internal FileStream Stream {
            get { return s; }
        }

        public void Close ()
        {
            isOpen = false;
            s.Close ();
        }
        
        public bool IsOpen ()
        {
            return isOpen;
        }

        public void Force (bool f)
        {
            s.Flush ();
        }

//      public MappedByteBuffer Map ()
//      {
//          throw new NotImplementedException ();
//      }
//
//      public MappedByteBuffer Map (MapMode mode, long pos, int size)
//      {
//          throw new NotImplementedException ();
//      }

//      public int Read (byte[] buffer)
//      {
//          return s.Read (buffer, 0, buffer.Length);
//      }
//
//      public int Read (ByteBuffer buffer)
//      {
//          int offset = buffer.Position () + buffer.ArrayOffset ();
//          int num2 = s.Read (buffer.Array (), offset, (buffer.Limit () + buffer.ArrayOffset ()) - offset);
//          buffer.Position (buffer.Position () + num2);
//          return num2;
//      }

        public long Size ()
        {
            return s.Length;
        }

        public FileLock TryLock ()
        {
            try {
                s.Lock (0, int.MaxValue);
                return new FileLock (s);
            } catch (IOException) {
                return null;
            }
        }

//      public int Write (byte[] buffer)
//      {
//          s.Write (buffer, 0, buffer.Length);
//          return buffer.Length;
//      }
//
//      public int Write (ByteBuffer buffer)
//      {
//          int offset = buffer.Position () + buffer.ArrayOffset ();
//          int count = (buffer.Limit () + buffer.ArrayOffset ()) - offset;
//          s.Write (buffer.Array (), offset, count);
//          buffer.Position (buffer.Position () + count);
//          return count;
//      }
        
        public long TransferFrom (FileChannel src, long pos, long count)
        {
            if (buffer == null)
                buffer = new byte [8092];
            int nr = src.s.Read (buffer, 0, (int) Math.Min (buffer.Length, count));
            long curPos = s.Position;
            s.Position = pos;
            s.Write (buffer, 0, nr);
            s.Position = curPos;
            return nr;
        }

        public enum MapMode
        {
            READ_ONLY
        }
    }
}
