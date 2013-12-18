using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Couchbase.Lite.Util;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite {

    

    public partial class DocumentChange
    {
        internal RevisionInternal RevisionInternal { get; private set; }

        internal DocumentChange(RevisionInternal revisionInternal, bool isCurrentRevision, bool isConflict, Uri sourceUrl)
        {
            RevisionInternal = revisionInternal;
            IsCurrentRevision = isCurrentRevision;
            IsConflict = isConflict;
            SourceUrl = sourceUrl;
        }
    
    #region Instance Members
        //Properties
        public String DocumentId { get { throw new NotImplementedException(); } }

        public String RevisionId { get { throw new NotImplementedException(); } }

        public Boolean IsCurrentRevision { get; private set; }

        public Boolean IsConflict { get; private set; }

        public Uri SourceUrl { get; private set; }

    #endregion
    
    #region Static Members

        internal static DocumentChange TempFactory(RevisionInternal revisionInternal, Uri sourceUrl)
        {
            const bool isCurrentRevFixMe = false;
            // TODO: fix this to have a real value
            const bool isConflictRevFixMe = false;
            // TODO: fix this to have a real value
            var change = new DocumentChange(revisionInternal, isCurrentRevFixMe, isConflictRevFixMe, sourceUrl);
            return change;
        }

    #endregion

    }

}
