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
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Serialization
{
    internal unsafe interface IJsonSerializer
    {
        #region Public Methods

        T Deserialize<T>(FLValue* value);

        void Populate<T>(T item, FLValue* value);
        FLSliceResult Serialize(object obj);

        #endregion
    }

    internal abstract unsafe class Serializer : IJsonSerializer
    {
        #region Properties

        public JsonSerializerSettings SerializerSettings { get; set; } = new JsonSerializerSettings {
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

    }

    internal sealed unsafe class DefaultSerializer : Serializer
    {
        #region Constants

        private const string Tag = nameof(DefaultSerializer);

        #endregion

        #region Variables

        private readonly Database _db;

        #endregion

        #region Constructors

        public DefaultSerializer(Database db)
        {
            _db = db;
            SerializerSettings.Converters = new JsonConverter[] { new BlobWriteConverter(_db), new CouchbaseTypeReadConverter(_db) };
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
                Log.To.Database.E(Tag, $"Exception during deserialization: {e}");
                throw new LiteCoreException(new C4Error(FLError.JSONError));
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
                Log.To.Database.E(Tag, $"Exception during Populate: {e}");
                throw new LiteCoreException(new C4Error(FLError.JSONError));
            }
        }

        public override FLSliceResult Serialize(object obj)
        {
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
                Log.To.Database.E(Tag, $"Exception during serialization: {e}");
                throw new LiteCoreException(new C4Error(FLError.EncodeError));
            }
        }

        #endregion
    }
}
