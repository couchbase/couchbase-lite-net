using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    public partial class Document {

    #region Instance Members
        //Properties
        public Database Database { get { throw new NotImplementedException(); } }

        public String Id { get { throw new NotImplementedException(); } }

        public Boolean Deleted { get { throw new NotImplementedException(); } }

        public String CurrentRevisionId { get { throw new NotImplementedException(); } }

        public SavedRevision CurrentRevision { get { throw new NotImplementedException(); } }

        public IEnumerable<SavedRevision> RevisionHistory { get { throw new NotImplementedException(); } }

        public IEnumerable<SavedRevision> ConflictingRevisions { get { throw new NotImplementedException(); } }

        public IEnumerable<SavedRevision> LeafRevisions { get { throw new NotImplementedException(); } }

        public Dictionary<String, Object> Properties { get { throw new NotImplementedException(); } }

        public Dictionary<String, Object> UserProperties { get { throw new NotImplementedException(); } }

        public Object model { get; set; }

        //Methods
        public void Delete() { throw new NotImplementedException(); }

        public void Purge() { throw new NotImplementedException(); }

        public SavedRevision GetRevision(String id) { throw new NotImplementedException(); }

        public UnsavedRevision CreateRevision() { throw new NotImplementedException(); }

        public Object GetProperty(String key) { throw new NotImplementedException(); }

        public SavedRevision PutProperties(Dictionary<String, Object> properties) { throw new NotImplementedException(); }

        public SavedRevision Update(UpdateDelegate updateDelegate) { throw new NotImplementedException(); }

        public event EventHandler<DocumentChangeEventArgs> Change;

    #endregion
    
    #region Delegates
        

        public delegate Boolean UpdateDelegate(UnsavedRevision revision);

    #endregion
    
    #region EventArgs Subclasses
        public class DocumentChangeEventArgs : EventArgs {

            //Properties
            public Document Source { get { throw new NotImplementedException(); } }

            public DocumentChange Change { get { throw new NotImplementedException(); } }

        }

    #endregion
    
    }

    public partial class DocumentChange {

    #region Instance Members
        //Properties
        public String DocumentId { get { throw new NotImplementedException(); } }

        public String RevisionId { get { throw new NotImplementedException(); } }

        public Boolean IsCurrentRevision { get { throw new NotImplementedException(); } }

        public Boolean IsConflict { get { throw new NotImplementedException(); } }

        public Uri SourceUrl { get { throw new NotImplementedException(); } }

    #endregion
    
    }

}

