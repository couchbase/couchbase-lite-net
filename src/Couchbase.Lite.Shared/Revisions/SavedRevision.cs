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
using System.Collections.Generic;
using Couchbase.Lite.Internal;
using Sharpen;
using Couchbase.Lite.Util;
using Couchbase.Lite.Store;

namespace Couchbase.Lite {
    /// <summary>
    /// A saved Couchbase Lite <see cref="Couchbase.Lite.Document"/> <see cref="Couchbase.Lite.Revision"/>.
    /// </summary>
    public sealed class SavedRevision : Revision {

        #region Variables

        private string _parentRevID;

        #endregion

    #region Constructors

        /// <summary>Constructor</summary>
        internal SavedRevision(Document document, RevisionInternal revision)
            : base(document) { RevisionInternal = revision; }

        /// <summary>Constructor</summary>
        internal SavedRevision(Database database, RevisionInternal revision)
            : this(database.GetDocument(revision == null ? null : revision.DocID), revision) { }

        internal SavedRevision(Database database, RevisionInternal revision, string parentRevId)
            : this(database, revision)
        {
            _parentRevID = parentRevId;
        }

    #endregion
    
    #region Non-public Members

        internal RevisionInternal RevisionInternal { get; private set; }

        private  Boolean CheckedProperties { get; set; }

        internal override long Sequence {
            get {
                var sequence = RevisionInternal.Sequence;
                if (sequence == 0 && LoadProperties())
                {
                    sequence = RevisionInternal.Sequence;
                }
                return sequence;
            }
        }

        //Throws CouchbaseLiteException other than NotFound
        internal bool LoadProperties()
        {
            try {
                var loadRevision = Database.LoadRevisionBody(RevisionInternal);
                if (loadRevision == null)
                {
                    Log.W(Database.TAG, "Couldn't load body/sequence of {0}" + this);
                    return false;
                }
                RevisionInternal = loadRevision;
                return true;
            } catch (CouchbaseLiteException e) {
                if (e.Code == StatusCode.NotFound) {
                    return false;
                }

                throw;
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
                return Document.GetRevisionFromRev(Database.Storage.GetParentRevision(RevisionInternal));
            }
        }

        /// <summary>
        /// Gets the parent <see cref="Couchbase.Lite.Revision"/>'s Id.
        /// </summary>
        /// <value>The parent.</value>
        public override String ParentId {
            get {
                if (_parentRevID != null) {
                    return _parentRevID;
                }

                var parRev = Document.Database.Storage.GetParentRevision(RevisionInternal);
                if (parRev == null) {
                    return null;
                }

                return parRev.RevID;
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
                var revisions = new List<SavedRevision>();
                var internalRevisions = Database.Storage.GetRevisionHistory(RevisionInternal, null);

                foreach (var internalRevision in internalRevisions)
                {
                    if (internalRevision.RevID.Equals(Id))
                    {
                        revisions.Add(this);
                    }
                    else
                    {
                        var revision = Document.GetRevisionFromRev(internalRevision);
                        revisions.Add(revision);
                    }
                }
                Collections.Reverse(revisions);
                return Collections.UnmodifiableList(revisions);
            }
        }

        /// <summary>Gets the Revision's id.</summary>
        public override String Id {
            get {
                return RevisionInternal.RevID;
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
                return RevisionInternal.Deleted;
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
                IDictionary<string, object> properties = RevisionInternal == null ? null : RevisionInternal.GetProperties();
                if (properties == null && !CheckedProperties)
                {
                    if (LoadProperties()) {
                        properties = RevisionInternal == null ? null : RevisionInternal.GetProperties();
                    }
                    CheckedProperties = true;
                }
                return Collections.UnmodifiableMap(properties);
            }
        }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Revision"/>'s properties are available. 
        /// Older, ancestor, <see cref="Couchbase.Lite.Revision"/>s are not guaranteed to have their properties available.
        /// </summary>
        /// <value><c>true</c> if properties available; otherwise, <c>false</c>.</value>
        public Boolean PropertiesAvailable { get { return RevisionInternal.GetProperties() != null; } }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.UnsavedRevision"/> whose properties and attachments are initially identical to this one.
        /// </summary>
        /// <remarks>
        /// Creates a new mutable child revision whose properties and attachments are initially identical
        /// to this one's, which you can modify and then save.
        /// </remarks>
        /// <returns>
        /// A new child <see cref="Couchbase.Lite.UnsavedRevision"/> whose properties and attachments 
        /// are initially identical to this one.
        /// </returns>
        public UnsavedRevision CreateRevision() {
            var newRevision = new UnsavedRevision(Document, this);
            return newRevision;
        }

        /// <summary>
        /// Creates and saves a new <see cref="Couchbase.Lite.Revision"/> with the specified properties. 
        /// To succeed the specified properties must include a '_rev' property whose value maches the current Revision's id.
        /// </summary>
        /// <returns>
        /// The new <see cref="Couchbase.Lite.SavedRevision"/>.
        /// </returns>
        /// <param name="properties">
        /// The properties to set on the new Revision.
        /// </param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an error occurs while creating or saving the new <see cref="Couchbase.Lite.Revision"/>.
        /// </exception>
        public SavedRevision CreateRevision(IDictionary<String, Object> properties) {
            return Document.PutProperties(properties, RevisionInternal.RevID, false);           
        }

        /// <summary>
        /// Creates and saves a new deletion <see cref="Couchbase.Lite.Revision"/> 
        /// for the associated <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <returns>
        /// A new deletion Revision for the associated <see cref="Couchbase.Lite.Document"/>
        /// </returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Throws if an issue occurs while creating a new deletion <see cref="Couchbase.Lite.Revision"/>.
        /// </exception>
        public SavedRevision DeleteDocument() { return CreateRevision(null); }

    #endregion
    
    }

    

}
