//
// PluginExtensions.cs
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
using CBForest;
using Couchbase.Lite.Revisions;
using System;
using System.Diagnostics;
using System.Linq;

namespace Couchbase.Lite.Storage.ForestDB
{
    internal static class PluginExtensions
    {
        internal static void SortByDocID(this RevisionList list)
        {
            list.Sort((r1, r2) => r1.DocID.CompareTo(r2.DocID));
        }

        internal static RevisionID AsRevID(this C4Slice slice)
        {
            return RevisionIDFactory.FromData(slice.ToArray());
        }

        internal static unsafe void PinAndUse(this RevisionID revID, Action<C4Slice> action)
        {
            var data = revID.AsData();
            fixed(byte *dataPtr = data)
            {
                var slice = new C4Slice(dataPtr, (uint)data.Length);
                action(slice);
            }
        }
    }
}

