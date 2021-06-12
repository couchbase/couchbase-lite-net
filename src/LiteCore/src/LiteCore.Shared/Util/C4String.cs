//
// C4String.cs
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

using LiteCore.Interop;

namespace LiteCore.Util
{
    /// <summary>
    /// Helper class for marshalling string &lt;&gt; C4Slice without creating an extra copy
    /// of the bytes.  Not for storage or long-term use
    /// </summary>
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local", Justification = "Marshaller will need to change these")]
    [ExcludeFromCodeCoverage]
    internal unsafe struct C4String : IDisposable
    {
        private int _byteCount;
        private byte* _bytes;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="s">The string to store in this instance</param>
        public C4String(string s)
        {
            _byteCount = 0;
            _bytes = null;
            if(s != null) {
                _byteCount = Encoding.UTF8.GetByteCount(s);
                _bytes = (byte *)Marshal.AllocHGlobal(_byteCount);
                fixed(char *c = s) {
                    Encoding.UTF8.GetBytes(c, s.Length, _bytes, _byteCount);
                }
            }
        }

        /// <summary>
        /// Returns this object as a FLSlice.  This object will only be valid
        /// while the original C4String object is valid.
        /// </summary>
        /// <returns>Ths C4String instance as a FLSlice</returns>
        public FLSlice AsFLSlice()
        {
            return new FLSlice(_bytes, (ulong)_byteCount);
        }

#pragma warning disable 1591
        public void Dispose()
        {
            Marshal.FreeHGlobal((IntPtr)_bytes);
            _bytes = null;
        }
#pragma warning restore 1591
    }
}
