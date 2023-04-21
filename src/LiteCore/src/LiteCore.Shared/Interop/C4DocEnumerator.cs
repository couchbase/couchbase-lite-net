﻿//
//  C4DocEnumerator.cs
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System.Diagnostics.CodeAnalysis;

namespace LiteCore.Interop
{
    
    [ExcludeFromCodeCoverage]
    internal partial struct C4EnumeratorOptions
    {
        public static readonly C4EnumeratorOptions Default = new C4EnumeratorOptions {
            flags = C4EnumeratorFlags.IncludeNonConflicted | C4EnumeratorFlags.IncludeBodies
        };
    }

    internal partial struct C4DocumentInfo
    {
        public const long TimestampError = -1;
        public const long TimestampNone = 0;
    }
}
