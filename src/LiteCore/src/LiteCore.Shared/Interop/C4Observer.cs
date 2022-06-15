//
// C4Observer.cs
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

using System.Runtime.InteropServices;

namespace LiteCore.Interop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4CollectionObserverCallback(C4CollectionObserver* observer, void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4DocumentObserverCallback(C4DocumentObserver* observer, C4Collection* collection, FLSlice docID, ulong sequence, void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4QueryObserverCallback(C4QueryObserver* observer, C4Query* query, void* context);
}