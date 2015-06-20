//
//  NewtonsoftJsonSerializer.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Lite
{
    //Eventually split this into another assembly
    internal class NewtonsoftJsonSerializer : IJsonSerializer
    {
        #region Constants

        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings { 
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        #endregion

        #region IJsonSerializer
        #pragma warning disable 1591

        public string SerializeObject(object obj, bool pretty)
        {
            return JsonConvert.SerializeObject(obj, pretty ? Formatting.Indented : Formatting.None, settings);
        }
        public T DeserializeObject<T>(string json)
        {
            T item;
            try {
                item = JsonConvert.DeserializeObject<T>(json, settings);
            } catch(JsonException e) {
                throw new CouchbaseLiteException(e, StatusCode.BadJson);
            }

            return item;
        }

        public T Deserialize<T>(Stream json) 
        {
            using (var jsonReader = new JsonTextReader(new StreamReader(json))) 
            {
                var serializer = JsonSerializer.Create(settings);
                T item;
                try {
                    item = serializer.Deserialize<T>(jsonReader);
                } catch (JsonException e) {
                    throw new CouchbaseLiteException(e, StatusCode.BadJson);
                }

                return item;
            }
        }

        public IDictionary<K, V> ConvertToDictionary<K, V>(object obj)
        {
            if (obj == null) {
                return null;
            }

            var jObj = obj as JObject;
            return jObj == null ? null : jObj.ToObject<IDictionary<K, V>>();
        }

        public IList<T> ConvertToList<T>(object obj)
        {
            if (obj == null) {
                return null;
            }

            var jObj = obj as JArray;
            return jObj == null ? null : jObj.ToObject<IList<T>>();
        }

        #pragma warning restore 1591
        #endregion
        
    }
}

