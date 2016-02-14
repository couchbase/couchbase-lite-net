//
// DocumentChange.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Couchbase.Lite.Util;
using Couchbase.Lite.Internal;
using Sharpen;
using Couchbase.Lite.Store;

namespace Couchbase.Lite {

    /// <summary>
    /// Provides details about a Document change.
    /// </summary>
    public class DocumentChange
    {
        internal RevisionInternal AddedRevision { get; private set; }

        internal DocumentChange(RevisionInternal addedRevision, string winningRevisionId, bool isConflict, Uri sourceUrl)
        {
            AddedRevision = addedRevision;
            WinningRevisionId = winningRevisionId;
            IsConflict = isConflict;
            SourceUrl = sourceUrl;
        }
    
    #region Instance Members
        //Properties
        /// <summary>
        /// Gets the Id of the <see cref="Couchbase.Lite.Document"/> that changed.
        /// </summary>
        /// <value>The Id of the <see cref="Couchbase.Lite.Document"/> that changed.</value>
        public String DocumentId { get { return AddedRevision.DocID; } }

        /// <summary>
        /// Gets the Id of the new Revision.
        /// </summary>
        /// <value>The Id of the new Revision.</value>
        public String RevisionId { get { return AddedRevision.RevID; } }

        /// <summary>
        /// Gets a value indicating whether this instance is current revision.
        /// </summary>
        /// <value><c>true</c> if this instance is current revision; otherwise, <c>false</c>.</value>
        public Boolean IsCurrentRevision { get { return WinningRevisionId != null && WinningRevisionId.Equals(AddedRevision.RevID); } }

        internal string WinningRevisionId { get; private set; }

        /// <summary>
        /// Gets the winning revision.
        /// </summary>
        /// <value>The winning revision.</value>
        internal RevisionInternal WinningRevisionIfKnown
        { 
            get
            {
                return IsCurrentRevision ? AddedRevision : null;
            }
        }

        internal void ReduceMemoryUsage()
        {
            AddedRevision = AddedRevision.CopyWithoutBody();
        }

        /// <summary>
        /// Gets a value indicating whether this instance is conflict.
        /// </summary>
        /// <value><c>true</c> if this instance is conflict; otherwise, <c>false</c>.</value>
        public Boolean IsConflict { get; private set; }

        /// <summary>
        /// Gets the remote URL of the source Database from which this change was replicated.
        /// </summary>
        /// <value>The remote URL of the source Database from which this change was replicated.</value>
        public Uri SourceUrl { get; private set; }

    #endregion

    }

}
