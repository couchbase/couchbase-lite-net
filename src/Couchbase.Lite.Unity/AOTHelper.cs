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
using System;
using Sharpen;
using System.Collections.Generic;
using Couchbase.Lite.Util;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace Couchbase.Lite.Unity
{
    public static class AOTHelper
    {
        private const string Tag = "AOTHelper";

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static void Dummy()
        {
            //AOT compilation needs direct references to used types in order to compiled
            //correctly.  The types listed below are in nested generic types and need to
            //be spelled out.  However, the method doesn't actually need to be called so
            //it is private
            for (bool workaround = false; workaround; )
            {
                var dummy = new LinkedHashMap<string, Document>();
                var dummy2 = new List<KeyValuePair<string, Document>>();
                var dummy3 = new SplitOrderedList<string, KeyValuePair<string, string>>(new GenericEqualityComparer<string>());
            }
        }
    }
}

