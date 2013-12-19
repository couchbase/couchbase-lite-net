using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;
using Couchbase.Lite.Util;

namespace Couchbase.Lite {

    

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
            attachments.Put(name, attachment);
            Properties.Put("_attachments", attachments);
            attachment.Name = name;
            attachment.Revision = this;
        }


    #endregion

    #region Constructors
        protected internal UnsavedRevision(Document document, SavedRevision parentRevision): base(document)
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

        public override SavedRevision Parent {
            get {
                if (String.IsNullOrEmpty (ParentId)) {
                    return null;
                }
                return Document.GetRevision(ParentId);
            }
        }

        public override string ParentId {
            get {
                return ParentRevisionID;
            }
        }

        public override IEnumerable<SavedRevision> RevisionHistory {
            get {
                // (Don't include self in the array, because this revision doesn't really exist yet)
                return Parent != null ? Parent.RevisionHistory : new AList<SavedRevision>();
            }
        }

        public override String Id {
            get {
                return null; // Once a revision is saved, it gets an id, but also becomes a new SavedRevision instance.
            }
        }

        public override IDictionary<String, Object> Properties {
            get {
                return properties;
            }
        }

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

        //Methods
        public SavedRevision Save() { return Document.PutProperties(Properties, ParentId); }

        public void SetAttachment(String name, String contentType, IEnumerable<Byte> content) {
            var attachment = new Attachment(new MemoryStream(content.ToArray()), contentType);
            AddAttachment(attachment, name);        
        }

        public void SetAttachment(String name, String contentType, Uri contentUrl) {
            try
            {
                var inputStream = contentUrl.OpenConnection(new Proxy()).GetInputStream();
                var length = inputStream.GetWrappedStream().Length;
                var inputBytes = new byte[length];
                inputStream.Read(inputBytes);
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
