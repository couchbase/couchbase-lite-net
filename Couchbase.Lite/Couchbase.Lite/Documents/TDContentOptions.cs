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
    /// <summary>Options for what metadata to include in document bodies</summary>
    internal enum TDContentOptions
    {
        TDIncludeAttachments,
        TDIncludeConflicts,
        TDIncludeRevs,
        TDIncludeRevsInfo,
        TDIncludeLocalSeq,
        TDNoBody,
        TDBigAttachmentsFollow
    }

    

    

}
