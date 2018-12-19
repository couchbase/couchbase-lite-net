//
// JsonFLValueReader.cs
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
#if CBL_LINQ
using System;
using Newtonsoft.Json;
using System.Collections.Generic;

using Couchbase.Lite.Logging;
using LiteCore;
using LiteCore.Interop;
using System.Globalization;

namespace Couchbase.Lite.Internal.Serialization
{
    internal sealed unsafe class JsonFLValueReader : JsonReader
    {
        #region Constants

        private const string Tag = nameof(JsonFLValueReader);

        #endregion

        #region Variables

        private readonly Stack<object> _sequenceStack = new Stack<object>();
        private readonly SharedStringCache _stringCache;
        private FLValue* _currentValue;
        private bool _inValue;

        #endregion

        #region Constructors

        public JsonFLValueReader(FLValue *root, SharedStringCache stringCache)
        {
            _currentValue = root;
            _stringCache = stringCache;
        }

        #endregion

        #region Private Methods

        private void BeginArray(FLArray* a)
        {
            FLArrayIterator i;
            Native.FLArrayIterator_Begin(a, &i);
            _sequenceStack.Push(i);
        }

        private void BeginObject(FLDict* d)
        {
            FLDictIterator i;
            Native.FLDictIterator_Begin(d, &i);
            _sequenceStack.Push(i);
        }

        private string GetKey()
        {
            string key;
            if(Native.FLValue_GetType(_currentValue) == FLValueType.Number) {
                key = _stringCache.GetKey((int)Native.FLValue_AsInt(_currentValue));
                if(key == null) {
                    WriteLog.To.Database.W(Tag, "Corrupt key found during deserialization, skipping...");
                }
            } else {
                key = Native.FLValue_AsString(_currentValue);
            }

            return key;
        }

        private NextActionCode NextAction()
        {
            if(_sequenceStack.Count == 0) {
                return NextActionCode.ReadValue;
            }

            var i = _sequenceStack.Pop();
            if(i is FLDictIterator) {
                var iter = (FLDictIterator)i;
                if(_inValue) {
                    _inValue = false;
                    _currentValue = Native.FLDictIterator_GetValue(&iter);
                    if(_currentValue == null) {
                        return NextActionCode.EndObject;
                    }

                    Native.FLDictIterator_Next(&iter);
                    _sequenceStack.Push(iter);
                    return NextActionCode.ReadValue;
                }
                
                _currentValue = Native.FLDictIterator_GetKey(&iter);
                if(_currentValue == null) {
                    return NextActionCode.EndObject;
                }

                _inValue = true;
                _sequenceStack.Push(i);
                return NextActionCode.ReadObjectKey;
            } else {
                var iter = (FLArrayIterator)i;
                _currentValue = Native.FLArrayIterator_GetValue(&iter);
                if(_currentValue == null) {
                    return NextActionCode.EndArray;
                }

                Native.FLArrayIterator_Next(&iter);
                _sequenceStack.Push(iter);
                return NextActionCode.ReadValue;
            }
        }

        #endregion

        #region Overrides

        public override bool Read()
        {
            if(_sequenceStack.Count == 0 && _currentValue == null) {
                return false;
            }

            switch(NextAction()) {
                case NextActionCode.EndArray:
                    SetToken(JsonToken.EndArray);
                    break;
                case NextActionCode.EndObject:
                    SetToken(JsonToken.EndObject);
                    break;
                case NextActionCode.ReadObjectKey:
                    var key = GetKey();
                    if(key == null) {
                        return false;
                    }

                    SetToken(JsonToken.PropertyName, key);
                    break;
                case NextActionCode.ReadValue:
                    switch(Native.FLValue_GetType(_currentValue)) {
                        case FLValueType.Array:
                            BeginArray(Native.FLValue_AsArray(_currentValue));
                            SetToken(JsonToken.StartArray);
                            break;
                        case FLValueType.Boolean:
                            SetToken(JsonToken.Boolean, Native.FLValue_AsBool(_currentValue));
                            break;
                        case FLValueType.Dict:
                            BeginObject(Native.FLValue_AsDict(_currentValue));
                            SetToken(JsonToken.StartObject);
                            break;
                        case FLValueType.Null:
                            SetToken(JsonToken.Null, null);
                            break;
                        case FLValueType.Number:
                            if(Native.FLValue_IsInteger(_currentValue)) {
                                if(Native.FLValue_IsUnsigned(_currentValue)) {
                                    SetToken(JsonToken.Integer, Native.FLValue_AsUnsigned(_currentValue));
                                }

                                SetToken(JsonToken.Integer, Native.FLValue_AsInt(_currentValue));
                            } else if(Native.FLValue_IsDouble(_currentValue)) {
                                SetToken(JsonToken.Float, Native.FLValue_AsDouble(_currentValue));
                            } else {
                                SetToken(JsonToken.Float, Native.FLValue_AsFloat(_currentValue));
                            }

                            break;
                        case FLValueType.String:
                            SetToken(JsonToken.String, Native.FLValue_AsString(_currentValue));
                            break;
                        default:
                            return false;
                    }
                    break;
                default:
                    return false;

            }

            return true;
        }

        public override DateTime? ReadAsDateTime()
        {
            var action = NextAction();
            if(action != NextActionCode.ReadValue) {
                throw new JsonReaderException($"Invalid state for reading date time offset ({action})");
            }

            if(Native.FLValue_GetType(_currentValue) != FLValueType.String) {
                return null;
            }


            DateTime retVal;
            if(DateTime.TryParseExact(Native.FLValue_AsString(_currentValue), "o", null, DateTimeStyles.RoundtripKind, out retVal)) {
                SetToken(JsonToken.Date, retVal);
                return retVal;
            }

            throw new LiteCoreException(new C4Error(FLError.EncodeError));
        }

        public override DateTimeOffset? ReadAsDateTimeOffset()
        {
            var action = NextAction();
            if(action != NextActionCode.ReadValue) {
                throw new JsonReaderException($"Invalid state for reading date time offset ({action})");
            }

            if(Native.FLValue_GetType(_currentValue) != FLValueType.String) {
                return null;
            }

            
            DateTimeOffset retVal;
            if(DateTimeOffset.TryParseExact(Native.FLValue_AsString(_currentValue), "o", null, DateTimeStyles.RoundtripKind, out retVal)) {
                SetToken(JsonToken.Date, retVal);
                return retVal;
            }

            throw new LiteCoreException(new C4Error(FLError.EncodeError));
        }

        #endregion

        #region Nested

        private enum NextActionCode
        {
            ReadValue,
            ReadObjectKey,
            EndObject,
            EndArray
        }

        #endregion
    }
}
#endif