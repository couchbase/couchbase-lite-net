//
// FLJSON_native.cs
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

using System;
using System.Linq;
using System.Runtime.InteropServices;

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        public static string? FLValue_ToJSON(FLValue* value)
        {
            using(var retVal = NativeRaw.FLValue_ToJSON(value)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static string? FLValue_ToJSONX(FLValue* v, bool json5, bool canonicalForm)
        {
            using(var retVal = NativeRaw.FLValue_ToJSONX(v, json5, canonicalForm)) {
                return ((FLSlice)retVal).CreateString();
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSON(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSONX(FLValue* v, [MarshalAs(UnmanagedType.U1)]bool json5, [MarshalAs(UnmanagedType.U1)]bool canonicalForm);


    }
}
