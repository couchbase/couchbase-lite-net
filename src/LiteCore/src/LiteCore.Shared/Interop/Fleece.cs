//
// Fleece.cs
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
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Doc;

namespace LiteCore.Interop;

internal unsafe interface IFLEncodable
{
    void FLEncode(FLEncoder* enc);
}

internal unsafe interface IFLSlotSettable
{
    void FLSlotSet(FLSlot* slot);
}

[ExcludeFromCodeCoverage]
internal unsafe partial struct FLSlice
{
    [SuppressMessage("ReSharper", "ConvertToPrimaryConstructor")]
    public FLSlice(void* buf, ulong size)
    {
        this.buf = buf;
        _size = (UIntPtr)size;
    }
    
    public static readonly FLSlice Null = new FLSlice(null, 0);

    private static readonly ConcurrentDictionary<string, FLSlice> Constants = new();

    public static FLSlice Constant(string input)
    {
        // Warning: This creates unmanaged memory that is intended never to be freed
        // You should only use it with constant strings
        return Constants.GetOrAdd(input, Allocate);
    }

    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public static FLSlice Allocate(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var intPtr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, intPtr, bytes.Length);
        return new FLSlice(intPtr.ToPointer(), (ulong)bytes.Length);
    }

    public static void Free(FLSlice slice)
    {
        Marshal.FreeHGlobal(new IntPtr(slice.buf));
        slice.buf = null;
        slice.size = 0;
    }

    public byte[]? ToArrayFast()
    {
        if (buf == null) {
            return null;
        }

        var tmp = new IntPtr(buf);
        var bytes = new byte[size];
        Marshal.Copy(tmp, bytes, 0, bytes.Length);
        return bytes;
    }

    public string? CreateString()
    {
        if(buf == null) {
            return null;
        }

        var tmp = new IntPtr(buf);
        var bytes = new byte[size];
        Marshal.Copy(tmp, bytes, 0, bytes.Length);
        return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
    }

    public override int GetHashCode()
    {
        var hasher = new HashCode();
        hasher.Add(size);
        var ptr = (byte*)buf;
        if (ptr != null) {
            hasher.Add(ptr[size - 1]);
        }

        return hasher.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        FLSlice other;
        switch (obj) {
            case FLSlice slice:
                other = slice;
                break;
            case FLSliceResult sliceResult:
                other = (FLSlice) sliceResult;
                break;
            case FLHeapSlice heapSlice:
                other = heapSlice;
                break;
            default:
                return false;
        }

        return Native.FLSlice_Compare(this, other) == 0;
    }

    public override string ToString() => $"FLSlice[{CreateString()}]";

    public static explicit operator FLSliceResult(FLSlice input)
    {
        return new FLSliceResult(input.buf, input.size);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal unsafe partial struct FLSliceResult(void* buf, ulong size) : IDisposable, IEquatable<FLSliceResult>
{
    public readonly void* buf = buf;
    private UIntPtr _size = (UIntPtr)size;

    public ulong size
    {
        get => _size.ToUInt64();
        set => _size = (UIntPtr)value;
    }

    public static explicit operator FLSlice(FLSliceResult input) => new(input.buf, input.size);

    public string? CreateString() => ((FLSlice)this).CreateString();

    public void Dispose() => Native.FLSliceResult_Release(this);

    public override int GetHashCode()
    {
        var hasher = new HashCode();
        hasher.Add(size);
        var ptr = (byte*)buf;
        if (ptr != null) {
            hasher.Add(ptr[size - 1]);
        }

        return hasher.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        FLSlice other;
        switch (obj) {
            case FLSlice slice:
                other = slice;
                break;
            case FLSliceResult sliceResult:
                other = (FLSlice)sliceResult;
                break;
            case FLHeapSlice heapSlice:
                other = heapSlice;
                break;
            default:
                return false;
        }

        return Native.FLSlice_Compare((FLSlice)this, other) == 0;
    }

    public override string ToString() => $"FLSliceResult[{CreateString()}]";
    
    public bool Equals(FLSliceResult other) => buf == other.buf && _size == other._size;
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal unsafe struct FLHeapSlice
{
 #pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    public void* buf;
 #pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    private UIntPtr _size;

    public ulong size
    {
        get => (ulong)_size;
        set => _size = (UIntPtr)value;
    }

    public static implicit operator FLSlice(FLHeapSlice input) => new(input.buf, input.size);

    public string? CreateString() => ((FLSlice) this).CreateString();

    public override int GetHashCode()
    {
        var hasher = new HashCode();
        hasher.Add(size);
        var ptr = (byte*)buf;
        if (ptr != null) {
            hasher.Add(ptr[size - 1]);
        }

        return hasher.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        FLSlice other;
        switch (obj) {
            case FLSlice slice:
                other = slice;
                break;
            case FLSliceResult sliceResult:
                other = (FLSlice) sliceResult;
                break;
            case FLHeapSlice heapSlice:
                other = heapSlice;
                break;
            default:
                return false;
        }

        return Native.FLSlice_Compare(this, other) == 0;
    }

    public override string ToString() => $"FLHeapSlice[{CreateString()}]";
}

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal static unsafe class FLSlotSetExt
{
    public static void FLSlotSet(this string str, FLSlot* slot) => Native.FLSlot_SetString(slot, str);

    public static void FLSlotSet(this bool b, FLSlot* slot) => Native.FLSlot_SetBool(slot, b);

    public static void FLSlotSet(this long l, FLSlot* slot) => Native.FLSlot_SetInt(slot, l);

    public static void FLSlotSet(this ulong ul, FLSlot* slot) => Native.FLSlot_SetUInt(slot, ul);

    public static void FLSlotSet(this float f, FLSlot* slot) => Native.FLSlot_SetFloat(slot, f);

    public static void FLSlotSet(this double d, FLSlot* slot) => Native.FLSlot_SetDouble(slot, d);

    public static void FLSlotSet<TVal>(this IDictionary<string, TVal> dict, FLSlot* slot) => 
        new MutableDictionaryObject((IDictionary<string, object?>)dict).FLSlotSet(slot);

    public static void FLSlotSet(this IList list, FLSlot* slot) => new MutableArrayObject(list).FLSlotSet(slot);

    public static void FLSlotSet(this object obj, FLSlot* slot)
    {
        switch (obj) {
            case null:
                Native.FLSlot_SetNull(slot);
                break;
            case IFLEncodable flObj:
                flObj.FLSlotSet(slot);
                break;
            case IDictionary<string, object> dict:
                dict.FLSlotSet(slot);
                break;
            case IDictionary<string, string> dict:
                dict.FLSlotSet(slot);
                break;
            case IEnumerable<byte> data:
                data.FLSlotSet(slot);
                break;
            case IList list:
                list.FLSlotSet(slot);
                break;
            case string s:
                s.FLSlotSet(slot);
                break;
            case byte or ushort or uint or ulong:
                var unsignedNumericVal = Convert.ToUInt64(obj);
                unsignedNumericVal.FLSlotSet(slot);
                break;
            case sbyte or short or int or long:
                var numericVal = Convert.ToInt64(obj);
                numericVal.FLSlotSet(slot);
                break;
            case float f:
                f.FLSlotSet(slot);
                break;
            case double d:
                d.FLSlotSet(slot);
                break;
            case bool b:
                b.FLSlotSet(slot);
                break;
            case DateTimeOffset dto:
                dto.ToString("o").FLSlotSet(slot);
                break;
            case ArrayObject arObj:
                arObj.ToMCollection().FLSlotSet(slot);
                break;
            case DictionaryObject roDict:
                roDict.ToMCollection().FLSlotSet(slot);
                break;
            case Blob b:
                b.FLSlotSet(slot);
                break;
            default:
                throw new ArgumentException($"Cannot encode {obj.GetType().FullName} to Fleece!");
        }
    }

}

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal static unsafe class FLSliceExtensions
{
    public static object? ToObject(FLValue* value)
    {
        if (value == null) {
            return null;
        }

        switch (Native.FLValue_GetType(value)) {
            case FLValueType.Array:
            {
                var arr = Native.FLValue_AsArray(value);
                var retVal = new object?[Native.FLArray_Count(arr)];
                if (retVal.Length == 0) {
                    return retVal;
                }
                    
                FLArrayIterator i;
                Native.FLArrayIterator_Begin(arr, &i);
                int pos = 0;
                do {
                    retVal[pos++] = ToObject(Native.FLArrayIterator_GetValue(&i));
                } while (Native.FLArrayIterator_Next(&i));

                return retVal;
            }
            case FLValueType.Boolean:
                return Native.FLValue_AsBool(value);
            case FLValueType.Data:
                return Native.FLValue_AsData(value);
            case FLValueType.Dict:
            {
                var dict = Native.FLValue_AsDict(value);
                var count = (int) Native.FLDict_Count(dict);
                var retVal = new Dictionary<string, object?>(count);
                if (count == 0) {
                    return retVal;
                }

                FLDictIterator i;
                Native.FLDictIterator_Begin(dict, &i);
                do {
                    var rawKey = Native.FLDictIterator_GetKey(&i);
                    string? key = Native.FLValue_AsString(rawKey);
                    if (key == null) {
                        break;
                    }

                    retVal[key] = ToObject(Native.FLDictIterator_GetValue(&i));
                } while (Native.FLDictIterator_Next(&i));

                return retVal;
            }
            case FLValueType.Null:
                return null;
            case FLValueType.Number:
                if (Native.FLValue_IsInteger(value)) {
                    if (Native.FLValue_IsUnsigned(value)) {
                        return Native.FLValue_AsUnsigned(value);
                    }

                    return Native.FLValue_AsInt(value);
                } else if (Native.FLValue_IsDouble(value)) {
                    return Native.FLValue_AsDouble(value);
                }

                return Native.FLValue_AsFloat(value);
            case FLValueType.String:
                return Native.FLValue_AsString(value);
            default:
                return null;
        }
    }

    public static FLSliceResult FLEncode(this object? obj)
    {
        var enc = Native.FLEncoder_New();
        try {
            obj.FLEncode(enc);
            FLError err;
            var retVal = NativeRaw.FLEncoder_Finish(enc, &err);
            if (retVal.buf == null) {
                throw new CouchbaseFleeceException(err);
            }

            return retVal;
        } finally {
            Native.FLEncoder_Free(enc);
        }
    }

    public static void FLEncode<TVal>(this IDictionary<string, TVal> dict, FLEncoder* enc)
    {
        Native.FLEncoder_BeginDict(enc, (ulong) dict.Count);
        foreach (var pair in dict) {
            Native.FLEncoder_WriteKey(enc, pair.Key);
            FLEncode(pair.Value, enc);
        }

        Native.FLEncoder_EndDict(enc);
    }

    public static void FLEncode(this IList list, FLEncoder* enc)
    {
        Native.FLEncoder_BeginArray(enc, (ulong)list.Count);
        foreach (var obj in list) {
            obj.FLEncode(enc);
        }

        Native.FLEncoder_EndArray(enc);
    }

    public static void FLEncode(this string str, FLEncoder* enc)
    {
        Native.FLEncoder_WriteString(enc, str);
    }

    public static void FLEncode(this IEnumerable<byte> str, FLEncoder* enc)
    {
        Native.FLEncoder_WriteData(enc, str.ToArray());
    }

    public static void FLEncode(this double d, FLEncoder* enc)
    {
        Native.FLEncoder_WriteDouble(enc, d);
    }

    public static void FLEncode(this float f, FLEncoder* enc)
    {
        Native.FLEncoder_WriteFloat(enc, f);
    }

    public static void FLEncode(this long l, FLEncoder* enc)
    {
        Native.FLEncoder_WriteInt(enc, l);
    }

    public static void FLEncode(this ulong u, FLEncoder* enc)
    {
        Native.FLEncoder_WriteUInt(enc, u);
    }

    public static void FLEncode(this bool b, FLEncoder* enc)
    {
        Native.FLEncoder_WriteBool(enc, b);
    }

    public static void FLEncode(this object? obj, FLEncoder* enc)
    {
        switch (obj) {
            case null:
                Native.FLEncoder_WriteNull(enc);
                break;
            case IFLEncodable flObj:
                flObj.FLEncode(enc);
                break;
            case IDictionary<string, object> dict:
                dict.FLEncode(enc);
                break;
            case IDictionary<string, string> dict:
                dict.FLEncode(enc);
                break;
            case IEnumerable<byte> data:
                data.FLEncode(enc);
                break;
            case IList list:
                list.FLEncode(enc);
                break;
            case string s:
                s.FLEncode(enc);
                break;
            case byte or ushort or uint or ulong:
                var unsignedNumericVal = Convert.ToUInt64(obj);
                unsignedNumericVal.FLEncode(enc);
                break;
            case sbyte or short or int or long:
                var numericVal = Convert.ToInt64(obj);
                numericVal.FLEncode(enc);
                break;
            case float f:
                f.FLEncode(enc);
                break;
            case double d:
                d.FLEncode(enc);
                break;
            case bool b:
                b.FLEncode(enc);
                break;
            case DateTimeOffset dto:
                (dto.ToString("o")).FLEncode(enc);
                break;
            case ArrayObject arObj:
                arObj.ToMCollection().FLEncode(enc);
                break;
            case DictionaryObject roDict:
                roDict.ToMCollection().FLEncode(enc);
                break;
            case Blob b:
                b.FLEncode(enc);
                break;
            case SecureString ss:
                new NetworkCredential(string.Empty, ss).Password.FLEncode(enc);
                break;
            case JsonElement jObj:
                switch (jObj.ValueKind) {
                    case JsonValueKind.Array:
                        jObj.Deserialize<IList>(DataOps.SerializerOptions)!.FLEncode();
                        break;
                    case JsonValueKind.Object:
                        jObj.Deserialize<IDictionary<string, object?>>(DataOps.SerializerOptions)!.FLEncode();
                        break;
                    case JsonValueKind.Number:
                        if (jObj.TryGetInt64(out var l)) {
                            Native.FLEncoder_WriteInt(enc, l);
                        } else {
                            Native.FLEncoder_WriteDouble(enc, jObj.GetDouble());
                        }
                        break;
                    case JsonValueKind.String:
                        Native.FLEncoder_WriteString(enc, jObj.GetString());
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        Native.FLEncoder_WriteBool(enc, jObj.GetBoolean());
                        break;
                    case JsonValueKind.Null:
                        Native.FLEncoder_WriteNull(enc);
                        break;
                    default:
                        throw new ArgumentException("Invalid JsonElement type: " + jObj.ValueKind);
                }
                break;
            default:
                throw new ArgumentException($"Cannot encode {obj.GetType().FullName} to Fleece!");
        }
    }


}