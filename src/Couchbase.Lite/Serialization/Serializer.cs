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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Lite.Serialization
{
    internal unsafe interface IJsonSerializer
    {
        FLSliceResult Serialize(object obj);

        T Deserialize<T>(FLValue* value);
    }

    internal abstract unsafe class Serializer : IJsonSerializer
    {
        internal static IJsonSerializer CreateDefaultFor(Database db)
        {
            return new DefaultSerializer(db);
        }

        public JsonSerializerSettings SerializerSettings { get; set; } = new JsonSerializerSettings {
            ContractResolver = new CouchbaseLiteContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            FloatFormatHandling = FloatFormatHandling.DefaultValue,
            NullValueHandling = NullValueHandling.Ignore,
        };

        protected Serializer()
        {

        }

        public abstract FLSliceResult Serialize(object obj);

        public abstract T Deserialize<T>(FLValue* value);
    }

    internal sealed unsafe class DefaultSerializer : Serializer
    {
        private const string Tag = nameof(DefaultSerializer);
        private readonly Database _db;

        public DefaultSerializer(Database db) : base()
        {
            _db = db;
            SerializerSettings.Converters = new JsonConverter[] { new BlobWriteConverter(_db), new CouchbaseTypeReadConverter(_db) };
        }

        public override FLSliceResult Serialize(object obj)
        {
            try {
                using(var writer = new JsonFLValueWriter(_db.c4db)) {
                    var settings = SerializerSettings;
#if DEBUG
                    var traceWriter = new MemoryTraceWriter();
                    settings.TraceWriter = traceWriter;
#endif
                    var serializer = JsonSerializer.CreateDefault(settings);
                    serializer.Serialize(writer, obj);

#if DEBUG
                    Debug.WriteLine(traceWriter);
#endif

                    writer.Flush();
                    return writer.Result;
                }
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, StatusCode.BadJson, Tag, "Unable to serialize object!");
            }
        }

        public override T Deserialize<T>(FLValue* value)
        {
            try {
                using(var reader = new JsonFLValueReader(value, _db.SharedStrings)) {
                    var settings = SerializerSettings;
#if DEBUG
                    var traceWriter = new MemoryTraceWriter();
                    settings.TraceWriter = traceWriter;
#endif

                    var serializer = JsonSerializer.CreateDefault(settings);
                    var retVal = serializer.Deserialize<T>(reader);

#if DEBUG
                    Debug.WriteLine(traceWriter);
#endif

                    if(retVal == null) {
                        retVal = Activator.CreateInstance<T>();
                    }

                    return retVal;
                }
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, StatusCode.BadJson, Tag, $"Unable to deserialize into type {typeof(T).FullName}!");
            }
        }
    }
}
