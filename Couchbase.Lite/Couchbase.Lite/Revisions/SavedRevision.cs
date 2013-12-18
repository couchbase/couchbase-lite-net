using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Couchbase.Lite.Internal;

namespace Couchbase.Lite {

    

    public partial class SavedRevision : Revision {

    #region Constructors

        /// <summary>Constructor</summary>
        internal SavedRevision(Document document, RevisionInternal revision)
            : base(document)
        {
            RevisionInternal = revision;
        }

        /// <summary>Constructor</summary>
        internal SavedRevision(Database database, RevisionInternal revision)
            : this(database.GetDocument(revision.GetDocId()), revision)
        {
        }

    #endregion
    
    #region Non-public Members

        internal RevisionInternal RevisionInternal { get; private set; }

    #endregion

    #region Instance Members
        //Properties
        public override Boolean IsDeletion { get { throw new NotImplementedException(); } }

        readonly Dictionary<String, Object> properties;
        public override Dictionary<String, Object> Properties {
            get {
                throw new NotImplementedException();
                return properties;
            }
        }

        readonly Dictionary<String, Object> userProperties;
        public override Dictionary<String, Object> UserProperties {
            get {
                throw new NotImplementedException();
                return userProperties;
            }
        }

        public Boolean PropertiesAvailable { get { throw new NotImplementedException(); } }

        //Methods
        public UnsavedRevision CreateRevision() { throw new NotImplementedException(); }

        public SavedRevision CreateRevision(Dictionary<String, Object> properties) { throw new NotImplementedException(); }

        public SavedRevision DeleteDocument() { throw new NotImplementedException(); }

    #endregion
    
    }

    

}
