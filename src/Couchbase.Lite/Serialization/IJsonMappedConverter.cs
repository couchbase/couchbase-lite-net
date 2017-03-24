using System;
using System.Collections;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;

namespace Couchbase.Lite.Serialization
{
    internal sealed class IJsonMappedConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IJsonMapped));
        }

        public override bool CanWrite => true;

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var outerWriter = new CBJsonWriter(writer);
            writer.WriteStartObject();
            ((IJsonMapped)value).WriteTo(outerWriter);
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        private class CBJsonWriter : IJsonWriter
        {
            private readonly JsonWriter _inner;

            public CBJsonWriter(JsonWriter inner)
            {
                _inner = inner;
            }

            private void WriteValue(object value)
            {
                var arr = value as IList;
                var fast = value as IJsonMapped;
                if (arr != null) {
                    _inner.WriteStartArray();
                    foreach (var val in arr) {
                        WriteValue(val);
                    }

                    _inner.WriteEndArray();
                } else if (fast != null) {
                    fast.WriteTo(this);
                } else {
                    _inner.WriteValue(value);
                }
            }

            public void Write(string key, object value)
            {
                var fast = value as IJsonMapped;
                if (fast != null) {
                    Write(key, fast);
                } else {
                    _inner.WritePropertyName(key);
                    WriteValue(value);
                }
            }

            public void Write(string key, IJsonMapped value)
            {
                _inner.WritePropertyName(key);
                _inner.WriteStartObject();
                value.WriteTo(this);
                _inner.WriteEndObject();
            }
        }
    }
}
