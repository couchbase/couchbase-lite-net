using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;
using Couchbase.Lite.Util;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Internal;
using Couchbase.Lite;

namespace Couchbase.Lite {

    public partial interface IValidationContext {

    #region Instance Members
        //Properties
        SavedRevision CurrentRevision { get; }

        IEnumerable<String> ChangedKeys { get; }

        //Methods
        void Reject();

        void Reject(String message);

        Boolean ValidateChanges(ValidateChangeDelegate changeValidator);

    #endregion
    }

}
