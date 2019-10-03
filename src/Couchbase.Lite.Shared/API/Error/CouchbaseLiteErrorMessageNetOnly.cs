//  CouchbaseLiteErrorMessage.cs
//
//  Copyright (c) 2019 Couchbase, Inc All rights reserved.
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
using System.Text;

namespace Couchbase.Lite
{
    internal static partial class CouchbaseLiteErrorMessage
    {
        //Database - Copy
        internal const string ResolveDefaultDirectoryFailed = "Failed to resolve a default directory! If you have overriden the default directory, please check it.  Otherwise please file a bug report.";
        //Replicator Start()
        internal const string ReplicatorDisposed = "Replication cannot be started after disposal";
        //MArray MDict <--- in the future, we will replace them with LiteCore Mutable Fleece API
        internal const string CannotRemoveItemsFromNonMutableMArray = "Cannot remove items from a non-mutable array";
        internal const string CannotRemoveStartingFromIndexLessThan = "Cannot remove starting from an index less than 0 (got {0})";
        internal const string CannotRemoveRangeEndsBeforeItStarts = "Cannot remove a range that ends before it starts (got start= {0}, count = {1} )";
        internal const string RangeEndForRemoveExceedsArrayLength = "Range end for remove exceeds the length of the array(got start = {0}, count = {1} )";
        internal const string CannotSetItemsInNonMutableMArray = "Cannot set items in a non-mutable array";
        internal const string CannotClearNonMutableMArray = "Cannot clear a non-mutable array";
        internal const string CannotInsertItemsInNonMutableMArray = "Cannot insert items in a non-mutable array";
        internal const string CannotClearNonMutableMDict = "Cannot clear a non-mutable MDict";
        internal const string CannotSetItemsInNonMutableInMDict = "Cannot set items in a non-mutable MDict";
    }
}
