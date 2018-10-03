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
        public static FLDoc* FLDoc_FromData(byte[] data, FLTrust x, FLSharedKeys* shared, byte[] externData)
        {
            fixed(byte *data_ = data)
            fixed(byte *externData_ = externData) {
                return NativeRaw.FLDoc_FromData(new FLSlice(data_, (ulong)data.Length), x, shared, new FLSlice(externData_, (ulong)externData.Length));
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDoc* FLDoc_FromResultData(FLSliceResult data, FLTrust x, FLSharedKeys* shared, FLSlice externData);

        public static FLDoc* FLDoc_FromJSON(byte[] json, FLError* outError)
        {
            fixed(byte *json_ = json) {
                return NativeRaw.FLDoc_FromJSON(new FLSlice(json_, (ulong)json.Length), outError);
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDoc_Release(FLDoc* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDoc* FLDoc_Retain(FLDoc* x);

        public static byte[] FLDoc_GetData(FLDoc* x)
        {
            return (NativeRaw.FLDoc_GetData(x)).ToArrayFast();
        }

        public static byte[] FLDoc_GetAllocedData(FLDoc* x)
        {
            using(var retVal = NativeRaw.FLDoc_GetAllocedData(x)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDoc_GetRoot(FLDoc* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSharedKeys* FLDoc_GetSharedKeys(FLDoc* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
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

        public static string FLData_Dump(FLSlice data)
        {
            using(var retVal = NativeRaw.FLData_Dump(data)) {
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

        public static string FLValue_ToJSONX(FLValue* v, bool json5, bool canonicalForm)
        {
            using(var retVal = NativeRaw.FLValue_ToJSONX(v, json5, canonicalForm)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static string FLJSON5_ToJSON(string json5, FLError* error)
        {
            using(var json5_ = new C4String(json5)) {
                using(var retVal = NativeRaw.FLJSON5_ToJSON((FLSlice)json5_.AsFLSlice(), error)) {
                    return ((FLSlice)retVal).CreateString();
                }
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern char* FLDump(FLValue* value);

        public static char* FLDumpData(byte[] data)
        {
            fixed(byte *data_ = data) {
                return NativeRaw.FLDumpData(new FLSlice(data_, (ulong)data.Length));
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
            return (NativeRaw.FLValue_AsData(value)).ToArrayFast();
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

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLValue_Retain(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLValue_Release(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLArray_Count(FLArray* array);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLArray_IsEmpty(FLArray* array);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableArray FLArray_AsMutable(FLArray* array);

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
        public static extern FLMutableArray FLArray_MutableCopy(FLArray* array);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableArray FLMutableArray_New();

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLArray* FLMutableArray_GetSource(FLMutableArray x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLMutableArray_IsChanged(FLMutableArray x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_AppendNull(FLMutableArray x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_AppendBool(FLMutableArray x, [MarshalAs(UnmanagedType.U1)]bool b);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_AppendInt(FLMutableArray x, long l);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_AppendUInt(FLMutableArray x, ulong u);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_AppendFloat(FLMutableArray x, float f);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_AppendDouble(FLMutableArray x, double d);

        public static void FLMutableArray_AppendString(FLMutableArray x, string str)
        {
            using(var str_ = new C4String(str)) {
                NativeRaw.FLMutableArray_AppendString(x, (FLSlice)str_.AsFLSlice());
            }
        }

        public static void FLMutableArray_AppendData(FLMutableArray x, byte[] slice)
        {
            fixed(byte *slice_ = slice) {
                NativeRaw.FLMutableArray_AppendData(x, new FLSlice(slice_, (ulong)slice.Length));
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_AppendValue(FLMutableArray x, FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_SetNull(FLMutableArray x, uint index);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_SetBool(FLMutableArray x, uint index, [MarshalAs(UnmanagedType.U1)]bool b);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_SetInt(FLMutableArray x, uint index, long l);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_SetUInt(FLMutableArray x, uint index, ulong u);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_SetFloat(FLMutableArray x, uint index, float f);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_SetDouble(FLMutableArray x, uint index, double d);

        public static void FLMutableArray_SetString(FLMutableArray x, uint index, string str)
        {
            using(var str_ = new C4String(str)) {
                NativeRaw.FLMutableArray_SetString(x, index, (FLSlice)str_.AsFLSlice());
            }
        }

        public static void FLMutableArray_SetData(FLMutableArray x, uint index, byte[] slice)
        {
            fixed(byte *slice_ = slice) {
                NativeRaw.FLMutableArray_SetData(x, index, new FLSlice(slice_, (ulong)slice.Length));
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_SetValue(FLMutableArray x, uint index, FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_Remove(FLMutableArray array, uint firstIndex, uint count);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_Resize(FLMutableArray array, uint size);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableArray FLMutableArray_GetMutableArray(FLMutableArray x, uint index);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableDict FLMutableArray_GetMutableDict(FLMutableArray x, uint index);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLDict_Count(FLDict* dict);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLDict_IsEmpty(FLDict* dict);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableDict FLDict_AsMutable(FLDict* dict);

        public static FLValue* FLDict_Get(FLDict* dict, byte[] keyString)
        {
            fixed(byte *keyString_ = keyString) {
                return NativeRaw.FLDict_Get(dict, new FLSlice(keyString_, (ulong)keyString.Length));
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDictIterator_Begin(FLDict* dict, FLDictIterator* i);

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

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDictIterator_End(FLDictIterator* i);

        // Note: Allocates unmanaged heap memory; should only be used with constants
        public static FLDictKey FLDictKey_Init(string str)
        {
            return NativeRaw.FLDictKey_Init(FLSlice.Constant(str));
        }

        public static string FLDictKey_GetString(FLDictKey* dictKey)
        {
            return NativeRaw.FLDictKey_GetString(dictKey).CreateString();
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_GetWithKey(FLDict* dict, FLDictKey* dictKey);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableDict FLDict_MutableCopy(FLDict* source);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableDict FLMutableDict_New();

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDict* FLMutableDict_GetSource(FLMutableDict x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLMutableDict_IsChanged(FLMutableDict x);

        public static void FLMutableDict_SetNull(FLMutableDict x, string key)
        {
            using(var key_ = new C4String(key)) {
                NativeRaw.FLMutableDict_SetNull(x, (FLSlice)key_.AsFLSlice());
            }
        }

        public static void FLMutableDict_SetBool(FLMutableDict x, string key, bool b)
        {
            using(var key_ = new C4String(key)) {
                NativeRaw.FLMutableDict_SetBool(x, (FLSlice)key_.AsFLSlice(), b);
            }
        }

        public static void FLMutableDict_SetInt(FLMutableDict x, string key, long l)
        {
            using(var key_ = new C4String(key)) {
                NativeRaw.FLMutableDict_SetInt(x, (FLSlice)key_.AsFLSlice(), l);
            }
        }

        public static void FLMutableDict_SetUInt(FLMutableDict x, string key, ulong u)
        {
            using(var key_ = new C4String(key)) {
                NativeRaw.FLMutableDict_SetUInt(x, (FLSlice)key_.AsFLSlice(), u);
            }
        }

        public static void FLMutableDict_SetFloat(FLMutableDict x, string key, float f)
        {
            using(var key_ = new C4String(key)) {
                NativeRaw.FLMutableDict_SetFloat(x, (FLSlice)key_.AsFLSlice(), f);
            }
        }

        public static void FLMutableDict_SetDouble(FLMutableDict x, string key, double d)
        {
            using(var key_ = new C4String(key)) {
                NativeRaw.FLMutableDict_SetDouble(x, (FLSlice)key_.AsFLSlice(), d);
            }
        }

        public static void FLMutableDict_SetString(FLMutableDict x, string key, string str)
        {
            using(var key_ = new C4String(key))
            using(var str_ = new C4String(str)) {
                NativeRaw.FLMutableDict_SetString(x, (FLSlice)key_.AsFLSlice(), (FLSlice)str_.AsFLSlice());
            }
        }

        public static void FLMutableDict_SetData(FLMutableDict x, string key, byte[] slice)
        {
            using(var key_ = new C4String(key))
            fixed(byte *slice_ = slice) {
                NativeRaw.FLMutableDict_SetData(x, (FLSlice)key_.AsFLSlice(), new FLSlice(slice_, (ulong)slice.Length));
            }
        }

        public static void FLMutableDict_SetValue(FLMutableDict x, string key, FLValue* value)
        {
            using(var key_ = new C4String(key)) {
                NativeRaw.FLMutableDict_SetValue(x, (FLSlice)key_.AsFLSlice(), value);
            }
        }

        public static void FLMutableDict_Remove(FLMutableDict x, string key)
        {
            using(var key_ = new C4String(key)) {
                NativeRaw.FLMutableDict_Remove(x, (FLSlice)key_.AsFLSlice());
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_RemoveAll(FLMutableDict x);

        public static FLMutableArray FLMutableDict_GetMutableArray(FLMutableDict x, string key)
        {
            using(var key_ = new C4String(key)) {
                return NativeRaw.FLMutableDict_GetMutableArray(x, (FLSlice)key_.AsFLSlice());
            }
        }

        public static FLMutableDict FLMutableDict_GetMutableDict(FLMutableDict x, string key)
        {
            using(var key_ = new C4String(key)) {
                return NativeRaw.FLMutableDict_GetMutableDict(x, (FLSlice)key_.AsFLSlice());
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDeepIterator* FLDeepIterator_New(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDeepIterator_Free(FLDeepIterator* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDeepIterator_GetValue(FLDeepIterator* x);

        public static byte[] FLDeepIterator_GetKey(FLDeepIterator* x)
        {
            return (NativeRaw.FLDeepIterator_GetKey(x)).ToArrayFast();
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLDeepIterator_GetIndex(FLDeepIterator* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr FLDeepIterator_GetDepth(FLDeepIterator* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDeepIterator_SkipChildren(FLDeepIterator* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLDeepIterator_Next(FLDeepIterator* x);

        public static void FLDeepIterator_GetPath(FLDeepIterator* x, FLPathComponent** outPath, UIntPtr* outDepth)
        {
            NativeRaw.FLDeepIterator_GetPath(x, outPath, outDepth);
        }

        public static byte[] FLDeepIterator_GetPathString(FLDeepIterator* x)
        {
            using(var retVal = NativeRaw.FLDeepIterator_GetPathString(x)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        public static byte[] FLDeepIterator_GetJSONPointer(FLDeepIterator* x)
        {
            using(var retVal = NativeRaw.FLDeepIterator_GetJSONPointer(x)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        public static FLKeyPath* FLKeyPath_New(byte[] specifier, FLError* error)
        {
            fixed(byte *specifier_ = specifier) {
                return NativeRaw.FLKeyPath_New(new FLSlice(specifier_, (ulong)specifier.Length), error);
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLKeyPath_Free(FLKeyPath* keyPath);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLKeyPath_Eval(FLKeyPath* keyPath, FLValue* root);

        public static FLValue* FLKeyPath_EvalOnce(byte[] specifier, FLValue* root, FLError* error)
        {
            fixed(byte *specifier_ = specifier) {
                return NativeRaw.FLKeyPath_EvalOnce(new FLSlice(specifier_, (ulong)specifier.Length), root, error);
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSharedKeys* FLSharedKeys_Create();

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSharedKeys* FLSharedKeys_Retain(FLSharedKeys* shared);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLSharedKeys_Release(FLSharedKeys* shared);

        public static FLSharedKeys* FLSharedKeys_CreateFromStateData(byte[] slice)
        {
            fixed(byte *slice_ = slice) {
                return NativeRaw.FLSharedKeys_CreateFromStateData(new FLSlice(slice_, (ulong)slice.Length));
            }
        }

        public static byte[] FLSharedKeys_GetStateData(FLSharedKeys* shared)
        {
            using(var retVal = NativeRaw.FLSharedKeys_GetStateData(shared)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        public static int FLSharedKeys_Encode(FLSharedKeys* shared, string str, bool add)
        {
            using(var str_ = new C4String(str)) {
                return NativeRaw.FLSharedKeys_Encode(shared, (FLSlice)str_.AsFLSlice(), add);
            }
        }

        public static string FLSharedKeys_Decode(FLSharedKeys* shared, int key)
        {
            return NativeRaw.FLSharedKeys_Decode(shared, key).CreateString();
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLEncoder* FLEncoder_New();

        public static FLEncoder* FLEncoder_NewWithOptions(FLEncoderFormat format, ulong reserveSize, bool uniqueStrings)
        {
            return NativeRaw.FLEncoder_NewWithOptions(format, (UIntPtr)reserveSize, uniqueStrings);
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_Free(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_SetSharedKeys(FLEncoder* encoder, FLSharedKeys* shared);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_SetExtraInfo(FLEncoder* encoder, void* info);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void* FLEncoder_GetExtraInfo(FLEncoder* encoder);

        public static void FLEncoder_Amend(FLEncoder* e, byte[] @base, bool reuseStrings, bool externPointers)
        {
            fixed(byte *@base_ = @base) {
                NativeRaw.FLEncoder_Amend(e, new FLSlice(@base_, (ulong)@base.Length), reuseStrings, externPointers);
            }
        }

        public static byte[] FLEncoder_GetBase(FLEncoder* encoder)
        {
            return (NativeRaw.FLEncoder_GetBase(encoder)).ToArrayFast();
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_SuppressTrailer(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_Reset(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr FLEncoder_BytesWritten(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr FLEncoder_GetNextWritePos(FLEncoder* encoder);

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
                return NativeRaw.FLEncoder_WriteString(encoder, (FLSlice)str_.AsFLSlice());
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
                return NativeRaw.FLEncoder_WriteKey(encoder, (FLSlice)str_.AsFLSlice());
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_EndDict(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncoder_WriteValue(FLEncoder* encoder, FLValue* value);

        public static bool FLEncoder_ConvertJSON(FLEncoder* encoder, byte[] json)
        {
            fixed(byte *json_ = json) {
                return NativeRaw.FLEncoder_ConvertJSON(encoder, new FLSlice(json_, (ulong)json.Length));
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr FLEncoder_FinishItem(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDoc* FLEncoder_FinishDoc(FLEncoder* encoder, FLError* outError);

        public static byte[] FLEncoder_Finish(FLEncoder* e, FLError* outError)
        {
            using(var retVal = NativeRaw.FLEncoder_Finish(e, outError)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLError FLEncoder_GetError(FLEncoder* encoder);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string FLEncoder_GetErrorMessage(FLEncoder* encoder);

        public static byte[] FLCreateJSONDelta(FLValue* old, FLValue* nuu)
        {
            using(var retVal = NativeRaw.FLCreateJSONDelta(old, nuu)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncodeJSONDelta(FLValue* old, FLValue* nuu, FLEncoder* jsonEncoder);

        public static byte[] FLApplyJSONDelta(FLValue* old, byte[] jsonDelta, FLError* error)
        {
            fixed(byte *jsonDelta_ = jsonDelta) {
                using(var retVal = NativeRaw.FLApplyJSONDelta(old, new FLSlice(jsonDelta_, (ulong)jsonDelta.Length), error)) {
                    return ((FLSlice)retVal).ToArrayFast();
                }
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLEncodeApplyingJSONDelta(FLValue* old, FLValue* jsonDelta, FLEncoder* encoder);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDoc* FLDoc_FromData(FLSlice data, FLTrust x, FLSharedKeys* shared, FLSlice externData);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDoc* FLDoc_FromJSON(FLSlice json, FLError* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDoc_GetData(FLDoc* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLDoc_GetAllocedData(FLDoc* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLValue_FromData(FLSlice data, FLTrust x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLData_ConvertJSON(FLSlice json, FLError* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLData_Dump(FLSlice data);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSON(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSON5(FLValue* v);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToJSONX(FLValue* v, [MarshalAs(UnmanagedType.U1)]bool json5, [MarshalAs(UnmanagedType.U1)]bool canonicalForm);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLJSON5_ToJSON(FLSlice json5, FLError* error);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern char* FLDumpData(FLSlice data);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLValue_AsString(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLValue_AsData(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToString(FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_AppendString(FLMutableArray x, FLSlice str);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_AppendData(FLMutableArray x, FLSlice slice);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_SetString(FLMutableArray x, uint index, FLSlice str);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableArray_SetData(FLMutableArray x, uint index, FLSlice slice);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_Get(FLDict* dict, FLSlice keyString);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDictIterator_GetKeyString(FLDictIterator* i);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDictKey FLDictKey_Init(FLSlice @string);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDictKey_GetString(FLDictKey* dictKey);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_SetNull(FLMutableDict x, FLSlice key);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_SetBool(FLMutableDict x, FLSlice key, [MarshalAs(UnmanagedType.U1)]bool b);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_SetInt(FLMutableDict x, FLSlice key, long l);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_SetUInt(FLMutableDict x, FLSlice key, ulong u);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_SetFloat(FLMutableDict x, FLSlice key, float f);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_SetDouble(FLMutableDict x, FLSlice key, double d);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_SetString(FLMutableDict x, FLSlice key, FLSlice str);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_SetData(FLMutableDict x, FLSlice key, FLSlice slice);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_SetValue(FLMutableDict x, FLSlice key, FLValue* value);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLMutableDict_Remove(FLMutableDict x, FLSlice key);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableArray FLMutableDict_GetMutableArray(FLMutableDict x, FLSlice key);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableDict FLMutableDict_GetMutableDict(FLMutableDict x, FLSlice key);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDeepIterator_GetKey(FLDeepIterator* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDeepIterator_GetPath(FLDeepIterator* x, FLPathComponent** outPath, UIntPtr* outDepth);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLDeepIterator_GetPathString(FLDeepIterator* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLDeepIterator_GetJSONPointer(FLDeepIterator* x);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLKeyPath* FLKeyPath_New(FLSlice specifier, FLError* error);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLKeyPath_EvalOnce(FLSlice specifier, FLValue* root, FLError* error);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSharedKeys* FLSharedKeys_CreateFromStateData(FLSlice slice);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLSharedKeys_GetStateData(FLSharedKeys* shared);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FLSharedKeys_Encode(FLSharedKeys* shared, FLSlice str, [MarshalAs(UnmanagedType.U1)]bool add);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLSharedKeys_Decode(FLSharedKeys* shared, int key);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLEncoder* FLEncoder_NewWithOptions(FLEncoderFormat format, UIntPtr reserveSize, [MarshalAs(UnmanagedType.U1)]bool uniqueStrings);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLEncoder_Amend(FLEncoder* e, FLSlice @base, [MarshalAs(UnmanagedType.U1)]bool reuseStrings, [MarshalAs(UnmanagedType.U1)]bool externPointers);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLEncoder_GetBase(FLEncoder* encoder);

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
        public static extern bool FLEncoder_ConvertJSON(FLEncoder* encoder, FLSlice json);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLEncoder_Finish(FLEncoder* e, FLError* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLCreateJSONDelta(FLValue* old, FLValue* nuu);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLApplyJSONDelta(FLValue* old, FLSlice jsonDelta, FLError* error);


    }
}
