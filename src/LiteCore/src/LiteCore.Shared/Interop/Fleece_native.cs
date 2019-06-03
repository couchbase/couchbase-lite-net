//
// Fleece_native.cs
//
// Copyright (c) 2019 Couchbase, Inc All rights reserved.
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
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDoc* FLDoc_FromResultData(FLSliceResult data, FLTrust x, FLSharedKeys* shared, FLSlice externData);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDoc_Release(FLDoc* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDoc_GetRoot(FLDoc* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSharedKeys* FLDoc_GetSharedKeys(FLDoc* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDoc* FLValue_FindDoc(FLValue* value);

        public static FLValue* FLValue_FromData(byte[] data, FLTrust x)
        {
            fixed(byte *data_ = data) {
                return NativeRaw.FLValue_FromData(new FLSlice(data_, (ulong)data.Length), x);
            }
        }

        public static byte[] FLData_ConvertJSON(byte[] json, FLError* outError)
        {
            fixed(byte *json_ = json) {
                using(var retVal = NativeRaw.FLData_ConvertJSON(new FLSlice(json_, (ulong)json.Length), outError)) {
                    return ((FLSlice)retVal).ToArrayFast();
                }
            }
        }

        public static string FLValue_ToJSON(FLValue* value)
        {
            using(var retVal = NativeRaw.FLValue_ToJSON(value)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static string FLValue_ToJSONX(FLValue* v, bool json5, bool canonicalForm)
        {
            using(var retVal = NativeRaw.FLValue_ToJSONX(v, json5, canonicalForm)) {
                return ((FLSlice)retVal).CreateString();
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
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValueType FLValue_GetType(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_IsInteger(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_IsUnsigned(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_IsDouble(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_AsBool(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long FLValue_AsInt(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong FLValue_AsUnsigned(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float FLValue_AsFloat(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern double FLValue_AsDouble(FLValue* value);

        public static string FLValue_AsString(FLValue* value)
        {
            return NativeRaw.FLValue_AsString(value).CreateString();
        }

        public static byte[] FLValue_AsData(FLValue* value)
        {
            return (NativeRaw.FLValue_AsData(value)).ToArrayFast();
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLArray* FLValue_AsArray(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDict* FLValue_AsDict(FLValue* value);

        public static string FLValue_ToString(FLValue* value)
        {
            using(var retVal = NativeRaw.FLValue_ToString(value)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLArray_Count(FLArray* array);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLArray_Get(FLArray* array, uint index);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLArrayIterator_Begin(FLArray* array, FLArrayIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLArrayIterator_GetValue(FLArrayIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLArrayIterator_GetValueAt(FLArrayIterator* i, uint offset);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLArrayIterator_GetCount(FLArrayIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLArrayIterator_Next(FLArrayIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLDict_Count(FLDict* dict);

        public static FLValue* FLDict_Get(FLDict* dict, byte[] keyString)
        {
            fixed(byte *keyString_ = keyString) {
                return NativeRaw.FLDict_Get(dict, new FLSlice(keyString_, (ulong)keyString.Length));
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDictIterator_Begin(FLDict* dict, FLDictIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDictIterator_GetKey(FLDictIterator* i);

        public static string FLDictIterator_GetKeyString(FLDictIterator* i)
        {
            return NativeRaw.FLDictIterator_GetKeyString(i).CreateString();
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDictIterator_GetValue(FLDictIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLDictIterator_Next(FLDictIterator* i);

        // Note: Allocates unmanaged heap memory; should only be used with constants
        public static FLDictKey FLDictKey_Init(string str)
        {
            return NativeRaw.FLDictKey_Init(FLSlice.Constant(str));
        }

        public static string FLDictKey_GetString(FLDictKey* dictKey)
        {
            return NativeRaw.FLDictKey_GetString(dictKey).CreateString();
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_GetWithKey(FLDict* dict, FLDictKey* dictKey);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLEncoder* FLEncoder_New();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_Free(FLEncoder* encoder);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_SetExtraInfo(FLEncoder* encoder, void* info);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void* FLEncoder_GetExtraInfo(FLEncoder* encoder);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_Reset(FLEncoder* encoder);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteNull(FLEncoder* encoder);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteBool(FLEncoder* encoder, [MarshalAs(UnmanagedType.U1)]bool b);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteInt(FLEncoder* encoder, long l);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteUInt(FLEncoder* encoder, ulong u);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteFloat(FLEncoder* encoder, float f);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteDouble(FLEncoder* encoder, double d);

        public static bool FLEncoder_WriteString(FLEncoder* encoder, string str)
        {
            using(var str_ = new C4String(str)) {
                return NativeRaw.FLEncoder_WriteString(encoder, (FLSlice)str_.AsFLSlice());
            }
        }

        public static bool FLEncoder_WriteData(FLEncoder* encoder, byte[] slice)
        {
            fixed(byte *slice_ = slice) {
                return NativeRaw.FLEncoder_WriteData(encoder, new FLSlice(slice_, (ulong)slice.Length));
            }
        }

        public static bool FLEncoder_BeginArray(FLEncoder* encoder, ulong reserveCount)
        {
            return NativeRaw.FLEncoder_BeginArray(encoder, (UIntPtr)reserveCount);
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_EndArray(FLEncoder* encoder);

        public static bool FLEncoder_BeginDict(FLEncoder* encoder, ulong reserveCount)
        {
            return NativeRaw.FLEncoder_BeginDict(encoder, (UIntPtr)reserveCount);
        }

        public static bool FLEncoder_WriteKey(FLEncoder* encoder, string str)
        {
            using(var str_ = new C4String(str)) {
                return NativeRaw.FLEncoder_WriteKey(encoder, (FLSlice)str_.AsFLSlice());
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_EndDict(FLEncoder* encoder);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteValue(FLEncoder* encoder, FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDoc* FLEncoder_FinishDoc(FLEncoder* encoder, FLError* outError);

        public static byte[] FLEncoder_Finish(FLEncoder* e, FLError* outError)
        {
            using(var retVal = NativeRaw.FLEncoder_Finish(e, outError)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLValue_FromData(FLSlice data, FLTrust x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLData_ConvertJSON(FLSlice json, FLError* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSON(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSONX(FLValue* v, [MarshalAs(UnmanagedType.U1)]bool json5, [MarshalAs(UnmanagedType.U1)]bool canonicalForm);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLJSON5_ToJSON(FLSlice json5, FLSlice* outErrorMessage, UIntPtr* outErrorPos, FLError* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLValue_AsString(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLValue_AsData(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToString(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_Get(FLDict* dict, FLSlice keyString);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDictIterator_GetKeyString(FLDictIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDictKey FLDictKey_Init(FLSlice @string);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDictKey_GetString(FLDictKey* dictKey);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteString(FLEncoder* encoder, FLSlice str);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteData(FLEncoder* encoder, FLSlice slice);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_BeginArray(FLEncoder* encoder, UIntPtr reserveCount);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_BeginDict(FLEncoder* encoder, UIntPtr reserveCount);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteKey(FLEncoder* encoder, FLSlice str);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLEncoder_Finish(FLEncoder* e, FLError* outError);


    }
}
