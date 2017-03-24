using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.Lite.Serialization
{
    [JsonConverter(typeof(DBNullConverter))]
    internal sealed class DBNull
    {
    }

    internal sealed class DBNullConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteNull();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return new DBNull();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DBNull);
        }
    }
}
