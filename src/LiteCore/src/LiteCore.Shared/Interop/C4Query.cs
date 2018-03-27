//
// C4Query.cs
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
using System.Runtime.InteropServices;
using System.Threading;

namespace LiteCore.Interop
{
#if LITECORE_PACKAGED
    internal
#else
    public
#endif
        partial struct C4IndexOptions : IDisposable
    {
        public void Dispose()
        {
            var old = Interlocked.Exchange(ref _language, IntPtr.Zero);
            if(old != IntPtr.Zero) {
                Marshal.FreeHGlobal(old);
            }
        }
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
        partial struct C4QueryOptions
    {
        public static readonly C4QueryOptions Default = new C4QueryOptions
        {
            rankFullText = true
        };
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
        partial struct C4FullTextMatch
    {
        public C4FullTextMatch(ulong dataSource, uint property, uint term,
            uint start, uint length)
        {
            this.dataSource = dataSource;
            this.property = property;
            this.term = term;
            this.start = start;
            this.length = length;
        }
    }
}


