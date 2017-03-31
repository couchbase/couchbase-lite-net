//
// SilentCryptoStream.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.IO;
using System.Security.Cryptography;

namespace Couchbase.Lite.Util
{

    // Works around the bug in .NET where disposing a CryptoStream without
    // reading it to the end throws an exception (based on
    // http://stackoverflow.com/a/22072068/1155387)
    internal sealed class SilentCryptoStream : CryptoStream
    {
        private Stream _underlying;

        public SilentCryptoStream(Stream underlying, ICryptoTransform transform, CryptoStreamMode mode)
            : base(underlying, transform, mode)
        {
            _underlying = underlying;
        }

        protected override void Dispose(bool disposing)
        {
            try {
                base.Dispose(disposing);
            } catch(CryptographicException) {
                if(disposing) {
                    _underlying.Dispose();
                }
            }
        }
    }
}
