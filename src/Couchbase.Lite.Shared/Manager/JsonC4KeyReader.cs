//
// JsonC4KeyReader.cs
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
#if FORESTDB
using System;
using Newtonsoft.Json;
using CBForest;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    internal sealed unsafe class JsonC4KeyReader : JsonReader
    {
        private C4KeyReader _reader;
        private Stack<Newtonsoft.Json.JsonToken> _sequenceStack = new Stack<Newtonsoft.Json.JsonToken>();

        public JsonC4KeyReader(C4KeyReader keyReader)
        {
            _reader = keyReader;
        }

        public override bool Read()
        {
            fixed(C4KeyReader *r = &_reader) {
                switch (Native.c4key_peek(r)) {
                    case C4KeyToken.Array:
                        SetToken(Newtonsoft.Json.JsonToken.StartArray);
                        _sequenceStack.Push(Newtonsoft.Json.JsonToken.StartArray);
                        break;
                    case C4KeyToken.Bool:
                        SetToken(Newtonsoft.Json.JsonToken.Boolean, Native.c4key_readBool(r));
                        break;
                    case C4KeyToken.EndSequence:
                        {
                            var lastSequence = _sequenceStack.Pop();
                            if (lastSequence == Newtonsoft.Json.JsonToken.StartArray) {
                                SetToken(Newtonsoft.Json.JsonToken.EndArray);
                            } else {
                                SetToken(Newtonsoft.Json.JsonToken.EndObject);
                            }

                            break;
                        }
                    case C4KeyToken.Map:
                        SetToken(Newtonsoft.Json.JsonToken.StartObject);
                        _sequenceStack.Push(Newtonsoft.Json.JsonToken.StartObject);
                        break;
                    case C4KeyToken.Null:
                        SetToken(Newtonsoft.Json.JsonToken.Null, null);
                        break;
                    case C4KeyToken.Number:
                        SetToken(Newtonsoft.Json.JsonToken.Float, Native.c4key_readNumber(r));
                        break;
                    case C4KeyToken.String:
                        {
                            var lastSequence = _sequenceStack.Peek();
                            if (lastSequence == Newtonsoft.Json.JsonToken.StartObject) {
                                SetToken(Newtonsoft.Json.JsonToken.PropertyName, Native.c4key_readString(r));
                                _sequenceStack.Push(Newtonsoft.Json.JsonToken.PropertyName);
                            } else {
                                SetToken(Newtonsoft.Json.JsonToken.String, Native.c4key_readString(r));
                                if (lastSequence == Newtonsoft.Json.JsonToken.PropertyName) {
                                    _sequenceStack.Pop();
                                }
                            }

                            break;
                        }
                    case C4KeyToken.Error:
                        return false;
                }

                return true;
            }
        }

        public override decimal? ReadAsDecimal()
        {
            fixed(C4KeyReader* r = &_reader) {
                if (Native.c4key_peek(r) != C4KeyToken.Number || !Read()) {
                    return null;
                }
            }

            return (decimal)Value;
        }

        public override int? ReadAsInt32()
        {
            fixed(C4KeyReader* r = &_reader) {
                if (Native.c4key_peek(r) != C4KeyToken.Number || !Read()) {
                    return null;
                }
            }

            return (int)Value;
        }

        public override string ReadAsString()
        {
            fixed(C4KeyReader* r = &_reader) {
                if (Native.c4key_peek(r) != C4KeyToken.String || !Read()) {
                    return null;
                }
            }

            return (string)Value;
        }

        public override byte[] ReadAsBytes()
        {
            return null;
        }

        public override DateTime? ReadAsDateTime()
        {
            return null;
        }

        public override DateTimeOffset? ReadAsDateTimeOffset()
        {
            return null;
        }
    }
}
#endif
