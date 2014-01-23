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
        internal RevisionInternal AddedRevision { get; private set; }

        internal DocumentChange(RevisionInternal addedRevision, RevisionInternal winningRevision, bool isConflict, Uri sourceUrl)
        {
            AddedRevision = addedRevision;
            WinningRevision = winningRevision;
            IsConflict = isConflict;
            SourceUrl = sourceUrl;
        }
    
    #region Instance Members
        //Properties
        public String DocumentId { get { return AddedRevision.GetDocId(); } }

        public String RevisionId { get { return AddedRevision.GetDocId(); } }

        public RevisionInternal WinningRevision { get; private set; }

        public Boolean IsConflict { get; private set; }

        public Uri SourceUrl { get; private set; }

    #endregion
    
    #region Static Members

        internal static DocumentChange TempFactory(RevisionInternal revisionInternal, Uri sourceUrl, bool inConflict)
        {
            var change = new DocumentChange(revisionInternal, null, inConflict, sourceUrl);
            // TODO: fix winning revision here
            return change;
        }

    #endregion

    }

}
