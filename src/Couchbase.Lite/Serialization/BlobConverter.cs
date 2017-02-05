//
//  BlobConverter.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Lite.Serialization
{
    internal sealed class BlobConverter : JsonConverter
    {
        private readonly Database _db;

        public BlobConverter(Database db)
        {
            _db = db;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Blob);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var props = JObject.ReadFrom(reader).ToObject<IDictionary<string, object>>();
            return new Blob(_db, props);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var blob = (Blob)value;
            blob.Install(_db);
            serializer.Serialize(writer, blob.JsonRepresentation);
        }
    }
}
