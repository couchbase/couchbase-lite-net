//
//  CouchbaseJson.cs
//
//  Copyright (c) 2026 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Couchbase.Lite.Internal.Serialization;

// Source generated serializer metadata for the root types that Couchbase Lite
// deserializes JSON into (see DataOps.ParseTo<T>).  Using a JsonSerializerContext
// as the TypeInfoResolver keeps System.Text.Json away from its reflection based
// resolver so the library remains functional under Native AOT / trimming.  All
// values inside these roots are handled by the custom converters registered on
// DataOps.SerializerOptions, which are entirely hand written.
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(IDictionary<string, object>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(IList<object>))]
[JsonSerializable(typeof(IList))]
[JsonSerializable(typeof(IList<IList<object>>))]
internal sealed partial class CouchbaseJsonContext : JsonSerializerContext
{
}

// Reflection free JSON reading and writing for the closed set of value types that
// Couchbase Lite supports (see DataOps.ToCouchbaseObject).  These methods replace
// the reflection based JsonSerializer code paths, which are unavailable under
// Native AOT and produce trim warnings (IL2026 / IL3050).
internal static class CouchbaseJson
{
    // Serializes a tree of Couchbase supported values to a JSON string.
    internal static string Serialize(object? value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            WriteValue(writer, value);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // Serialization for logging purposes.  Never throws on unsupported types so
    // that a log call cannot take down the library.
    internal static string SerializeLenient(object? value)
    {
        try {
            return Serialize(value);
        } catch (ArgumentException) {
            return value?.ToString() ?? "null";
        }
    }

    // Writes a single value of any Couchbase supported type, recursing into
    // dictionaries and collections.
    internal static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value) {
            case null:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case byte or sbyte or short or ushort or int or uint or long:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case ulong ul:
                writer.WriteNumberValue(ul);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case decimal dec:
                writer.WriteNumberValue(dec);
                break;
            case DateTimeOffset dto:
                writer.WriteStringValue(dto);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt);
                break;
            case byte[] data:
                writer.WriteBase64StringValue(data);
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case Blob blob:
                WriteValue(writer, blob.JsonRepresentation);
                break;
            case IDictionaryObject dictObject:
                writer.WriteStartObject();
                foreach (var pair in dictObject) {
                    writer.WritePropertyName(pair.Key);
                    WriteValue(writer, pair.Value);
                }

                writer.WriteEndObject();
                break;
            case IArray arrayObject:
                writer.WriteStartArray();
                foreach (var item in arrayObject) {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            case IEnumerable<KeyValuePair<string, object?>> pairs:
                writer.WriteStartObject();
                foreach (var pair in pairs) {
                    writer.WritePropertyName(pair.Key);
                    WriteValue(writer, pair.Value);
                }

                writer.WriteEndObject();
                break;
            case IDictionary legacyDict:
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in legacyDict) {
                    writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!);
                    WriteValue(writer, entry.Value);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable enumerable:
                writer.WriteStartArray();
                foreach (var item in enumerable) {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                throw new ArgumentException($"Cannot serialize an object of type {value.GetType().FullName} to JSON");
        }
    }

    // Reads a single JSON value into plain .NET objects (Dictionary<string, object> /
    // List<object> / primitives).  The reader must be positioned on the first token
    // of the value.
    internal static object? ReadValue(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.StartObject => ReadObject(ref reader),
            JsonTokenType.StartArray => ReadArray(ref reader),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token parsing JSON. Token: {reader.TokenType}")
        };
    }

    // Converts an already parsed JsonElement into plain .NET objects using only
    // AOT safe JsonElement traversal.
    internal static object? ToNetObject(JsonElement element)
    {
        switch (element.ValueKind) {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();
            case JsonValueKind.Number:
                return element.TryGetInt64(out var l) ? l : element.GetDouble();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Array: {
                var list = new List<object?>(element.GetArrayLength());
                foreach (var item in element.EnumerateArray()) {
                    list.Add(ToNetObject(item));
                }

                return list;
            }
            case JsonValueKind.Object: {
                var dict = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject()) {
                    dict[property.Name] = ToNetObject(property.Value);
                }

                return dict;
            }
            default:
                throw new ArgumentException("Invalid JsonElement type: " + element.ValueKind);
        }
    }

    private static object ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var l)) {
            return l;
        }

        return reader.GetDouble();
    }

    private static Dictionary<string, object> ReadObject(ref Utf8JsonReader reader)
    {
        var dictionary = new Dictionary<string, object>();

        while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndObject) {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName) {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString()!;
            reader.Read();
            dictionary[propertyName] = ReadValue(ref reader)!;
        }

        return dictionary;
    }

    private static List<object> ReadArray(ref Utf8JsonReader reader)
    {
        var list = new List<object>();
        while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndArray) {
                break;
            }

            list.Add(ReadValue(ref reader)!);
        }

        return list;
    }
}
