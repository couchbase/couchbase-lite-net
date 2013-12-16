using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    public partial class Revision {

    #region Instance Members
        //Properties
        public Document Document { get { throw new NotImplementedException(); } }

        public Database Database { get { throw new NotImplementedException(); } }

        public String Id { get { throw new NotImplementedException(); } }

        public Boolean IsDeletion { get { throw new NotImplementedException(); } }

        public Dictionary<String, Object> Properties { get { throw new NotImplementedException(); } }

        public Dictionary<String, Object> UserProperties { get { throw new NotImplementedException(); } }

        public SavedRevision Parent { get { throw new NotImplementedException(); } }

        public String ParentId { get { throw new NotImplementedException(); } }

        public IEnumerable<SavedRevision> RevisionHistory { get { throw new NotImplementedException(); } }

        public IEnumerable<String> AttachmentNames { get { throw new NotImplementedException(); } }

        public IEnumerable<Attachment> Attachments { get { throw new NotImplementedException(); } }

        //Methods
        public Object GetProperty(String key) { throw new NotImplementedException(); }

        public Attachment GetAttachment(String name) { throw new NotImplementedException(); }

    #endregion
    
    }

    public partial class SavedRevision : Revision {

    #region Instance Members
        //Properties
        public Boolean PropertiesAvailable { get { throw new NotImplementedException(); } }

        //Methods
        public UnsavedRevision CreateRevision() { throw new NotImplementedException(); }

        public SavedRevision CreateRevision(Dictionary<String, Object> properties) { throw new NotImplementedException(); }

        public SavedRevision DeleteDocument() { throw new NotImplementedException(); }

    #endregion
    
    }

    public partial class UnsavedRevision : Revision {

    #region Instance Members
        //Properties
        public Boolean IsDeletion { get; set; }

        public Dictionary<String, Object> Properties { get; set; }

        public Dictionary<String, Object> UserProperties { get; set; }

        //Methods
        public SavedRevision Save() { throw new NotImplementedException(); }

        public void SetAttachment(String name, String contentType, Object content) { throw new NotImplementedException(); }

        public void SetAttachment(String name, String contentType, Uri contentUrl) { throw new NotImplementedException(); }

        public void RemoveAttachment(String name) { throw new NotImplementedException(); }

    #endregion
    
    }

}

