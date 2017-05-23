//
//  Serialization.cs
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
using System.Diagnostics.CodeAnalysis;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;
using LiteCore.Interop;
using LiteCore.Util;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Serialization
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class JsonPropertyAttribute : Attribute
    {
        
    }

    internal unsafe interface IJsonSerializer
    {
        #region Public Methods

        T Deserialize<T>(FLValue* value);

        void Populate<T>(T item, FLValue* value);
        FLSliceResult Serialize(object obj);

        #endregion
    }

    internal abstract unsafe class Serializer : IJsonSerializer, IJsonWriter
    {
        #region Properties

        public JsonSerializerSettings SerializerSettings { get; set; } = new JsonSerializerSettings {
            ContractResolver = new CouchbaseLiteContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            FloatFormatHandling = FloatFormatHandling.DefaultValue,
            NullValueHandling = NullValueHandling.Include
        };

        #endregion

        #region Internal Methods

        internal static IJsonSerializer CreateDefaultFor(Database db)
        {
            return new DefaultSerializer(db);
        }

        #endregion

        #region IJsonSerializer

        public abstract T Deserialize<T>(FLValue* value);

        public abstract void Populate<T>(T item, FLValue* value);

        public abstract FLSliceResult Serialize(object obj);

        #endregion

        #region IJsonWriter

        public abstract void Write(string name, object value);
        public abstract void Write(string key, IJsonMapped value);

        #endregion
    }

    internal sealed unsafe class DefaultSerializer : Serializer
    {
        #region Constants

        private const string Tag = nameof(DefaultSerializer);

        #endregion

        #region Variables

        private readonly Database _db;
        private JsonFLValueWriter _innerWriter;

        #endregion

        #region Constructors

        public DefaultSerializer(Database db)
        {
            _db = db;
            SerializerSettings.Converters = new JsonConverter[] { new BlobWriteConverter(_db), new CouchbaseTypeReadConverter(_db) };
        }

        #endregion

        #region Public Methods

        public FLSliceResult Serialize(IJsonMapped obj)
        {
            using(var writer = new JsonFLValueWriter(_db.c4db)) {
                _innerWriter = writer;
                PerfTimer.StartEvent("Serialize_Write");
                writer.WriteStartObject();
                obj.WriteTo(this);
                writer.WriteEndObject();
                PerfTimer.StopEvent("Serialize_Write");
                PerfTimer.StartEvent("Serialize_Flush");
                writer.Flush();
                PerfTimer.StopEvent("Serialize_Flush");

                _innerWriter = null;
                return writer.Result;
            }
        }

        #endregion

        #region Overrides

        [SuppressMessage("ReSharper", "ConvertIfStatementToNullCoalescingExpression", Justification = "T is not constrained to nullable type")]
        public override T Deserialize<T>(FLValue* value)
        {
            try {
                using(var reader = new JsonFLValueReader(value, _db.SharedStrings)) {
                    var settings = SerializerSettings;
                    var serializer = JsonSerializer.CreateDefault(settings);
                    var retVal = serializer.Deserialize<T>(reader);
                    if(retVal == null) {
                        retVal = Activator.CreateInstance<T>();
                    }

                    return retVal;
                }
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, StatusCode.BadJson, Tag, $"Unable to deserialize into type {typeof(T).FullName}!");
            }
        }

        public override void Populate<T>(T item, FLValue* value)
        {
            try {
                using(var reader = new JsonFLValueReader(value, _db.SharedStrings)) {
                    var settings = SerializerSettings;
                    var serializer = JsonSerializer.CreateDefault(settings);
                    serializer.Populate(reader, item);
                }
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, StatusCode.BadJson, Tag, $"Unable to deserialize into type {typeof(T).FullName}!");
            }
        }

        public override FLSliceResult Serialize(object obj)
        {
            var fast = obj as IJsonMapped;
            if(fast != null) {
                return Serialize(fast);
            }
            //using(var writer = new JsonFLValueWriter(_db.c4db)) {
            //    PerfTimer.StartEvent("Serialize_Write");
            //    writer.Write(obj);
            //    PerfTimer.StopEvent("Serialize_Write");
            //    PerfTimer.StartEvent("Serialize_Flush");
            //    writer.Flush();
            //    PerfTimer.StopEvent("Serialize_Flush");

            //    return writer.Result;
            //}
            try {
                using(var writer = new JsonFLValueWriter(_db.c4db)) {
                    var settings = SerializerSettings;
                    var serializer = JsonSerializer.CreateDefault(settings);
                    PerfTimer.StartEvent("Serialize_Write");
                    serializer.Serialize(writer, obj);
                    PerfTimer.StopEvent("Serialize_Write");
                    PerfTimer.StartEvent("Serialize_Flush");
                    writer.Flush();
                    PerfTimer.StopEvent("Serialize_Flush");
                    return writer.Result;
                }
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, StatusCode.BadJson, Tag, "Unable to serialize object!");
            }
        }

        public override void Write(string key, object value)
        {
            _innerWriter.WritePropertyName(key);
            _innerWriter.WriteValue(value);
        }

        public override void Write(string key, IJsonMapped value)
        {
            _innerWriter.WritePropertyName(key);
            _innerWriter.WriteStartObject();
            value.WriteTo(this);
            _innerWriter.WriteEndObject();
        }

        #endregion
    }
}
