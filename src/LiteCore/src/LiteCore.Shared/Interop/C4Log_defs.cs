//
// C4Log_defs.cs
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

#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649  // Member never assigned to
#pragma warning disable CS0169  // Member never used


using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using LiteCore.Util;

namespace LiteCore.Interop
{
    internal enum C4LogLevel : sbyte
    {
        Debug,
        Verbose,
        Info,
        Warning,
        Error,
        None
    }

	internal unsafe struct C4LogDomain
    {
    }

	internal unsafe struct C4LogFileOptions
    {
        public C4LogLevel log_level;
        public FLSlice base_path;
        public long max_size_bytes;
        public int max_rotate_count;
        private byte _use_plaintext;
        public FLSlice header;

        public bool use_plaintext
        {
            get {
                return Convert.ToBoolean(_use_plaintext);
            }
            set {
                _use_plaintext = Convert.ToByte(value);
            }
        }
    }
}

#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore CS0649  // Member never assigned to
#pragma warning restore CS0169  // Member never used
