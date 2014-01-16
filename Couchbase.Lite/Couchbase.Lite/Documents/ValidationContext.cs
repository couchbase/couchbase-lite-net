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

namespace Couchbase.Lite
{

    public class ValidationContext : IValidationContext
    {
        IList<String> changedKeys;

        private RevisionInternal InternalRevision { get; set; }
        private RevisionInternal NewRevision { get; set; }

        private Database Database { get; set; }

        internal String RejectMessage { get; set; }

        internal ValidationContext(Database database, RevisionInternal currentRevision, RevisionInternal newRevision)
        {
            Database = database;
            InternalRevision = currentRevision;
            NewRevision = newRevision;
        }

        #region IValidationContext implementation

        public void Reject ()
        {
            if (RejectMessage == null)
            {
                Reject("invalid document");
            }
        }

        public void Reject (String message)
        {
            if (RejectMessage == null)
            {
                RejectMessage = message;
            }
        }

        public bool ValidateChanges (ValidateChangeDelegate changeValidator)
        {
            var cur = CurrentRevision.Properties;
            var nuu = NewRevision.GetProperties();

            foreach (var key in ChangedKeys)
            {
                if (!changeValidator(key, cur.Get(key), nuu.Get(key)))
                {
                    Reject(String.Format("Illegal change to '{0}' property", key));
                    return false;
                }
            }
            return true;
        }

        public SavedRevision CurrentRevision {
            get {
                if (InternalRevision != null)
                {
                    try
                    {
                        InternalRevision = Database.LoadRevisionBody(InternalRevision, EnumSet.NoneOf<TDContentOptions>());
                        return new SavedRevision(Database, InternalRevision);
                    }
                    catch (CouchbaseLiteException e)
                    {
                        throw new RuntimeException(e);
                    }
                }
                return null;

            }
        }

        public IEnumerable<String> ChangedKeys {
            get {
                if (changedKeys == null)
                {
                    changedKeys = new AList<String>();
                    var cur = CurrentRevision.Properties;
                    var nuu = NewRevision.GetProperties();

                    foreach (var key in cur.Keys)
                    {
                        if (!cur.Get(key).Equals(nuu.Get(key)) && !key.Equals("_rev"))
                        {
                            changedKeys.AddItem(key);
                        }
                    }

                    foreach (var key in nuu.Keys)
                    {
                        if (cur.Get(key) == null && !key.Equals("_rev") && !key.Equals("_id"))
                        {
                            changedKeys.AddItem(key);
                        }
                    }
                }
                return changedKeys;
            }
        }

        #endregion
    }
}
