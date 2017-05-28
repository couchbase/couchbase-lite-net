//
// Revision.cs
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
using System.Threading.Tasks;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;

namespace Couchbase.Lite 
{

    internal delegate bool RevisionFilter(RevisionInternal rev);

    /// <summary>
    /// A Couchbase Lite Document Revision.
    /// </summary>
    public abstract class Revision 
    {
    
    #region Constructors

        /// <summary>
        /// Convenience constructor
        /// </summary>
        protected internal Revision() : this(null) { }

        /// <summary>
        /// Default Constructor
        /// </summary>
        /// <param name="document">The document that owns this revision</param>
        protected internal Revision(Document document)
        {
            Document = document;
        }

    #endregion

    #region Non-public Members

        internal virtual Int64 Sequence { get; private set; }

        internal IDictionary<String, Object> GetAttachmentMetadata()
        {
            return GetProperty("_attachments").AsDictionary<string,object>();
        }


    #endregion

    #region Instance Members
        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Document"/> that this <see cref="Couchbase.Lite.Revision"/> belongs to.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Document"/> that this <see cref="Couchbase.Lite.Revision"/> belongs to</value>
        public virtual Document Document { get; protected set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.Revision"/>'s 
        /// <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.Revision"/>'s 
        /// <see cref="Couchbase.Lite.Document"/>.</value>
        public Database Database { get { return Document.Database; } }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Revision"/>'s id.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Revision"/>'s id.</value>
        public abstract String Id { get; }

        /// <summary>
        /// Gets if the <see cref="Couchbase.Lite.Revision"/> marks the deletion of its <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value><c>true</c> if the <see cref="Couchbase.Lite.Revision"/> marks the deletion; otherwise, <c>false</c>.</value>
        public virtual Boolean IsDeletion {
            get {
                var deleted = GetProperty("_deleted");
                if (deleted == null)
                {
                    return false;
                }
                var deletedBool = (Boolean)deleted;
                return deletedBool;
            }
        }

        /// <summary>
        /// Does this revision mark the deletion or removal (from available channels) of its document?
        /// (In other words, does it have a "_deleted_ or "_removed" property?)
        /// </summary>
        public bool IsGone
        {
            get 
            {
                var wasRemovedFromChannel = false;
                var removed = GetProperty("_removed");
                if (removed != null)
                {
                    var removedBoolean = (bool)removed;
                    wasRemovedFromChannel = removedBoolean;
                }
                return IsDeletion || wasRemovedFromChannel;
            }
        }

        /// <summary>Gets the properties of the <see cref="Couchbase.Lite.Revision"/>.</summary>
        /// <remarks>
        /// The contents of this revision of the document.
        /// Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
        /// </remarks>
        /// <value>The properties of the <see cref="Couchbase.Lite.Revision"/>.</value>
        public abstract IDictionary<String, Object> Properties { get; }

        /// <summary>
        /// Gets the properties of the <see cref="Couchbase.Lite.Revision"/>. 
        /// without any properties with keys prefixed with '_' (which contain Couchbase Lite data).
        /// </summary>
        /// <value>The properties of the <see cref="Couchbase.Lite.Revision"/>.</value>
        public virtual IDictionary<String, Object> UserProperties { 
            get {
                var result = new Dictionary<String, Object>();
                foreach (string key in Properties.Keys) {
                    if (!key.StartsWith("_", StringComparison.InvariantCultureIgnoreCase)) {
                        result[key] = Properties.Get(key);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the parent <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <value>The parent <see cref="Couchbase.Lite.Revision"/>.</value>
        public abstract SavedRevision Parent { get; }

        /// <summary>
        /// Gets the parent <see cref="Couchbase.Lite.Revision"/>'s id.
        /// </summary>
        /// <value>The parent <see cref="Couchbase.Lite.Revision"/>'s id.</value>
        public abstract String ParentId { get; }

        /// <summary>Returns the history of this document as an array of CBLRevisions, in forward order.</summary>
        /// <remarks>
        /// Returns the history of this document as an array of CBLRevisions, in forward order.
        /// Older revisions are NOT guaranteed to have their properties available.
        /// </remarks>
        /// <value>The history of this document as an array of CBLRevisions, in forward order</value>
        public abstract IEnumerable<SavedRevision> RevisionHistory { get; }

        /// <summary>
        /// Gets the names of all the <see cref="Couchbase.Lite.Attachment"/>s.
        /// </summary>
        /// <value>
        /// the names of all the <see cref="Couchbase.Lite.Attachment"/>s.
        /// </value>
        public IEnumerable<String> AttachmentNames {
            get {
                var attachmentMetadata = GetAttachmentMetadata();
                var result = new List<String>();

                if (attachmentMetadata != null) {
                    result.AddRange(attachmentMetadata.Keys);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets all the <see cref="Couchbase.Lite.Attachment"/>s.
        /// </summary>
        /// <value>All the <see cref="Couchbase.Lite.Attachment"/>s.</value>
        public IEnumerable<Attachment> Attachments {
            get {
                var result = new List<Attachment>();
                foreach (var attachmentName in AttachmentNames) {
                    result.Add(GetAttachment(attachmentName));
                }

                return result;
            } 
        }

        /// <summary>
        /// Returns the value of the property with the specified key.
        /// </summary>
        /// <returns>The value of the property with the specified key.</returns>
        /// <param name="key">The key of the property value to return.</param>
        public Object GetProperty(String key) {
            return Properties.Get(key);
        }

        /// <summary>
        /// Returns the <see cref="Couchbase.Lite.Attachment"/> with the specified name if it exists, otherwise null.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.Attachment"/> with the specified name if it exists, otherwise null.</returns>
        /// <param name="name">The name of the <see cref="Couchbase.Lite.Attachment"/> to return.</param>
        public Attachment GetAttachment(String name) {
            var attachmentsMetadata = GetAttachmentMetadata();
            if(attachmentsMetadata == null) {
                return null;
            }


            var attachmentMetadata = attachmentsMetadata.Get(name);
            var attachmentMetadataObj = attachmentMetadata as Attachment;
            if(attachmentMetadataObj != null) {
                return attachmentMetadataObj;
            }

            var attachmentMetadataDict = attachmentMetadata.AsDictionary<string, object>();
            return attachmentMetadataDict == null ? null : new Attachment(this, name, attachmentMetadataDict);
        }

        /// <summary>
        /// Returns the <see cref="Task"/> with the specified name if it exists, otherwise null.
        /// If a local copy of the attachment is not available, it will then attempt to download the attachment from the server
        /// </summary>
        /// <returns>The <see cref="Task"/> with the specified name if it exists, otherwise null.</returns>
        /// <param name="name">The name of the <see cref="Couchbase.Lite.Attachment"/> to return.</param>
        /// <param name="replicator">The puller to use when downloading attachments</param>
        public Task<Attachment> GetDeferedAttachment (String name, Replication replicator)
        {
            var task = new Task<Attachment>(() => {
                var att = GetAttachment (name);
                if (att == null) {
                    return null;
                }

                if (att.ContentStream != null) {
                    return att;
                }

                if (replicator.IsAttachmentPull ==  false) {
                    return att;
                }

                var puller = (AttachmentPuller)replicator;

                var req = puller.QueueRemoteAttachment(att);

                RevisionInternal revInt = new RevisionInternal(att.Revision.Document.Id, att.Document.CurrentRevision.RevisionInternal.RevID, false);
                puller.AddToInbox(revInt); // to trigger the batcher thread

                req.WaitForComple();

                att.Body = req.GetStream();

                return att;
            });

            task.Start();
            return task;
        }

    #endregion
    
    #region Operator/Object Overloads

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="Couchbase.Lite.Revision"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current
        /// <see cref="Couchbase.Lite.Revision"/>; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            var result = false;
            if (obj is SavedRevision)
            {
                var other = (SavedRevision)obj;
                if (Document.Id.Equals(other.Document.Id) && Id.Equals(other.Id))
                {
                    result = true;
                }
            }
            return result;
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="Couchbase.Lite.Revision"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a
        /// hash table.</returns>
        public override int GetHashCode()
        {
            return Document.Id.GetHashCode() ^ Id.GetHashCode();
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <returns>A <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.Revision"/>.</returns>
        public override string ToString()
        {
            return "{" + Document.Id + " #" + Id + (IsDeletion ? "DEL" : String.Empty) + "}";
        }

    #endregion
    }
}

