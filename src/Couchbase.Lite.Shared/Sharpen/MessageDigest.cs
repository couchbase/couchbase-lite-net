//
// MessageDigest.cs
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
    using System.Security.Cryptography;

    internal abstract class MessageDigest
    {
        protected MessageDigest ()
        {
        }
        
        public void Digest (byte[] buffer, int o, int len)
        {
            byte[] d = Digest ();
            d.CopyTo (buffer, o);
        }

        public byte[] Digest (byte[] buffer)
        {
            Update (buffer);
            return Digest ();
        }

        public abstract byte[] Digest ();
        public abstract int GetDigestLength ();
        public static MessageDigest GetInstance (string algorithm)
        {
            switch (algorithm.ToLower ()) {
            case "sha-1":
                return new MessageDigest<SHA1Managed> ();
            case "md5":
                return new MessageDigest<MD5CryptoServiceProvider> ();
            }
            throw new NotSupportedException (string.Format ("The requested algorithm \"{0}\" is not supported.", algorithm));
        }

        public abstract void Reset ();
        public abstract void Update (byte[] b);
        public abstract void Update (byte b);
        public abstract void Update (byte[] b, int offset, int len);
    }


    internal class MessageDigest<TAlgorithm> : MessageDigest where TAlgorithm : HashAlgorithm, new()
    {
        private TAlgorithm _hash;
        private CryptoStream _stream;

        public MessageDigest ()
        {
            this.Init ();
        }

        public override byte[] Digest ()
        {
            this._stream.FlushFinalBlock ();
            byte[] hash = this._hash.Hash;
            this.Reset ();
            return hash;
        }

        public void Dispose ()
        {
            if (this._stream != null) {
                this._stream.Dispose ();
            }
            this._stream = null;
        }

        public override int GetDigestLength ()
        {
            return (this._hash.HashSize / 8);
        }

        private void Init ()
        {
            this._hash = Activator.CreateInstance<TAlgorithm> ();
            this._stream = new CryptoStream (Stream.Null, this._hash, CryptoStreamMode.Write);
        }

        public override void Reset ()
        {
            this.Dispose ();
            this.Init ();
        }

        public override void Update (byte[] input)
        {
            this._stream.Write (input, 0, input.Length);
        }

        public override void Update (byte input)
        {
            this._stream.WriteByte (input);
        }

        public override void Update (byte[] input, int index, int count)
        {
            if (count < 0)
                Console.WriteLine ("Argh!");
            this._stream.Write (input, index, count);
        }
    }
}
