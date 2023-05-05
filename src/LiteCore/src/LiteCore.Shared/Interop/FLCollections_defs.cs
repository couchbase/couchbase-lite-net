//
// FLCollections_defs.cs
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


    internal unsafe struct FLArrayIterator
    {
        #pragma warning disable CS0169

        private void* _private1;
        private uint _private2;
        private byte _private3;
        private void* _private4;

        #pragma warning restore CS0169
    }

    internal unsafe struct FLDictIterator
    {
        #pragma warning disable CS0169

        private void* _private1;
        private uint _private2;
        private byte _private3;

        // _private4[4]
        private void* _private4a;
        private void* _private4b;
        private void* _private4c;
        private void* _private4d;
        private int _private5;

        #pragma warning restore CS0169
    }

    internal unsafe struct FLDictKey
    {
        #pragma warning disable CS0169

        private FLSlice _private1;
        private void* _private2;
        private uint _private3;
        private uint _private4;
        private byte _private5;

        #pragma warning restore CS0169
    }
}

#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore CS0649  // Member never assigned to
#pragma warning restore CS0169  // Member never used