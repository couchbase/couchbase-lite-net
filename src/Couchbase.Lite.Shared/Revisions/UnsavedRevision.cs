//
// UnsavedRevision.cs
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
using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    /// <summary>
    /// An unsaved Couchbase Lite Document Revision.
    /// </summary>
    public partial class UnsavedRevision : Revision {

    #region Non-public Members
        IDictionary<String, Object> properties;

        String ParentRevisionID { get; set; }

        /// <summary>Creates or updates an attachment.</summary>
        /// <remarks>
        /// Creates or updates an attachment.
        /// The attachment data will be written to the database when the revision is saved.
        /// </remarks>
        /// <param name="attachment">A newly-created Attachment (not yet associated with any revision)</param>
        /// <param name="name">The attachment name.</param>
        internal void AddAttachment(Attachment attachment, string name)
        {
            var attachments = (IDictionary<String, Object>)Properties.Get("_attachments");
            if (attachments == null)
            {
                attachments = new Dictionary<String, Object>();
            }
            attachments[name] = attachment;
            Properties["_attachments"] = attachments;
            if (attachment != null)
            {
                attachment.Name = name;
                attachment.Revision = this;
            }
        }


    #endregion

    #region Constructors
        internal UnsavedRevision(Document document, SavedRevision parentRevision): base(document)
        {
            if (parentRevision == null)
                ParentRevisionID = null;
            else
                ParentRevisionID = parentRevision.Id;

            IDictionary<String, Object> parentRevisionProperties;
            if (parentRevision == null)
            {
                parentRevisionProperties = null;
            }

            else
            {
                parentRevisionProperties = parentRevision.Properties;
            }
            if (parentRevisionProperties == null)
            {
                properties = new Dictionary<String, Object>();
                properties["_id"] = document.Id;
                if (ParentRevisionID != null)
                {
                    properties["_rev"] = ParentRevisionID;
                }
            }
            else
            {
                properties = new Dictionary<string, object>(parentRevisionProperties);
            }
        }


    #endregion

    #region Instance Members
        /// <summary>
        /// Gets or sets if the <see cref="Couchbase.Lite.Revision"/> marks the deletion of its <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>
        /// <c>true</c> if tthe <see cref="Couchbase.Lite.Revision"/> marks the deletion of its <see cref="Couchbase.Lite.Document"/>; 
        /// otherwise, <c>false</c>.
        /// </value>
        public new bool IsDeletion {
            get {
                return base.IsDeletion;
            }
            set {
                if (value)
                {
                    properties["_deleted"] = true;
                }
                else
                {
                    properties.Remove("_deleted");
                }
            }
        }

        /// <summary>
        /// Gets the parent <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <value>The parent.</value>
        public override SavedRevision Parent {
            get {
                if (String.IsNullOrEmpty (ParentId)) {
                    return null;
                }
                return Document.GetRevision(ParentId);
            }
        }

        /// <summary>
        /// Gets the parent <see cref="Couchbase.Lite.Revision"/>'s Id.
        /// </summary>
        /// <value>The parent.</value>
        public override string ParentId {
            get {
                return ParentRevisionID;
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
                // (Don't include self in the array, because this revision doesn't really exist yet)
                return Parent != null ? Parent.RevisionHistory : new AList<SavedRevision>();
            }
        }

        /// <summary>Gets the Revision's id.</summary>
        public override String Id {
            get {
                return null; // Once a revision is saved, it gets an id, but also becomes a new SavedRevision instance.
            }
        }
        /// <summary>
        /// Gets the properties of the <see cref="Couchbase.Lite.Revision"/>.
        /// <value>the properties of the <see cref="Couchbase.Lite.Revision"/>.</value>
        public override IDictionary<String, Object> Properties {
            get {
                return properties;
            }
        }

        public void SetProperties(IDictionary<String, Object> newProperties)
        {
            properties = newProperties;
        }

        /// <summary>
        /// Gets or sets the userProperties of the <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <remarks>
        /// Gets or sets the userProperties of the <see cref="Couchbase.Lite.Revision"/>. 
        /// Get, returns the properties of the <see cref="Couchbase.Lite.Revision"/> 
        /// without any properties with keys prefixed with '_' (which contain Couchbase Lite data). 
        /// Set, replaces all properties except for those with keys prefixed with '_'.
        /// </remarks>
        /// <value>The userProperties of the <see cref="Couchbase.Lite.Revision"/>.</value>
        public void SetUserProperties(IDictionary<String, Object> userProperties) 
        {
            var newProps = new Dictionary<String, Object>();
            newProps.PutAll(userProperties);

            foreach (string key in Properties.Keys)
                {
                    if (key.StartsWith("_", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newProps.Put(key, properties.Get(key));
                    }
                }
                // Preserve metadata properties
                properties = newProps;
        }

        /// <summary>
        /// Saves the <see cref="Couchbase.Lite.UnsavedRevision"/>. 
        /// This will fail if its parent is not the current <see cref="Couchbase.Lite.Revision"/> 
        /// of the associated <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while saving the <see cref="Couchbase.Lite.UnsavedRevision"/>.
        /// </exception>
        public SavedRevision Save() { 
            return Document.PutProperties(Properties, ParentId, allowConflict: false); 
        }

        /// <summary>
        /// Saves the <see cref="Couchbase.Lite.UnsavedRevision"/>, optionally allowing 
        /// the save when there is a conflict.
        /// </summary>
        /// <param name="allowConflict">
        /// Whether or not to allow saving when there is a conflict.
        /// </param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while saving the <see cref="Couchbase.Lite.UnsavedRevision"/>.
        /// </exception>
        public SavedRevision Save(Boolean allowConflict) { return Document.PutProperties(Properties, ParentId, allowConflict); }

        /// <summary>
        /// Sets the attachment with the given name.
        /// </summary>
        /// <remarks>
        /// Sets the <see cref="Couchbase.Lite.Attachment"/> with the given name. 
        /// The <see cref="Couchbase.Lite.Attachment"/> data will be written to 
        /// the <see cref="Couchbase.Lite.Database"/> when the 
        /// <see cref="Couchbase.Lite.Revision"/> is saved.
        /// </remarks>
        /// <param name="name">The name of the <see cref="Couchbase.Lite.Attachment"/> to set.</param>
        /// <param name="contentType">The content-type of the <see cref="Couchbase.Lite.Attachment"/>.</param>
        /// <param name="content">The <see cref="Couchbase.Lite.Attachment"/> content.</param>
        public void SetAttachment(String name, String contentType, IEnumerable<Byte> content) {
            var attachment = new Attachment(new MemoryStream(content.ToArray()), contentType);
            AddAttachment(attachment, name);
        }

        /// <summary>
        /// Sets the attachment with the given name.
        /// </summary>
        /// <remarks>
        /// Sets the <see cref="Couchbase.Lite.Attachment"/> with the given name. 
        /// The <see cref="Couchbase.Lite.Attachment"/> data will be written to 
        /// the <see cref="Couchbase.Lite.Database"/> when the 
        /// <see cref="Couchbase.Lite.Revision"/> is saved.
        /// </remarks>
        /// <param name="name">The name of the <see cref="Couchbase.Lite.Attachment"/> to set.</param>
        /// <param name="contentType">The content-type of the <see cref="Couchbase.Lite.Attachment"/>.</param>
        /// <param name="content">The <see cref="Couchbase.Lite.Attachment"/> content.</param>
        public void SetAttachment(String name, String contentType, Stream content) {
            var attachment = new Attachment(content, contentType);
            AddAttachment(attachment, name);
        }

        /// <summary>
        /// Sets the attachment with the given name.
        /// </summary>
        /// <remarks>
        /// Sets the <see cref="Couchbase.Lite.Attachment"/> with the given name. 
        /// The <see cref="Couchbase.Lite.Attachment"/> data will be written to 
        /// the <see cref="Couchbase.Lite.Database"/> when the 
        /// <see cref="Couchbase.Lite.Revision"/> is saved.
        /// </remarks>
        /// <param name="name">The name of the <see cref="Couchbase.Lite.Attachment"/> to set.</param>
        /// <param name="contentType">The content-type of the <see cref="Couchbase.Lite.Attachment"/>.</param>
        /// <param name="contentUrl">The URL of the <see cref="Couchbase.Lite.Attachment"/> content.</param>
        public void SetAttachment(String name, String contentType, Uri contentUrl) {
            try
            {
                var inputStream = contentUrl.OpenConnection().GetInputStream();
                var length = inputStream.Length;
                var inputBytes = inputStream.ReadAllBytes();
                inputStream.Close();
                SetAttachment(name, contentType, inputBytes);
            }
            catch (IOException e)
            {
                Log.E(Database.Tag, "Error opening stream for url: " + contentUrl);
                throw new RuntimeException(e);
            }
        }

        /// <summary>
        /// Removes the <see cref="Couchbase.Lite.Attachment"/> 
        /// with the given name.
        /// </summary>
        /// <remarks>
        /// Removes the <see cref="Couchbase.Lite.Attachment"/> with the given name. 
        /// The <see cref="Couchbase.Lite.Attachment"/> will be deleted from the 
        /// Database when the Revision is saved.
        /// </remarks>
        /// <param name="name">
        /// The name of the <see cref="Couchbase.Lite.Attachment"/> to delete.
        /// </param>
        public void RemoveAttachment(String name) { AddAttachment(null, name); }

    #endregion
    
    }

}
