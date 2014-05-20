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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;

namespace Couchbase.Lite 
{

    public abstract partial class Revision 
    {
    
    #region Constructors

        protected internal Revision() : this(null) { }

        /// <summary>Constructor</summary>
        protected internal Revision(Document document)
        {
            this.Document = document;
        }

    #endregion

    #region Non-public Members

        internal virtual Int64 Sequence { get; private set; }

        internal IDictionary<String, Object> GetAttachmentMetadata()
        {
            return (IDictionary<String, Object>)GetProperty("_attachments");
        }


    #endregion

    #region Instance Members
        /// <summary>Get the document this is a revision of.</summary>
        public virtual Document Document { get; protected set; }

        /// <summary>Get the revision's owning database.</summary>
        public Database Database { get { return Document.Database; } }

        /// <summary>Gets the Revision's id.</summary>
        public abstract String Id { get; }

        /// <summary>
        /// Does this revision mark the deletion of its document?
        /// (In other words, does it have a "_deleted" property?)
        /// </summary>
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

        /// <summary>The contents of this revision of the document.</summary>
        /// <remarks>
        /// The contents of this revision of the document.
        /// Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
        /// </remarks>
        /// <returns>contents of this revision of the document.</returns>
        public abstract IDictionary<String, Object> Properties { get; }

        /// <summary>The user-defined properties, without the ones reserved by CouchDB.</summary>
        /// <remarks>
        /// The user-defined properties, without the ones reserved by CouchDB.
        /// This is based on -properties, with every key whose name starts with "_" removed.
        /// </remarks>
        /// <returns>user-defined properties, without the ones reserved by CouchDB.</returns>
        public virtual IDictionary<String, Object> UserProperties { 
            get {
                var result = new Dictionary<String, Object>();
                foreach (string key in Properties.Keys)
                {
                    if (!key.StartsWith("_", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Put(key, Properties.Get(key));
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Gets the parent <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <value>The parent.</value>
        public abstract SavedRevision Parent { get; }

        /// <summary>
        /// Gets the parent <see cref="Couchbase.Lite.Revision"/>'s Id.
        /// </summary>
        /// <value>The parent.</value>
        public abstract String ParentId { get; }

        /// <summary>Returns the history of this document as an array of CBLRevisions, in forward order.</summary>
        /// <remarks>
        /// Returns the history of this document as an array of CBLRevisions, in forward order.
        /// Older revisions are NOT guaranteed to have their properties available.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public abstract IEnumerable<SavedRevision> RevisionHistory { get; }

        /// <summary>
        /// Gets the names of all the <see cref="Couchbase.Lite.Attachment"/>s.
        /// </summary>
        /// <value>The attachment names.</value>
        public IEnumerable<String> AttachmentNames {
            get {
                var attachmentMetadata = GetAttachmentMetadata();
                var result = new AList<String>();

                if (attachmentMetadata != null)
                {
                    Collections.AddAll(result, attachmentMetadata.Keys);
                }
                return result;
            }
        }

        /// <summary>
        /// Gets all the <see cref="Couchbase.Lite.Attachment"/>s.
        /// </summary>
        /// <value>The attachments.</value>
        public IEnumerable<Attachment> Attachments {
            get {
                var result = new AList<Attachment>();

                foreach (var attachmentName in AttachmentNames)
                {
                    result.AddItem(GetAttachment(attachmentName));
                }
                return result;
            } 
        }

        /// <summary>
        /// Gets the value of the property with the specified key.
        /// </summary>
        /// <returns>The value for the named property.</returns>
        /// <param name="key">The name of the desired property.</param>
        public Object GetProperty(String key) {
            return Properties.Get(key);
        }

        /// <summary>
        /// Returns the <see cref="Couchbase.Lite.Attachment"/> with the specified name if it exists, otherwise null.
        /// </summary>
        /// <returns>The attachment.</returns>
        /// <param name="name">Name.</param>
        public Attachment GetAttachment(String name) {
            var attachmentsMetadata = GetAttachmentMetadata();
            if (attachmentsMetadata == null)
            {
                return null;
            }
            var attachmentMetadata = (IDictionary<String, Object>)attachmentsMetadata.Get(name);
            return new Attachment(this, name, attachmentMetadata);
        }

    #endregion
    
    #region Operator/Object Overloads

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

        public override int GetHashCode()
        {
            return Document.Id.GetHashCode() ^ Id.GetHashCode();
        }

        public override string ToString()
        {
            return "{" + Document.Id + " #" + Id + (IsDeletion ? "DEL" : String.Empty) + "}";
        }

    #endregion
    }
}

