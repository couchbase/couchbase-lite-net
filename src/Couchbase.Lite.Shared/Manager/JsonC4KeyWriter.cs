//
// JsonC4KeyWriter.cs
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
using CBForest;

namespace Couchbase.Lite
{
    internal unsafe class JsonC4KeyWriter : JsonWriter
    {
        private C4Key* _c4key;

        public JsonC4KeyWriter(C4Key *key) 
        {
            _c4key = key;
        }

        #region Overrides

        public override void Flush()
        {
            //no-op
        }

        public override void WriteEndArray()
        {
            base.WriteEndArray();
            Native.c4key_endArray(_c4key);
        }

        public override void WriteEndObject()
        {
            base.WriteEndObject();
            Native.c4key_endMap(_c4key);
        }

        public override void WriteNull()
        {
            base.WriteNull();
            Native.c4key_addNull(_c4key);
        }

        public override void WritePropertyName(string name)
        {
            base.WritePropertyName(name);
            Native.c4key_addMapKey(_c4key, name);
        }

        public override void WriteStartArray()
        {
            base.WriteStartArray();
            Native.c4key_beginArray(_c4key);
        }

        public override void WriteStartObject()
        {
            base.WriteStartObject();
            Native.c4key_beginMap(_c4key);
        }

        public override void WriteValue(bool value)
        {
            base.WriteValue(value);
            Native.c4key_addBool(_c4key, value);
        }

        public override void WriteValue(byte value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }

        public override void WriteValue(double value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, value);
        }

        public override void WriteValue(decimal value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }

        public override void WriteValue(float value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }

        public override void WriteValue(int value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }

        public override void WriteValue(long value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }

        public override void WriteValue(sbyte value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }

        public override void WriteValue(short value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }

        public override void WriteValue(uint value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }
            
        public override void WriteValue(ulong value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }

        public override void WriteValue(ushort value)
        {
            base.WriteValue(value);
            Native.c4key_addNumber(_c4key, (double)value);
        }

        public override void WriteValue(string value)
        {
            base.WriteValue(value);
            Native.c4key_addString(_c4key, value);
        }

        #endregion
    }
}

