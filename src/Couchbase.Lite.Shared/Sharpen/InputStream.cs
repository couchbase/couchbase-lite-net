//
// InputStream.cs
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
    using System.IO;

    internal class InputStream : IDisposable
    {
        private long mark;
        internal Stream Wrapped;
        protected Stream BaseStream;

        public static implicit operator InputStream (Stream s)
        {
            return Wrap (s);
        }

        public static implicit operator Stream (InputStream s)
        {
            return s == null 
                ? null 
                : s.GetWrappedStream();
        }
        
        public virtual int Available ()
        {
            if (Wrapped is WrappedSystemStream)
                return ((WrappedSystemStream)Wrapped).InputStream.Available ();
            else
                return 0;
        }

        public virtual void Close ()
        {
            if (Wrapped != null) {
                Wrapped.Close ();
            }
        }

        public void Dispose ()
        {
            Close ();
        }

        internal Stream GetWrappedStream ()
        {
            // Always create a wrapper stream (not directly Wrapped) since the subclass
            // may be overriding methods that need to be called when used through the Stream class
            return new WrappedSystemStream (this);
        }

        public virtual void Mark (int readlimit)
        {
            if (Wrapped is WrappedSystemStream)
                ((WrappedSystemStream)Wrapped).InputStream.Mark (readlimit);
            else {
                if (BaseStream is WrappedSystemStream)
                    ((WrappedSystemStream)BaseStream).OnMark (readlimit);
                if (Wrapped != null)
                    this.mark = Wrapped.Position;
            }
        }
        
        public virtual bool MarkSupported ()
        {
            if (Wrapped is WrappedSystemStream)
                return ((WrappedSystemStream)Wrapped).InputStream.MarkSupported ();
            else
                return ((Wrapped != null) && Wrapped.CanSeek);
        }

        public virtual int Read ()
        {
            if (Wrapped == null) {
                throw new NotImplementedException ();
            }
            return Wrapped.ReadByte ();
        }

        public virtual int Read (byte[] buf)
        {
            return Read (buf, 0, buf.Length);
        }

        public virtual int Read (byte[] b, int off, int len)
        {
            if (Wrapped is WrappedSystemStream)
                return ((WrappedSystemStream)Wrapped).InputStream.Read (b, off, len);
            
            if (Wrapped != null) {
                int num = Wrapped.Read (b, off, len);
                return ((num <= 0) ? -1 : num);
            }
            int totalRead = 0;
            while (totalRead < len) {
                int nr = Read ();
                if (nr == -1)
                    return -1;
                b[off + totalRead] = (byte)nr;
                totalRead++;
            }
            return totalRead;
        }

        public virtual void Reset ()
        {
            if (Wrapped is WrappedSystemStream)
                ((WrappedSystemStream)Wrapped).InputStream.Reset ();
            else {
                if (Wrapped == null)
                    throw new IOException ();
                Wrapped.Position = mark;
            }
        }

        public virtual long Skip (long cnt)
        {
            if (Wrapped is WrappedSystemStream)
                return ((WrappedSystemStream)Wrapped).InputStream.Skip (cnt);
            
            long n = cnt;
            while (n > 0) {
                if (Read () == -1)
                    return cnt - n;
                n--;
            }
            return cnt - n;
        }
        
        internal bool CanSeek ()
        {
            if (Wrapped != null)
                return Wrapped.CanSeek;
            else
                return false;
        }
        
        internal long Position {
            get {
                if (Wrapped != null)
                    return Wrapped.Position;
                else
                    throw new NotSupportedException ();
            }
            set {
                if (Wrapped != null)
                    Wrapped.Position = value;
                else
                    throw new NotSupportedException ();
            }
        }

        static internal InputStream Wrap (Stream s)
        {
            InputStream stream = new InputStream ();
            stream.Wrapped = s;
            return stream;
        }
    }
}
