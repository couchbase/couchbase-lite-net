//
// C4Base_defs.cs
//
// Copyright (c) 2022 Couchbase, Inc All rights reserved.
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


    internal unsafe struct C4ExtraInfo
    {
        public void* pointer;
        private IntPtr _destructor;

        public C4ExtraInfoDestructor destructor
        {
            get => Marshal.GetDelegateForFunctionPointer<C4ExtraInfoDestructor>(_destructor);
            set => _destructor = Marshal.GetFunctionPointerForDelegate(value);
        }
    }
    

	internal unsafe partial struct C4BlobKey
    {
        public fixed byte bytes[20];
    }

	internal unsafe struct C4BlobStore
    {
    }

	internal unsafe struct C4Cert
    {
    }

	internal unsafe struct C4Collection
    {
    }

    // C4DatabaseObserver is replaced
    internal unsafe struct C4CollectionObserver
    {
    }

	internal unsafe struct C4Database
    {
    }

	internal unsafe struct C4DocumentObserver
    {
    }

	internal unsafe struct C4DocEnumerator
    {
    }

	internal unsafe struct C4KeyPair
    {
    }

	internal unsafe struct C4Listener
    {
    }

	internal unsafe struct C4Query
    {
    }

	internal unsafe struct C4QueryObserver
    {
    }

	internal unsafe struct C4ReadStream
    {
    }

	internal unsafe struct C4Replicator
    {
    }

	internal unsafe struct C4Socket
    {
    }

	internal unsafe struct C4WriteStream
    {
    }
}