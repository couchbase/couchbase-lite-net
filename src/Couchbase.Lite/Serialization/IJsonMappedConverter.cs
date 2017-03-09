using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Couchbase.Lite.Serialization
{
    internal sealed class IJsonMappedConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IJsonMapped));
        }

        public override bool CanWrite
        {
            get {
                return true;
            }
        }

        public override bool CanRead
        {
            get {
                return false;
            }
        }

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

            public void Write(string key, object value)
            {
                _inner.WritePropertyName(key);
                _inner.WriteValue(value);
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
