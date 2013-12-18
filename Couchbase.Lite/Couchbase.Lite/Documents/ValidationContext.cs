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

    public class ValidationContext : IValidationContext {

        RevisionInternal InternalRevision { get; set; }
        Database Database { get; set; }

        internal Status ErrorType { get; set; }
        internal String ErrorMessage { get; set; }

        internal ValidationContext(Database database, RevisionInternal currentRevision)
        {
            Database = database;
            InternalRevision = currentRevision;
            ErrorType = new Status(StatusCode.Forbidden);
            ErrorMessage = "invalid document";
        }

        #region IValidationContext implementation

        public void Reject ()
        {
            throw new NotImplementedException ();
        }

        public void Reject (string message)
        {
            throw new NotImplementedException ();
        }

        public bool ValidateChanges (ValidateChangeDelegate changeValidator)
        {
            throw new NotImplementedException ();
            var isValid = true;
            foreach(var key in ChangedKeys) {
                var newValue = CurrentRevision.GetProperty(key);
                var oldValue = CurrentRevision.Parent.GetProperty(key);
                isValid &= changeValidator(key, newValue, oldValue);
            }
        }

        public SavedRevision CurrentRevision {
            get {
                if (InternalRevision != null)
                {
                    Database.LoadRevisionBody(InternalRevision, EnumSet.NoneOf<TDContentOptions>());
                }
                throw new NotImplementedException();
                return null;

            }
        }

        public IEnumerable<String> ChangedKeys {
            get {
                throw new NotImplementedException ();
            }
        }

        #endregion

    }
}
