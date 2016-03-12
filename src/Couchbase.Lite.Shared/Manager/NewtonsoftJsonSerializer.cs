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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    //Eventually split this into another assembly
    internal class NewtonsoftJsonSerializer : IJsonSerializer
    {
        
        #region Constants

        private static readonly string Tag = typeof(NewtonsoftJsonSerializer).Name;

        private static JsonSerializerSettings settings = new JsonSerializerSettings { 
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        private const string TAG = "NewtonsoftJsonSerializer";

        #endregion

        #region Variables

        private JsonTextReader _textReader;
        private JsonSerializationSettings _settings = new JsonSerializationSettings();

        #endregion

        #region Properties

        public JsonSerializationSettings Settings
        {
            get { return _settings; }
            set { 
                if (_settings == value) {
                    return;
                }

                Log.To.Database.I(Tag, "Changing global JSON serialization settings from {0} to {1}", _settings, value);
                _settings = value;
                if (_settings.DateTimeHandling == DateTimeHandling.UseDateTimeOffset) {
                    settings.DateParseHandling = DateParseHandling.DateTimeOffset;
                    settings.DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind;
                } else if(_settings.DateTimeHandling == DateTimeHandling.Ignore) {
                    settings.DateParseHandling = DateParseHandling.None;
                } else {
                    settings.DateParseHandling = DateParseHandling.DateTime;
                    settings.DateTimeZoneHandling = DateTimeZoneHandling.Local;
                }
            }
        }

        public JsonToken CurrentToken
        {
            get {
                return _textReader == null ? JsonToken.None : (JsonToken)_textReader.TokenType;
            }
        }

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
                throw Misc.CreateExceptionAndLog(Log.To.NoDomain, e, TAG, "Error deserializing json ({0})",
                    new SecureLogString(json, LogMessageSensitivity.PotentiallyInsecure));
            }

            return item;
        }

        public T Deserialize<T>(Stream json) 
        {
            using (var sr = new StreamReader(json))
            using (var jsonReader = new JsonTextReader(sr)) 
            {
                var serializer = JsonSerializer.Create(settings);
                T item;
                try {
                    item = serializer.Deserialize<T>(jsonReader);
                } catch (JsonException e) {
                    throw Misc.CreateExceptionAndLog(Log.To.NoDomain, e, TAG, "Error deserializing json from stream");
                }

                return item;
            }
        }

        public void StartIncrementalParse(Stream json)
        {
            if (_textReader != null) {
                ((IDisposable)_textReader).Dispose();
            }

            _textReader = new JsonTextReader(new StreamReader(json));
        }

        public bool Read()
        {
            try {
                return _textReader != null && _textReader.Read();
            } catch (Exception e) {
                if (e is JsonReaderException) {
                    throw Misc.CreateExceptionAndLog(Log.To.NoDomain, StatusCode.BadJson, TAG, 
                        "Error reading from streaming parser");
                }

                throw Misc.CreateExceptionAndLog(Log.To.NoDomain, e, TAG, "Error reading from streaming parser");
            }
        }

        public T DeserializeNextObject<T>()
        {
            if (_textReader == null) {
                Log.To.Sync.W(TAG, "DeserializeNextObject is only valid after a call to StartIncrementalParse, " +
                    "returning null");
                return default(T);
            }

            try {
                return JToken.ReadFrom(_textReader).ToObject<T>();
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.NoDomain, e, TAG, "Error deserializing from streaming parser");
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
            return jObj == null ? null : jObj.Select(x => x.ToObject<T>()).ToList();
        }

        public IJsonSerializer DeepClone()
        {
            return new NewtonsoftJsonSerializer();
        }
 
        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_textReader != null) {
                ((IDisposable)_textReader).Dispose();
            }
        }

        #pragma warning restore 1591
        #endregion
        
    }
}

