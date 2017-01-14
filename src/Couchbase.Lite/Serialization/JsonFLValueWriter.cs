//
// JsonFLValueWriter.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using Newtonsoft.Json;
using LiteCore.Interop;
using LiteCore.Util;
using LiteCore;

namespace Couchbase.Lite
{
    internal unsafe class JsonFLValueWriter : JsonWriter
    {
        private FLEncoder* _encoder;

        public FLSliceResult Result { get; private set; }

        public JsonFLValueWriter(C4Database* db) 
        {
            _encoder = Native.c4db_createFleeceEncoder(db);
        }

        #region Overrides

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Native.FLEncoder_Free(_encoder);
        }

        public override void Flush()
        {
            FLError err;
            Result = NativeRaw.FLEncoder_Finish(_encoder, &err);
            if(Result.buf == null) {
                throw new LiteCoreException(new C4Error(err));
            }
        }

        public override void WriteEndArray()
        {
            base.WriteEndArray();
            Native.FLEncoder_EndArray(_encoder);
        }

        public override void WriteEndObject()
        {
            base.WriteEndObject();
            Native.FLEncoder_EndDict(_encoder);
        }

        public override void WriteNull()
        {
            base.WriteNull();
            Native.FLEncoder_WriteNull(_encoder);
        }

        public override void WritePropertyName(string name)
        {
            base.WritePropertyName(name);
            Native.FLEncoder_WriteKey(_encoder, name);
        }

        public override void WriteStartArray()
        {
            base.WriteStartArray();
            Native.FLEncoder_BeginArray(_encoder, 0);
        }

        public override void WriteStartObject()
        {
            base.WriteStartObject();
            Native.FLEncoder_BeginDict(_encoder, 0);
        }

        public override void WriteValue(bool value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteBool(_encoder, value);
        }

        public override void WriteValue(byte value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteUInt(_encoder, value);
        }

        public override void WriteValue(double value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteDouble(_encoder, value);
        }

        public override void WriteValue(decimal value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteDouble(_encoder, (double)value);
        }

        public override void WriteValue(float value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteFloat(_encoder, value);
        }

        public override void WriteValue(int value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteInt(_encoder, value);
        }

        public override void WriteValue(long value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteInt(_encoder, value);
        }

        public override void WriteValue(sbyte value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteInt(_encoder, value);
        }

        public override void WriteValue(short value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteInt(_encoder, value);
        }

        public override void WriteValue(uint value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteUInt(_encoder, value);
        }
            
        public override void WriteValue(ulong value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteUInt(_encoder, value);
        }

        public override void WriteValue(ushort value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteUInt(_encoder, value);
        }

        public override void WriteValue(string value)
        {
            base.WriteValue(value);
            Native.FLEncoder_WriteString(_encoder, value);
        }

        public override void WriteValue(DateTime value)
        {
            WriteValue(value.ToString("o"));
        }

        public override void WriteValue(DateTime? value)
        {
            if(!value.HasValue) {
                WriteNull();
            } else {
                WriteValue(value.Value);
            }
        }

        public override void WriteValue(DateTimeOffset value)
        {
            WriteValue(value.ToString("o"));
        }

        public override void WriteValue(DateTimeOffset? value)
        {
            if (!value.HasValue) {
                WriteNull();
            } else {
                WriteValue(value.Value);
            }
        }

        #endregion
    }
}
