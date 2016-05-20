//
// ForestRevisionInternal.cs
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
using Couchbase.Lite.Internal;
using System.Linq;

namespace Couchbase.Lite.Storage.ForestDB.Internal
{
    internal class ForestRevisionInternal : RevisionInternal
    {
        internal unsafe ForestRevisionInternal(CBForest.C4Document *doc, bool loadBody) 
            : base((string)doc->docID, doc->selectedRev.revID.AsRevID(), doc->selectedRev.IsDeleted)
        {
            if (!doc->Exists) {
                return;
            }

            Sequence = (long)doc->selectedRev.sequence;
            if (loadBody) {
                // Important not to lazy load here since we can only assume
                // doc lives until immediately after this function ends
                SetBody(new Body(doc->selectedRev.body.ToArray())); 
            }
        }

        internal unsafe ForestRevisionInternal(CBForest.CBForestDocStatus docStatus, bool loadBody)
            : this(docStatus.GetDocument(), loadBody)
        {

        }
    }
}

