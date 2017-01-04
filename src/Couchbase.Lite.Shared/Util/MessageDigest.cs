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
namespace Couchbase.Lite.Util
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    internal abstract class MessageDigest
    {

        #region Constructors

        protected MessageDigest ()
        {
        }

        #endregion

        #region Public Methods

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
        public abstract void Update (byte[] input);
        public abstract void Update (byte input);
        public abstract void Update (byte[] input, int offset, int len);

        #endregion
    }


    internal class MessageDigest<TAlgorithm> : MessageDigest where TAlgorithm : HashAlgorithm, new()
    {

        #region Variables

        private TAlgorithm _hash;
        private CryptoStream _stream;

        #endregion

        #region Constructors

        public MessageDigest ()
        {
        }

        #endregion

        #region Public Methods

        public override byte[] Digest ()
        {
            _stream.FlushFinalBlock ();
            byte[] hash = _hash.Hash;
            Clear ();
            return hash;
        }

        public void Clear ()
        {
            var stream = _stream;
            _stream = null;
            if (stream != null) {
                stream.Dispose ();
#if !NET_3_5
                _hash.Dispose ();
#endif
            }
        }

#endregion

#region Private Methods

        private void Init ()
        {
            if (_hash == null) {
                _hash = Activator.CreateInstance<TAlgorithm> ();
            }

            if (_stream == null) {
                _stream = new CryptoStream (Stream.Null, _hash, CryptoStreamMode.Write);
            }
        }


#endregion

#region Overrides

        public override int GetDigestLength ()
        {
            return (_hash.HashSize / 8);
        }


        public override void Reset ()
        {
            Clear ();
            Init ();
        }

        public override void Update (byte[] input)
        {
            Init();
            _stream.Write (input, 0, input.Length);
        }

        public override void Update (byte input)
        {
            Init();
            _stream.WriteByte (input);
        }

        public override void Update (byte[] input, int index, int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "must be greater than or equal to zero");
            }

            Init();
            _stream.Write (input, index, count);
        }

#endregion

    }
}
