//
//  AOTHelper.cs
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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Sharpen;
using System;
using Newtonsoft.Json.Serialization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic.Couchbase;

namespace Couchbase.Lite.Unity
{
    /// <summary>
    /// This class is *not* used directly.  It exists purely to give hints to the compiler
    /// about generic classes that are overlooked by the AOT compiler otherwise.
    /// </summary>
    public static class AOTHelper
    {
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static void Dummy()
        {
            //AOT compilation needs direct references to used types in order to compiled
            //correctly.  The types listed below are in nested generic types and need to
            //be spelled out.  However, the method doesn't actually need to be called so
            //it is private.  Furthermore, if this is not in the same assembly it doesn't 
            //help the compiler.  There is no memory storage taking place here so it won't
            //affect the footprint
            var dummy = new List<KeyValuePair<string, Document>>();
            var dummy2 = new SplitOrderedList<string, KeyValuePair<string, string>>(new GenericEqualityComparer<string>());
            var dummy3 = new SplitOrderedList<string, KeyValuePair<string, ConcurrentDictionary<string, object>>>(new GenericEqualityComparer<string>());
            var dummy4 = new SplitOrderedList<string, KeyValuePair<string, ConcurrentDictionary<string, ConcurrentDictionary<string, object>>>>(new GenericEqualityComparer<string>());
            var dummy5 = new SplitOrderedList<string, KeyValuePair<string, object>>(new GenericEqualityComparer<string>());
            var dummy6 = new Dictionary<DefaultSerializationBinder.TypeNameKey, Type>(new DefaultComparer<DefaultSerializationBinder.TypeNameKey>());
            var dummy7 = new SplitOrderedList<HttpRequestMessage, KeyValuePair<HttpRequestMessage, Task>>(new DefaultComparer<HttpRequestMessage>());
            var dummy8 = new Dictionary<ResolverContractKey, JsonContract>(new System.Collections.Generic.Couchbase.GenericEqualityComparer<ResolverContractKey>());
        }
    }
}