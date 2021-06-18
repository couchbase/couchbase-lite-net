//
// C4Index_native.cs
//
// Copyright (c) 2021 Couchbase, Inc All rights reserved.
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
        public static bool c4db_createIndex2(C4Database* database, string name, string indexSpec, C4QueryLanguage queryLanguage, C4IndexType indexType, C4IndexOptions* indexOptions, C4Error* outError)
        {
            using(var name_ = new C4String(name))
            using(var indexSpec_ = new C4String(indexSpec)) {
                return NativeRaw.c4db_createIndex2(database, name_.AsFLSlice(), indexSpec_.AsFLSlice(), queryLanguage, indexType, indexOptions, outError);
            }
        }

        public static bool c4db_deleteIndex(C4Database* database, string name, C4Error* outError)
        {
            using(var name_ = new C4String(name)) {
                return NativeRaw.c4db_deleteIndex(database, name_.AsFLSlice(), outError);
            }
        }

    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_createIndex2(C4Database* database, FLSlice name, FLSlice indexSpec, C4QueryLanguage queryLanguage, C4IndexType indexType, C4IndexOptions* indexOptions, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_deleteIndex(C4Database* database, FLSlice name, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4db_getIndexesInfo(C4Database* database, C4Error* outError);


    }
}
