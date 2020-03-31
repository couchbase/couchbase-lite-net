//
// C4Query_defs.cs
//
// Copyright (c) 2020 Couchbase, Inc All rights reserved.
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
    internal enum C4QueryLanguage : uint
    {
        JSONQuery,
        N1QLQuery,
    }

	internal unsafe partial struct C4QueryOptions
    {
        private byte _rankFullText;

        public bool rankFullText
        {
            get {
                return Convert.ToBoolean(_rankFullText);
            }
            set {
                _rankFullText = Convert.ToByte(value);
            }
        }
    }

	internal unsafe partial struct C4FullTextMatch
    {
        public ulong dataSource;
        public uint property;
        public uint term;
        public uint start;
        public uint length;
    }

	internal unsafe struct C4QueryEnumerator
    {
        public FLArrayIterator columns;
        public ulong missingColumns;
        public uint fullTextMatchCount;
        public C4FullTextMatch* fullTextMatches;
    }
}