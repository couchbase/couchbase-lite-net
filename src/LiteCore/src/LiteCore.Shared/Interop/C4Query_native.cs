//
// C4Query_native.cs
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

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        public static C4Query* c4query_new2(C4Database* database, C4QueryLanguage language, string expression, int* outErrorPos, C4Error* error)
        {
            using(var expression_ = new C4String(expression)) {
                return NativeRaw.c4query_new2(database, language, expression_.AsFLSlice(), outErrorPos, error);
            }
        }

        public static C4Query* c4query_new(C4Database* db, string str, C4Error* outError)
        {
            using(var str_ = new C4String(str)) {
                return NativeRaw.c4query_new(db, str_.AsFLSlice(), outError);
            }
        }

        public static string c4query_explain(C4Query* query)
        {
            using(var retVal = NativeRaw.c4query_explain(query)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint c4query_columnCount(C4Query* query);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice c4query_columnTitle(C4Query* query, uint column);

        public static void c4query_setParameters(C4Query* query, string encodedParameters)
        {
            using(var encodedParameters_ = new C4String(encodedParameters)) {
                NativeRaw.c4query_setParameters(query, encodedParameters_.AsFLSlice());
            }
        }

        public static C4QueryEnumerator* c4query_run(C4Query* query, C4QueryOptions* options, string encodedParameters, C4Error* outError)
        {
            using(var encodedParameters_ = new C4String(encodedParameters)) {
                return NativeRaw.c4query_run(query, options, encodedParameters_.AsFLSlice(), outError);
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4queryenum_next(C4QueryEnumerator* e, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4queryenum_seek(C4QueryEnumerator* e, long rowIndex, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4QueryEnumerator* c4queryenum_refresh(C4QueryEnumerator* e, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4queryenum_close(C4QueryEnumerator* x);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Query* c4query_new2(C4Database* database, C4QueryLanguage language, FLSlice expression, int* outErrorPos, C4Error* error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Query* c4query_new(C4Database* db, FLSlice str, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4query_explain(C4Query* query);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4query_setParameters(C4Query* query, FLSlice encodedParameters);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4QueryEnumerator* c4query_run(C4Query* query, C4QueryOptions* options, FLSlice encodedParameters, C4Error* outError);


    }
}
