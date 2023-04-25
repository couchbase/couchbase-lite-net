//
// C4IndexTypes_defs.cs
//
// Copyright (c) 2023 Couchbase, Inc All rights reserved.
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using LiteCore.Util;

namespace LiteCore.Interop
{
    internal enum C4IndexType : uint
    {
        ValueIndex,
        FullTextIndex,
        ArrayIndex,
        PredictiveIndex,
    }

	internal unsafe partial struct C4IndexOptions
    {
        private IntPtr _language;
        private byte _ignoreDiacritics;
        private byte _disableStemming;
        private IntPtr _stopWords;

        public string? language
        {
            get {
                return Marshal.PtrToStringAnsi(_language);
            }
            set {
                var old = Interlocked.Exchange(ref _language, Marshal.StringToHGlobalAnsi(value));
                Marshal.FreeHGlobal(old);
            }
        }

        public bool ignoreDiacritics
        {
            get {
                return Convert.ToBoolean(_ignoreDiacritics);
            }
            set {
                _ignoreDiacritics = Convert.ToByte(value);
            }
        }

        public bool disableStemming
        {
            get {
                return Convert.ToBoolean(_disableStemming);
            }
            set {
                _disableStemming = Convert.ToByte(value);
            }
        }

        public string? stopWords
        {
            get {
                return Marshal.PtrToStringAnsi(_stopWords);
            }
            set {
                var old = Interlocked.Exchange(ref _stopWords, Marshal.StringToHGlobalAnsi(value));
                Marshal.FreeHGlobal(old);
            }
        }
    }
}