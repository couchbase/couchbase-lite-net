//
// SavedRevision.cs
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
using Couchbase.Lite.Internal;
using Sharpen;
using Couchbase.Lite.Util;
using System.Dynamic;

namespace Couchbase.Lite {

    public partial class SavedRevision : Revision {

    #region Constructors

        /// <summary>Constructor</summary>
        internal SavedRevision(Document document, RevisionInternal revision)
            : base(document) { RevisionInternal = revision; }

        /// <summary>Constructor</summary>
        internal SavedRevision(Database database, RevisionInternal revision)
            : this(database.GetDocument(revision.GetDocId()), revision) { }

    #endregion
    
    #region Non-public Members

        internal RevisionInternal RevisionInternal { get; private set; }

        private  Boolean CheckedProperties { get; set; }

        internal override Int64 Sequence {
            get {
                var sequence = RevisionInternal.GetSequence();
                if (sequence == 0 && LoadProperties())
                {
                    sequence = RevisionInternal.GetSequence();
                }
                return sequence;
            }
        }

        internal Boolean LoadProperties()
        {
            try
            {
                var loadRevision = Database.LoadRevisionBody(RevisionInternal, EnumSet.NoneOf<TDContentOptions>());
                if (loadRevision == null)
                {
                    Log.W(Database.Tag, "Couldn't load body/sequence of {0}" + this);
                    return false;
                }
                RevisionInternal = loadRevision;
                return true;
            }
            catch (CouchbaseLiteException e)
            {
                throw new RuntimeException(e);
            }
        }

    #endregion

    #region Instance Members

        /// <summary>
        /// Gets the parent <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <value>The parent.</value>
        public override SavedRevision Parent {
            get {
                return Document.GetRevisionFromRev(Database.GetParentRevision(RevisionInternal));
            }
        }

        /// <summary>
        /// Gets the parent <see cref="Couchbase.Lite.Revision"/>'s Id.
        /// </summary>
        /// <value>The parent.</value>
        public override String ParentId {
            get {
                var parRev = Document.Database.GetParentRevision(RevisionInternal);
                if (parRev == null)
                {
                    return null;
                }
                return parRev.GetRevId();
            }
        }

        /// <summary>Returns the history of this document as an array of <see cref="Couchbase.Lite.Revision"/>s, in forward order.</summary>
        /// <remarks>
        /// Returns the history of this document as an array of <see cref="Couchbase.Lite.Revision"/>s, in forward order.
        /// Older, ancestor, revisions are not guaranteed to have their properties available.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public override IEnumerable<SavedRevision> RevisionHistory {
            get {
                var revisions = new AList<SavedRevision>();
                var internalRevisions = Database.GetRevisionHistory(RevisionInternal);

                foreach (var internalRevision in internalRevisions)
                {
                    if (internalRevision.GetRevId().Equals(Id))
                    {
                        revisions.AddItem(this);
                    }
                    else
                    {
                        var revision = Document.GetRevisionFromRev(internalRevision);
                        revisions.AddItem(revision);
                    }
                }
                Collections.Reverse(revisions);
                return Collections.UnmodifiableList(revisions);
            }
        }

        /// <summary>Gets the Revision's id.</summary>
        public override String Id {
            get {
                return RevisionInternal.GetRevId();
            }
        }

        /// <summary>
        /// Sets if the <see cref="Couchbase.Lite.Revision"/> marks the deletion of its <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <remarks>
        /// Does this revision mark the deletion of its document?
        /// (In other words, does it have a "_deleted" property?)
        /// </remarks>
        /// <value><c>true</c> if this instance is deletion; otherwise, <c>false</c>.</value>
        public override Boolean IsDeletion {
            get {
                return RevisionInternal.IsDeleted();
            }
        }

        /// <summary>The contents of this revision of the document.</summary>
        /// <remarks>
        /// The contents of this revision of the document.
        /// Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
        /// </remarks>
        /// <returns>contents of this revision of the document.</returns>
        public override IDictionary<String, Object> Properties {
            get {
                IDictionary<string, object> properties = RevisionInternal.GetProperties();
                if (properties == null && !CheckedProperties)
                {
                    if (LoadProperties() == true)
                    {
                        properties = RevisionInternal.GetProperties();
                    }
                    CheckedProperties = true;
                }
                return Collections.UnmodifiableMap(properties);
            }
        }

        /// <summary>
        /// Gets whether this <see cref="Couchbase.Lite.SavedRevision"/>'s properties are available.
        /// </summary>
        /// <remarks>
        /// Older, ancestor, <see cref="Couchbase.Lite.SavedRevision"/>s are not guaranteed to have their properties available.
        /// </remarks>
        /// <value><c>true</c> if properties available; otherwise, <c>false</c>.</value>
        public Boolean PropertiesAvailable { get { return RevisionInternal.GetProperties() != null; } }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.UnsavedRevision"/> whose properties and attachments are initially identical to this one.
        /// </summary>
        /// <remarks>
        /// Creates a new mutable child revision whose properties and attachments are initially identical
        /// to this one's, which you can modify and then save.
        /// </remarks>
        /// <returns>The revision.</returns>
        public UnsavedRevision CreateRevision() {
            var newRevision = new UnsavedRevision(Document, this);
            return newRevision;
        }

        /// <summary>Creates and saves a new revision with the given properties.</summary>
        /// <remarks>
        /// Creates and saves a new <see cref="Couchbase.Lite.Revision"/> with the specified properties. To succeed the specified properties must include a '_rev' property whose value maches the current %Revision's% id.
        /// This will fail with a 412 error if the receiver is not the current revision of the document.
        /// </remarks>
        /// <returns>
        /// The new <see cref="Couchbase.Lite.SavedRevision"/>.
        /// </returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public SavedRevision CreateRevision(IDictionary<String, Object> properties) {
            return Document.PutProperties(properties, RevisionInternal.GetRevId(), allowConflict: false);           
        }

        /// <summary>Deletes the document by creating a new deletion-marker revision.</summary>
        /// <remarks>
        /// Creates and saves a new deletion <see cref="Couchbase.Lite.Revision"/> for the associated <see cref="Couchbase.Lite.Document"/>.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public SavedRevision DeleteDocument() { return CreateRevision(null); }

    #endregion
    
    }

    

}
