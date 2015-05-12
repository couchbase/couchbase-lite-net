//
//  JSViewCompiler.cs
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
using Jint;
using System.Linq;
using Couchbase.Lite.Util;
using Jint.Native.Array;
using Jint.Native;
using System.Collections.Generic;
using System.Collections;
using Couchbase.Lite.Views;

namespace Couchbase.Lite.Listener
{
    public sealed class JSViewCompiler : IViewCompiler
    {
        public MapDelegate CompileMap(string source, string language)
        {
            if(!language.Equals("javascript")) {
                return null;
            }

            source = source.Replace("function", "function _f1");

            return (doc, emit) =>
            {
                var engine = new Engine().SetValue("log", new Action<object>((line) => Log.I("JSViewCompiler", line.ToString())));
                engine.SetValue("emit", emit);
                engine.Execute(source).Invoke("_f1", doc);
            };
        }

        public ReduceDelegate CompileReduce(string source, string language)
        {
            if(!language.Equals("javascript")) {
                return null;
            }

            if (source.StartsWith("_")) {
                return BuiltinReduceFunctions.Get(source.TrimStart('_'));
            }

            source = source.Replace("function", "function _f2");
            var engine = new Engine().Execute(source).SetValue("log", new Action<object>((line) => Log.I("JSViewCompiler", line.ToString())));

            return (keys, values, rereduce) => {
                var jsKeys = ToJSArray(keys, engine);
                var jsVals = ToJSArray(values, engine);

                var result = engine.Invoke("_f2", jsKeys, jsVals, rereduce);
                return result.ToObject();
            };
        }

        private static ArrayInstance ToJSArray(IEnumerable list, Engine engine)
        {
            List<JsValue> wrappedVals = new List<JsValue>();
            foreach (object x in list) {
                wrappedVals.Add(JsValue.FromObject(engine, x));
            }

            return (ArrayInstance)engine.Array.Construct(wrappedVals.ToArray());
        }
    }
}

