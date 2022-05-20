//
// C4Observer_native.cs
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

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
       
        public static C4DocumentObserver* c4docobs_create(C4Database* database, string docID, C4DocumentObserverCallback callback, void* context)
        {
            using(var docID_ = new C4String(docID)) {
                return NativeRaw.c4docobs_create(database, docID_.AsFLSlice(), callback, context);
            }
        }

        public static C4DocumentObserver* c4docobs_createWithCollection(C4Collection* collection, string docID, C4DocumentObserverCallback callback, void* context)
        {
            using(var docID_ = new C4String(docID)) {
                return NativeRaw.c4docobs_createWithCollection(collection, docID_.AsFLSlice(), callback, context);
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4QueryObserver* c4queryobs_create(C4Query* query, C4QueryObserverCallback callback, void* context);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4queryobs_setEnabled(C4QueryObserver* obs, [MarshalAs(UnmanagedType.U1)] bool enabled);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4CollectionObserver* c4dbobs_create(C4Database* database, C4DatabaseObserverCallback callback, void* context);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4QueryEnumerator* c4queryobs_getEnumerator(C4QueryObserver* obs, [MarshalAs(UnmanagedType.U1)] bool forget, C4Error* error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4CollectionObserver* c4dbobs_createOnCollection(C4Collection* collection, C4DatabaseObserverCallback callback, void* context);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint c4dbobs_getChanges(C4CollectionObserver* observer, [Out] C4CollectionChange[] outChanges, uint maxChanges, bool* outExternal);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4dbobs_releaseChanges(C4CollectionChange[] changes, uint numChanges);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4DocumentObserver* c4docobs_create(C4Database* database, FLSlice docID, C4DocumentObserverCallback callback, void* context);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4DocumentObserver* c4docobs_createWithCollection(C4Collection* collection, FLSlice docID, C4DocumentObserverCallback callback, void* context);


    }
}
