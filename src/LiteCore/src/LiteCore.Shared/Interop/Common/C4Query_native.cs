//
// C4Query_native.cs
//
// Copyright (c) 2018 Couchbase, Inc All rights reserved.
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
        public static C4Query* c4query_new(C4Database* database, string expression, C4Error* error)
        {
            using(var expression_ = new C4String(expression)) {
                return NativeRaw.c4query_new(database, expression_.AsC4Slice(), error);
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4query_free(C4Query* x);

        public static string c4query_explain(C4Query* query)
        {
            using(var retVal = NativeRaw.c4query_explain(query)) {
                return ((C4Slice)retVal).CreateString();
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint c4query_columnCount(C4Query* query);

        public static C4QueryEnumerator* c4query_run(C4Query* query, C4QueryOptions* options, string encodedParameters, C4Error* outError)
        {
            using(var encodedParameters_ = new C4String(encodedParameters)) {
                return NativeRaw.c4query_run(query, options, encodedParameters_.AsC4Slice(), outError);
            }
        }

        public static string c4query_fullTextMatched(C4Query* query, C4FullTextMatch* term, C4Error* outError)
        {
            using(var retVal = NativeRaw.c4query_fullTextMatched(query, term, outError)) {
                return ((C4Slice)retVal).CreateString();
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4queryenum_next(C4QueryEnumerator* e, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long c4queryenum_getRowCount(C4QueryEnumerator* e, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4queryenum_seek(C4QueryEnumerator* e, ulong rowIndex, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4QueryEnumerator* c4queryenum_refresh(C4QueryEnumerator* e, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4queryenum_close(C4QueryEnumerator* e);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4queryenum_free(C4QueryEnumerator* e);

        public static bool c4db_createIndex(C4Database* database, string name, string expressionsJSON, C4IndexType indexType, C4IndexOptions* indexOptions, C4Error* outError)
        {
            using(var name_ = new C4String(name))
            using(var expressionsJSON_ = new C4String(expressionsJSON)) {
                return NativeRaw.c4db_createIndex(database, name_.AsC4Slice(), expressionsJSON_.AsC4Slice(), indexType, indexOptions, outError);
            }
        }

        public static bool c4db_deleteIndex(C4Database* database, string name, C4Error* outError)
        {
            using(var name_ = new C4String(name)) {
                return NativeRaw.c4db_deleteIndex(database, name_.AsC4Slice(), outError);
            }
        }

        public static byte[] c4db_getIndexes(C4Database* database, C4Error* outError)
        {
            using(var retVal = NativeRaw.c4db_getIndexes(database, outError)) {
                return ((C4Slice)retVal).ToArrayFast();
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Query* c4query_new(C4Database* database, C4Slice expression, C4Error* error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4query_explain(C4Query* query);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4QueryEnumerator* c4query_run(C4Query* query, C4QueryOptions* options, C4Slice encodedParameters, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4query_fullTextMatched(C4Query* query, C4FullTextMatch* term, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_createIndex(C4Database* database, C4Slice name, C4Slice expressionsJSON, C4IndexType indexType, C4IndexOptions* indexOptions, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_deleteIndex(C4Database* database, C4Slice name, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4db_getIndexes(C4Database* database, C4Error* outError);


    }
}
