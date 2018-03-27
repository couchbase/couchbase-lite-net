//
// Fleece_native_ios.cs
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
        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLSliceResult_Free(FLSliceResult slice);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLSlice_Equal(FLSlice a, FLSlice b);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FLSlice_Compare(FLSlice left, FLSlice right);

        public static FLValue* FLValue_FromData(byte[] data)
        {
            fixed(byte *data_ = data) {
                return NativeRaw.FLValue_FromData(new FLSlice(data_, (ulong)data.Length));
            }
        }

        public static FLValue* FLValue_FromTrustedData(byte[] data)
        {
            fixed(byte *data_ = data) {
                return NativeRaw.FLValue_FromTrustedData(new FLSlice(data_, (ulong)data.Length));
            }
        }

        public static byte[] FLData_ConvertJSON(byte[] json, FLError* outError)
        {
            fixed(byte *json_ = json) {
                using(var retVal = NativeRaw.FLData_ConvertJSON(new FLSlice(json_, (ulong)json.Length), outError)) {
                    return ((C4Slice)retVal).ToArrayFast();
                }
            }
        }

        public static string FLData_Dump(FLSlice data)
        {
            using(var retVal = NativeRaw.FLData_Dump(data)) {
                return ((FLSlice)retVal).CreateString();
            }
        }


        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValueType FLValue_GetType(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_IsInteger(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_IsUnsigned(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_IsDouble(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_AsBool(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern long FLValue_AsInt(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong FLValue_AsUnsigned(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern float FLValue_AsFloat(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern double FLValue_AsDouble(FLValue* value);

        public static string FLValue_AsString(FLValue* value)
        {
            return NativeRaw.FLValue_AsString(value).CreateString();
        }

        public static byte[] FLValue_AsData(FLValue* value)
        {
            return ((C4Slice)NativeRaw.FLValue_AsData(value)).ToArrayFast();
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLArray* FLValue_AsArray(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDict* FLValue_AsDict(FLValue* value);

        public static string FLValue_ToString(FLValue* value)
        {
            using(var retVal = NativeRaw.FLValue_ToString(value)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static string FLValue_ToJSON(FLValue* value)
        {
            using(var retVal = NativeRaw.FLValue_ToJSON(value)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static string FLValue_ToJSON5(FLValue* v)
        {
            using(var retVal = NativeRaw.FLValue_ToJSON5(v)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static string FLValue_ToJSONX(FLValue* v, FLSharedKeys* sk, bool json5, bool canonicalForm)
        {
            using(var retVal = NativeRaw.FLValue_ToJSONX(v, sk, json5, canonicalForm)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static string FLJSON5_ToJSON(string json5, FLError* error)
        {
            using(var json5_ = new C4String(json5)) {
                using(var retVal = NativeRaw.FLJSON5_ToJSON((FLSlice)json5_.AsC4Slice(), error)) {
                    return ((FLSlice)retVal).CreateString();
                }
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLArray_Count(FLArray* array);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLArray_IsEmpty(FLArray* array);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLArray_Get(FLArray* array, uint index);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLArrayIterator_Begin(FLArray* array, FLArrayIterator* i);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLArrayIterator_GetValue(FLArrayIterator* i);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLArrayIterator_GetValueAt(FLArrayIterator* i, uint offset);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLArrayIterator_GetCount(FLArrayIterator* i);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLArrayIterator_Next(FLArrayIterator* i);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLDict_Count(FLDict* dict);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLDict_IsEmpty(FLDict* dict);

        public static FLValue* FLDict_Get(FLDict* dict, byte[] keyString)
        {
            fixed(byte *keyString_ = keyString) {
                return NativeRaw.FLDict_Get(dict, new FLSlice(keyString_, (ulong)keyString.Length));
            }
        }

        public static FLValue* FLDict_GetSharedKey(FLDict* d, byte[] keyString, FLSharedKeys* sk)
        {
            fixed(byte *keyString_ = keyString) {
                return NativeRaw.FLDict_GetSharedKey(d, new FLSlice(keyString_, (ulong)keyString.Length), sk);
            }
        }

        public static string FLSharedKey_GetKeyString(FLSharedKeys* sk, int keyCode, FLError* outError)
        {
            return NativeRaw.FLSharedKey_GetKeyString(sk, keyCode, outError).CreateString();
        }

        public static FLValue* FLDict_GetUnsorted(FLDict* dict, byte[] keyString)
        {
            fixed(byte *keyString_ = keyString) {
                return NativeRaw.FLDict_GetUnsorted(dict, new FLSlice(keyString_, (ulong)keyString.Length));
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDictIterator_Begin(FLDict* dict, FLDictIterator* i);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDictIterator_BeginShared(FLDict* dict, FLDictIterator* i, FLSharedKeys* shared);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDictIterator_GetKey(FLDictIterator* i);

        public static string FLDictIterator_GetKeyString(FLDictIterator* i)
        {
            return NativeRaw.FLDictIterator_GetKeyString(i).CreateString();
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDictIterator_GetValue(FLDictIterator* i);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLDictIterator_GetCount(FLDictIterator* i);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLDictIterator_Next(FLDictIterator* i);

        // Note: Allocates unmanaged heap memory; should only be used with constants
        public static FLDictKey FLDictKey_Init(string str, bool cachePointers)
        {
            return NativeRaw.FLDictKey_Init(FLSlice.Constant(str), cachePointers);
        }

        // Note: Allocates unmanaged heap memory; should only be used with constants
        public static FLDictKey FLDictKey_InitWithSharedKeys(string str, FLSharedKeys* sk)
        {
            return NativeRaw.FLDictKey_InitWithSharedKeys(FLSlice.Constant(str), sk);
        }

        public static string FLDictKey_GetString(FLDictKey* key)
        {
            return NativeRaw.FLDictKey_GetString(key).CreateString();
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_GetWithKey(FLDict* dict, FLDictKey* dictKey);

        public static ulong FLDict_GetWithKeys(FLDict* dict, FLDictKey[] keys, FLValue[] values, ulong count)
        {
            return NativeRaw.FLDict_GetWithKeys(dict, keys, values, (UIntPtr)count).ToUInt64();
        }

        public static FLKeyPath* FLKeyPath_New(byte[] specifier, FLSharedKeys* shared, FLError* error)
        {
            fixed(byte *specifier_ = specifier) {
                return NativeRaw.FLKeyPath_New(new FLSlice(specifier_, (ulong)specifier.Length), shared, error);
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLKeyPath_Free(FLKeyPath* keyPath);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLKeyPath_Eval(FLKeyPath* keyPath, FLValue* root);

        public static FLValue* FLKeyPath_EvalOnce(byte[] specifier, FLSharedKeys* shared, FLValue* root, FLError* error)
        {
            fixed(byte *specifier_ = specifier) {
                return NativeRaw.FLKeyPath_EvalOnce(new FLSlice(specifier_, (ulong)specifier.Length), shared, root, error);
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLEncoder* FLEncoder_New();

        public static FLEncoder* FLEncoder_NewWithOptions(FLEncoderFormat format, ulong reserveSize, bool uniqueStrings, bool sortKeys)
        {
            return NativeRaw.FLEncoder_NewWithOptions(format, (UIntPtr)reserveSize, uniqueStrings, sortKeys);
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_Free(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_SetSharedKeys(FLEncoder* encoder, FLSharedKeys* shared);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_SetExtraInfo(FLEncoder* e, void* info);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void* FLEncoder_GetExtraInfo(FLEncoder* e);

        public static void FLEncoder_MakeDelta(FLEncoder* e, byte[] @base, bool reuseStrings)
        {
            fixed(byte *@base_ = @base) {
                NativeRaw.FLEncoder_MakeDelta(e, new FLSlice(@base_, (ulong)@base.Length), reuseStrings);
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_Reset(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteNull(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteBool(FLEncoder* encoder, [MarshalAs(UnmanagedType.U1)]bool b);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteInt(FLEncoder* encoder, long l);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteUInt(FLEncoder* encoder, ulong u);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteFloat(FLEncoder* encoder, float f);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteDouble(FLEncoder* encoder, double d);

        public static bool FLEncoder_WriteString(FLEncoder* encoder, string str)
        {
            using(var str_ = new C4String(str)) {
                return NativeRaw.FLEncoder_WriteString(encoder, (FLSlice)str_.AsC4Slice());
            }
        }

        public static bool FLEncoder_WriteData(FLEncoder* encoder, byte[] slice)
        {
            fixed(byte *slice_ = slice) {
                return NativeRaw.FLEncoder_WriteData(encoder, new FLSlice(slice_, (ulong)slice.Length));
            }
        }

        public static bool FLEncoder_WriteRaw(FLEncoder* encoder, byte[] slice)
        {
            fixed(byte *slice_ = slice) {
                return NativeRaw.FLEncoder_WriteRaw(encoder, new FLSlice(slice_, (ulong)slice.Length));
            }
        }

        public static bool FLEncoder_BeginArray(FLEncoder* encoder, ulong reserveCount)
        {
            return NativeRaw.FLEncoder_BeginArray(encoder, (UIntPtr)reserveCount);
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_EndArray(FLEncoder* encoder);

        public static bool FLEncoder_BeginDict(FLEncoder* encoder, ulong reserveCount)
        {
            return NativeRaw.FLEncoder_BeginDict(encoder, (UIntPtr)reserveCount);
        }

        public static bool FLEncoder_WriteKey(FLEncoder* encoder, string str)
        {
            using(var str_ = new C4String(str)) {
                return NativeRaw.FLEncoder_WriteKey(encoder, (FLSlice)str_.AsC4Slice());
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_EndDict(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteValue(FLEncoder* encoder, FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteValueWithSharedKeys(FLEncoder* encoder, FLValue* value, FLSharedKeys* shared);

        public static bool FLEncoder_ConvertJSON(FLEncoder* e, byte[] json)
        {
            fixed(byte *json_ = json) {
                return NativeRaw.FLEncoder_ConvertJSON(e, new FLSlice(json_, (ulong)json.Length));
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr FLEncoder_BytesWritten(FLEncoder* e);

        public static byte[] FLEncoder_Finish(FLEncoder* encoder, FLError* outError)
        {
            using(var retVal = NativeRaw.FLEncoder_Finish(encoder, outError)) {
                return ((C4Slice)retVal).ToArrayFast();
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLError FLEncoder_GetError(FLEncoder* e);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string FLEncoder_GetErrorMessage(FLEncoder* encoder);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLValue_FromData(FLSlice data);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLValue_FromTrustedData(FLSlice data);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLData_ConvertJSON(FLSlice json, FLError* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLData_Dump(FLSlice data);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLValue_AsString(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLValue_AsData(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToString(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSON(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSON5(FLValue* v);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSONX(FLValue* v, FLSharedKeys* sk, [MarshalAs(UnmanagedType.U1)]bool json5, [MarshalAs(UnmanagedType.U1)]bool canonicalForm);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLJSON5_ToJSON(FLSlice json5, FLError* error);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_Get(FLDict* dict, FLSlice keyString);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_GetSharedKey(FLDict* d, FLSlice keyString, FLSharedKeys* sk);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLSharedKey_GetKeyString(FLSharedKeys* sk, int keyCode, FLError* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_GetUnsorted(FLDict* dict, FLSlice keyString);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDictIterator_GetKeyString(FLDictIterator* i);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDictKey FLDictKey_Init(FLSlice @string, [MarshalAs(UnmanagedType.U1)]bool cachePointers);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDictKey FLDictKey_InitWithSharedKeys(FLSlice @string, FLSharedKeys* sharedKeys);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDictKey_GetString(FLDictKey* key);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr FLDict_GetWithKeys(FLDict* dict, [Out]FLDictKey[] keys, [Out]FLValue[] values, UIntPtr count);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLKeyPath* FLKeyPath_New(FLSlice specifier, FLSharedKeys* shared, FLError* error);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLKeyPath_EvalOnce(FLSlice specifier, FLSharedKeys* shared, FLValue* root, FLError* error);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLEncoder* FLEncoder_NewWithOptions(FLEncoderFormat format, UIntPtr reserveSize, [MarshalAs(UnmanagedType.U1)]bool uniqueStrings, [MarshalAs(UnmanagedType.U1)]bool sortKeys);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_MakeDelta(FLEncoder* e, FLSlice @base, [MarshalAs(UnmanagedType.U1)]bool reuseStrings);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteString(FLEncoder* encoder, FLSlice str);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteData(FLEncoder* encoder, FLSlice slice);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteRaw(FLEncoder* encoder, FLSlice slice);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_BeginArray(FLEncoder* encoder, UIntPtr reserveCount);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_BeginDict(FLEncoder* encoder, UIntPtr reserveCount);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteKey(FLEncoder* encoder, FLSlice str);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_ConvertJSON(FLEncoder* e, FLSlice json);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLEncoder_Finish(FLEncoder* encoder, FLError* outError);


    }
}
