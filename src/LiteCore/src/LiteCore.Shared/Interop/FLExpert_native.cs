//
// FLExpert_native.cs
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
        public static FLValue* FLValue_FromData(byte[]? data, FLTrust trust)
        {
            fixed(byte *data_ = data) {
                return NativeRaw.FLValue_FromData(new FLSlice(data_, (ulong)data.Length), trust);
            }
        }

        public static string FLJSON5_ToJSON(string json5, FLSlice* outErrorMessage, UIntPtr* outErrPos, FLError* err)
        {
            using(var json5_ = new C4String(json5)) {
                using(var retVal = NativeRaw.FLJSON5_ToJSON((FLSlice)json5_.AsFLSlice(), outErrorMessage, outErrPos, err)) {
                    return ((FLSlice)retVal).CreateString();
                }
            }
        }
        public static byte[]? FLData_ConvertJSON(byte[]? json, FLError* outError)
        {
            fixed(byte *json_ = json) {
                using(var retVal = NativeRaw.FLData_ConvertJSON(new FLSlice(json_, (ulong)json.Length), outError)) {
                    return ((FLSlice)retVal).ToArrayFast();
                }
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLValue_FromData(FLSlice data, FLTrust trust);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLJSON5_ToJSON(FLSlice json5, FLSlice* outErrorMessage, UIntPtr* outErrorPos, FLError* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLData_ConvertJSON(FLSlice json, FLError* outError);


    }
}
