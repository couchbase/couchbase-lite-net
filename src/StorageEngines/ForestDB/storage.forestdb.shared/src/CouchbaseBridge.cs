//
// CouchbaseBridge.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using CBForest;
using Newtonsoft.Json;

namespace Couchbase.Lite.Storage.ForestDB.Internal
{
    internal static class CouchbaseBridge
    {
        internal static C4EnumeratorOptions AsC4EnumeratorOptions(this QueryOptions options)
        {
            var retVal = default(C4EnumeratorOptions);
            if (options.Descending) {
                retVal.flags |= C4EnumeratorFlags.Descending;
            }

            if (options.IncludeDocs) {
                retVal.flags |= C4EnumeratorFlags.IncludeBodies;
            }

            if (options.IncludeDeletedDocs || options.AllDocsMode == AllDocsMode.IncludeDeleted) {
                retVal.flags |= C4EnumeratorFlags.IncludeDeleted;
            }

            if (options.InclusiveEnd) {
                retVal.flags |= C4EnumeratorFlags.InclusiveEnd;
            }

            if (options.InclusiveStart) {
                retVal.flags |= C4EnumeratorFlags.InclusiveStart;
            }

            if (options.AllDocsMode != AllDocsMode.OnlyConflicts) {
                retVal.flags |= C4EnumeratorFlags.IncludeNonConflicted;
            }

            retVal.skip = (uint)options.Skip;

            return retVal;
        }

        public static unsafe C4Key* SerializeToKey(object value)
        {
            var retVal = Native.c4key_new();
            using (var jsonWriter = new JsonC4KeyWriter(retVal)) {
                var serializer = new JsonSerializer();
                serializer.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                serializer.Serialize(jsonWriter, value);
            }

            return retVal;
        }

        public static T DeserializeKey<T>(C4KeyReader keyReader)
        {
            using (var jsonReader = new JsonC4KeyReader(keyReader)) {
                var serializer = new JsonSerializer();
                serializer.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                return serializer.Deserialize<T>(jsonReader);
            }
        }
    }
}

