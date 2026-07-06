// 
// DataOps.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Lite.Internal.Serialization;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc;

internal sealed class CouchbaseConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return CouchbaseJson.ReadValue(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        CouchbaseJson.WriteValue(writer, value);
    }
}

internal static class DataOps
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        // The source generated resolver keeps System.Text.Json off of its
        // reflection based code paths so that deserialization works under
        // Native AOT / trimming.  The converters do all of the actual work.
        TypeInfoResolver = CouchbaseJsonContext.Default,
        Converters = { new CouchbaseConverter(), new IArrayConverter(), new IDictionaryObjectConverter() },
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace
    };

    internal static T? ParseTo<T>(string json)
    {
        T? retVal;
        try {
            // The JsonTypeInfo overload is AOT safe; T must be registered on
            // CouchbaseJsonContext (or handled by a registered converter)
            retVal = (T?)JsonSerializer.Deserialize(json, SerializerOptions.GetTypeInfo(typeof(T)));
        } catch {
            throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter, CouchbaseLiteErrorMessage.InvalidJSON);
        }

        return retVal;
    }

    internal static bool ConvertToBoolean(object? value)
    {
        return value switch
        {
            null => false,
            string => true, // string is IConvertible, but will throw on things other than true or false
            IConvertible c => c.ToBoolean(CultureInfo.InvariantCulture),
            _ => true
        };
    }

    internal static DateTimeOffset ConvertToDate(object? value)
    {
        switch (value) {
            case null:
                return DateTimeOffset.MinValue;
            case DateTimeOffset dto:
                return dto;
            case DateTime dt:
                return new DateTimeOffset(dt.ToUniversalTime());
            case string s:
                if (DateTimeOffset.TryParseExact(s, "o", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var retVal)) {
                    return retVal;
                }

                return DateTimeOffset.MinValue;
            default:
                return DateTimeOffset.MinValue;
        }
    }

    internal static double ConvertToDouble(object? value)
    {
        // NOTE: Cannot use ConvertToDecimal because double has a greater range
        return value switch
        {
            string => 0.0, // string is IConvertible, but will throw for non-numeric strings
            IConvertible c => c.ToDouble(CultureInfo.InvariantCulture),
            _ => 0.0
        };
    }

    internal static float ConvertToFloat(object? value)
    {
        // NOTE: Cannot use ConvertToDecimal because float has a greater range
        return value switch
        {
            string => 0.0f, // string is IConvertible, but will throw for non-numeric strings
            IConvertible c => c.ToSingle(CultureInfo.InvariantCulture),
            _ => 0.0f
        };
    }

    internal static int ConvertToInt(object? value)
    {
        return (int)Math.Truncate(ConvertToDecimal(value));
    }

    internal static long ConvertToLong(object? value)
    {
        return (long)Math.Truncate(ConvertToDecimal(value));
    }

    //makes sure it is ArrayObject / DictionaryObject instead of List and Dictionary
    internal static object? ToCouchbaseObject(object? value)
    {
        switch (value) {
            case null:
                return null;
            case DateTimeOffset dto:
                return dto.ToString("o");
            case DictionaryObject rodic and not MutableDictionaryObject:
                return rodic.ToMutable();
            case ArrayObject roarr and not MutableArrayObject:
                return roarr.ToMutable();
            case JsonElement jobj:
                return ToCouchbaseObject(CouchbaseJson.ToNetObject(jobj));
            case IDictionary<string, object?> dict:
                return ConvertDictionary(dict);
            case IList list:
                return ConvertList(list);
            case byte:
            case sbyte:
            case ushort:
            case short:
            case uint:
            case int:
            case long:
            case ulong:
            case string:
            case bool:
            case float:
            case double:
            case MutableArrayObject:
            case MutableDictionaryObject:
            case Blob:
                return value;
            default:
                throw new ArgumentException(String.Format(CouchbaseLiteErrorMessage.InvalidCouchbaseObjType, value.GetType().Name,
                    "byte, sbyte, short, ushort, int, uint, long, ulong, float, double, bool, DateTimeOffset, Blob," ));
        }
    }

    internal static object? ToNetObject(object? value)
    {
        switch (value) {
            case null:
                return null;
            case InMemoryDictionary inMem:
                return inMem.ToDictionary();
            case IDictionaryObject roDic:
                return roDic.ToDictionary();
            case IArray roarr:
                return roarr.ToList();
            default:
                return value;
        }
    }

    internal static unsafe bool ValueWouldChange(object? newValue, MValue oldValue, MCollection container)
    {
        // As a simplification we assume that array and fict values are always different, to avoid
        // a possibly expensive comparison
        var oldType = Native.FLValue_GetType(oldValue.Value);
        if (oldType == FLValueType.Undefined || oldType == FLValueType.Dict || oldType == FLValueType.Array) {
            return true;
        }

        switch (newValue) {
            case ArrayObject:
            case DictionaryObject:
                return true;
            default:
                var oldVal = oldValue.AsObject(container);
                return !newValue?.Equals(oldVal) ?? oldVal != null;
        }
    }

    private static MutableDictionaryObject ConvertDictionary(IDictionary<string, object?> dictionary)
    {
        var subdocument = new MutableDictionaryObject();
        subdocument.SetData(dictionary);
        return subdocument;
    }

    private static MutableArrayObject ConvertList(IList list)
    {
        var array = new MutableArrayObject();
        array.SetData(list);
        return array;
    }

    private static decimal ConvertToDecimal(object? value)
    {
        return value switch
        {
            string => // string is IConvertible, but will throw for non-numeric strings
                0,
            IConvertible c => c.ToDecimal(CultureInfo.InvariantCulture),
            _ => 0
        };
    }
}