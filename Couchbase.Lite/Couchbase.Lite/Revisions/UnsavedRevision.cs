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
            attachment.Name = name;
            attachment.Revision = this;
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
        /// <summary>The contents of this <see cref="Couchbase.Lite.Revision"/> of the <see cref="Couchbase.Lite.Document"/>.</summary>
        /// <remarks>
        /// The contents of this revision of the document.
        /// Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
        /// </remarks>
        /// <returns>contents of this revision of the document.</returns>
        public override IDictionary<String, Object> Properties {
            get {
                return properties;
            }
        }

        public void SetProperties(IDictionary<String, Object> newProperties)
        {
            properties = newProperties;
        }

        /// <summary>The user-defined properties, without the ones reserved by CouchDB.</summary>
        /// <remarks>
        /// Gets or sets the userProperties of the <see cref="Couchbase.Lite.Revision"/>.  Get, returns the properties 
        /// of the <see cref="Couchbase.Lite.Revision"/> without any properties with keys prefixed with '_' (which 
        /// contain Couchbase Lite data).  Set, replaces all properties except for those with keys prefixed with '_'.
        /// </remarks>
        /// <returns>user-defined properties, without the ones reserved by CouchDB.</returns>
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
        /// Saves the <see cref="Couchbase.Lite.UnsavedRevision"/>.  This will fail if its parent is not the current 
        /// <see cref="Couchbase.Lite.Revision"/> of the associated <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public SavedRevision Save() { return Document.PutProperties(Properties, ParentId); }

        /// <summary>
        /// Sets the attachment.
        /// </summary>
        /// <remarks>
        /// Sets the attachment with the given name.  The <see cref="Couchbase.Lite.Attachment"/> data will be 
        /// written to the <see cref="Couchbase.Lite.Database"/> when the <see cref="Couchbase.Lite.Revision"/> is saved.
        /// </remarks>
        /// <param name="name">Name.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="content">Content URL.</param>
        public void SetAttachment(String name, String contentType, IEnumerable<Byte> content) {
            var attachment = new Attachment(new MemoryStream(content.ToArray()), contentType);
            AddAttachment(attachment, name);
        }

        /// <summary>
        /// Sets the attachment.
        /// </summary>
        /// <remarks>
        /// Sets the attachment with the given name.  The <see cref="Couchbase.Lite.Attachment"/> data will be 
        /// written to the <see cref="Couchbase.Lite.Database"/> when the <see cref="Couchbase.Lite.Revision"/> is saved.
        /// </remarks>
        /// <param name="name">Name.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="content">Content stream.</param>
        public void SetAttachment(String name, String contentType, Stream content) {
            var attachment = new Attachment(content, contentType);
            AddAttachment(attachment, name);
        }

        /// <summary>
        /// Sets the attachment.
        /// </summary>
        /// <remarks>
        /// Sets the attachment with the given name.  The <see cref="Couchbase.Lite.Attachment"/> data will be 
        /// written to the <see cref="Couchbase.Lite.Database"/> when the <see cref="Couchbase.Lite.Revision"/> is saved.
        /// </remarks>
        /// <param name="name">Name.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="contentUrl">Content URL.</param>
        public void SetAttachment(String name, String contentType, Uri contentUrl) {
            try
            {
                var inputStream = contentUrl.OpenConnection(new Proxy()).GetInputStream();
                var length = inputStream.GetWrappedStream().Length;
                var inputBytes = new byte[length];
                inputStream.Read(inputBytes);
                inputStream.Close();
                SetAttachment(name, contentType, inputBytes);
            }
            catch (IOException e)
            {
                Log.E(Database.Tag, "Error opening stream for url: " + contentUrl);
                throw new RuntimeException(e);
            }
        }

        /// <summary>Deletes any existing attachment with the given name.</summary>
        /// <remarks>
        /// Deletes any existing attachment with the given name.
        /// The attachment will be deleted from the database when the revision is saved.
        /// </remarks>
        /// <param name="name">The attachment name.</param>
        public void RemoveAttachment(String name) { AddAttachment(null, name); }

    #endregion
    
    }

}
