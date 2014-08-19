// 
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
//using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
    /// <summary>Stores information about a revision -- its docID, revID, and whether it's deleted.
    ///     </summary>
    /// <remarks>
    /// Stores information about a revision -- its docID, revID, and whether it's deleted.
    /// It can also store the sequence number and document contents (they can be added after creation).
    /// </remarks>
    public sealed class SavedRevision : Revision
    {
        private RevisionInternal revisionInternal;

        private bool checkedProperties;

        /// <summary>Constructor</summary>
        /// <exclude></exclude>
        [InterfaceAudience.Private]
        internal SavedRevision(Document document, RevisionInternal revision) : base(document
            )
        {
            this.revisionInternal = revision;
        }

        /// <summary>Constructor</summary>
        /// <exclude></exclude>
        [InterfaceAudience.Private]
        internal SavedRevision(Database database, RevisionInternal revision) : this(database
            .GetDocument(revision.GetDocId()), revision)
        {
        }

        /// <summary>Get the document this is a revision of</summary>
        [InterfaceAudience.Public]
        public override Document GetDocument()
        {
            return document;
        }

        /// <summary>Has this object fetched its contents from the database yet?</summary>
        [InterfaceAudience.Public]
        public bool ArePropertiesAvailable()
        {
            return revisionInternal.GetProperties() != null;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [InterfaceAudience.Public]
        public override IList<Couchbase.Lite.SavedRevision> GetRevisionHistory()
        {
            IList<Couchbase.Lite.SavedRevision> revisions = new AList<Couchbase.Lite.SavedRevision
                >();
            IList<RevisionInternal> internalRevisions = GetDatabase().GetRevisionHistory(revisionInternal
                );
            foreach (RevisionInternal internalRevision in internalRevisions)
            {
                if (internalRevision.GetRevId().Equals(GetId()))
                {
                    revisions.AddItem(this);
                }
                else
                {
                    Couchbase.Lite.SavedRevision revision = document.GetRevisionFromRev(internalRevision
                        );
                    revisions.AddItem(revision);
                }
            }
            Sharpen.Collections.Reverse(revisions);
            return Sharpen.Collections.UnmodifiableList(revisions);
        }

        /// <summary>
        /// Creates a new mutable child revision whose properties and attachments are initially identical
        /// to this one's, which you can modify and then save.
        /// </summary>
        /// <remarks>
        /// Creates a new mutable child revision whose properties and attachments are initially identical
        /// to this one's, which you can modify and then save.
        /// </remarks>
        /// <returns></returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [InterfaceAudience.Public]
        public UnsavedRevision CreateRevision()
        {
            UnsavedRevision newRevision = new UnsavedRevision(document, this);
            return newRevision;
        }

        /// <summary>Creates and saves a new revision with the given properties.</summary>
        /// <remarks>
        /// Creates and saves a new revision with the given properties.
        /// This will fail with a 412 error if the receiver is not the current revision of the document.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [InterfaceAudience.Public]
        public Couchbase.Lite.SavedRevision CreateRevision(IDictionary<string, object> properties
            )
        {
            bool allowConflict = false;
            return document.PutProperties(properties, revisionInternal.GetRevId(), allowConflict
                );
        }

        [InterfaceAudience.Public]
        public override string GetId()
        {
            return revisionInternal.GetRevId();
        }

        [InterfaceAudience.Public]
        public override bool IsDeletion()
        {
            return revisionInternal.IsDeleted();
        }

        /// <summary>The contents of this revision of the document.</summary>
        /// <remarks>
        /// The contents of this revision of the document.
        /// Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
        /// </remarks>
        /// <returns>contents of this revision of the document.</returns>
        [InterfaceAudience.Public]
        public override IDictionary<string, object> GetProperties()
        {
            IDictionary<string, object> properties = revisionInternal.GetProperties();
            if (properties == null && !checkedProperties)
            {
                if (LoadProperties() == true)
                {
                    properties = revisionInternal.GetProperties();
                }
                checkedProperties = true;
            }
            return Sharpen.Collections.UnmodifiableMap(properties);
        }

        /// <summary>Deletes the document by creating a new deletion-marker revision.</summary>
        /// <remarks>Deletes the document by creating a new deletion-marker revision.</remarks>
        /// <returns></returns>
        /// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [InterfaceAudience.Public]
        public Couchbase.Lite.SavedRevision DeleteDocument()
        {
            return CreateRevision(null);
        }

        [InterfaceAudience.Public]
        public override Couchbase.Lite.SavedRevision GetParent()
        {
            return GetDocument().GetRevisionFromRev(GetDatabase().GetParentRevision(revisionInternal
                ));
        }

        [InterfaceAudience.Public]
        public override string GetParentId()
        {
            RevisionInternal parRev = GetDocument().GetDatabase().GetParentRevision(revisionInternal
                );
            if (parRev == null)
            {
                return null;
            }
            return parRev.GetRevId();
        }

        [InterfaceAudience.Public]
        internal override long GetSequence()
        {
            long sequence = revisionInternal.GetSequence();
            if (sequence == 0 && LoadProperties())
            {
                sequence = revisionInternal.GetSequence();
            }
            return sequence;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        internal bool LoadProperties()
        {
            try
            {
                RevisionInternal loadRevision = GetDatabase().LoadRevisionBody(revisionInternal, 
                    EnumSet.NoneOf<Database.TDContentOptions>());
                if (loadRevision == null)
                {
                    Log.W(Database.Tag, "%s: Couldn't load body/sequence", this);
                    return false;
                }
                revisionInternal = loadRevision;
                return true;
            }
            catch (CouchbaseLiteException e)
            {
                throw new RuntimeException(e);
            }
        }
    }
}
