using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    

    public partial class UnsavedRevision : Revision {

    #region Non-public Members
        String ParentRevisionID { get; set; }
    #endregion

    #region Constructors
        protected internal UnsavedRevision(Document document, SavedRevision parentRevision): base(document)
        {
            if (parentRevision == null)
                ParentRevisionID = null;
            else
                ParentRevisionID = parentRevision.Id;

            IDictionary<string, object> parentRevisionProperties;
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
                properties = new Dictionary<string, object>();
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

        //Methods
        public SavedRevision Save() { throw new NotImplementedException(); }

        public void SetAttachment(String name, String contentType, Object content) { throw new NotImplementedException(); }

        public void SetAttachment(String name, String contentType, Uri contentUrl) { throw new NotImplementedException(); }

        public void RemoveAttachment(String name) { throw new NotImplementedException(); }

    #endregion
    
    }

}
