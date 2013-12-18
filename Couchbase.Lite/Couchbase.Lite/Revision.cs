using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    public abstract partial class Revision {
    
    #region Constructors

        protected internal Revision() : this(null) { }

        /// <summary>Constructor</summary>
        protected internal Revision(Document document)
        {
            this.Document = document;
        }

    #endregion

    #region Non-public Members

        internal Int64 Sequence { get; private set; }

    #endregion

    #region Instance Members
        //Properties
        public Document Document { get; private set; }

        public Database Database { get { throw new NotImplementedException(); } }

        public String Id { get { throw new NotImplementedException(); } }

        public abstract Boolean IsDeletion { get; }

        public abstract Dictionary<String, Object> Properties { get; }

        public abstract Dictionary<String, Object> UserProperties { get; }

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
}

