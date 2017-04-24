// 
// BlobConverter.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Couchbase.Lite.Internal.DB;
using Couchbase.Lite.Internal.Doc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Lite.Internal.Serialization
{
    internal sealed class BlobWriteConverter : JsonConverter
    {
        #region Variables

        private readonly Database _db;

        #endregion

        #region Properties

        public override bool CanRead
        {
            get {
                return false;
            }
        }

        public override bool CanWrite
        {
            get {
                return true;
            }
        }

        #endregion

        #region Constructors

        public BlobWriteConverter(Database db)
        {
            _db = db;
        }

        #endregion

        #region Overrides

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Blob);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var blob = value as Blob;
            if(blob != null) {
                blob.Install(_db);
                serializer.Serialize(writer, blob.JsonRepresentation);
            }
        }

        #endregion
    }

    internal sealed class CouchbaseTypeReadConverter : JsonConverter
    {
        #region Constants

        private static readonly Type _DictType = typeof(IDictionary<,>).MakeGenericType(typeof(string), typeof(object));

        #endregion

        #region Variables

        private readonly Database _db;

        #endregion

        #region Properties

        public override bool CanRead
        {
            get {
                return true;
            }
        }

        public override bool CanWrite
        {
            get {
                return false;
            }
        }

        #endregion

        #region Constructors

        public CouchbaseTypeReadConverter(Database db)
        {
            _db = db;
        }

        #endregion

        #region Overrides

        public override bool CanConvert(Type objectType)
        {
            return objectType.GetTypeInfo().ImplementedInterfaces.Contains(_DictType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var props = JToken.ReadFrom(reader).ToObject<IDictionary<string, object>>();
            if(!props.ContainsKey("_cbltype")) {
                return props;
            }

            var type = props["_cbltype"] as string;
            if(type == "blob") {
                return new Blob(_db, props);
            }

            throw new InvalidOperationException($"Unrecognized _cbltype in document ({type})");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
