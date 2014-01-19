using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;

namespace Couchbase.Lite 
{

    public partial class QueryRow 
    {

    #region Constructors

        internal QueryRow(string documentId, long sequence, object key, object value, IDictionary<String, Object> documentProperties)
        {
            SourceDocumentId = documentId;
            SequenceNumber = sequence;
            Key = key;
            Value = value;
            DocumentProperties = documentProperties;
        }

    #endregion

    #region Instance Members

        public Database Database { get; internal set; }

        public Document Document { get; private set; }

        public Object Key { get; private set; }

        public Object Value { get; private set; }

        public String DocumentId { get; private set; }

        public String SourceDocumentId { get; private set; }

        public String DocumentRevisionId { get; private set; }

        public IDictionary<String, Object> DocumentProperties { get; private set; }

        public Int64 SequenceNumber { get; private set; }

        public virtual IEnumerable<SavedRevision> GetConflictingRevisions()
        {
            var doc = Database.GetDocument(SourceDocumentId);
            var valueTmp = (IDictionary<string, object>)Value;

            var conflicts = (IList<string>)valueTmp["_conflicts"];
            if (conflicts == null)
            {
                conflicts = new AList<string>();
            }

            var conflictingRevisions = new AList<SavedRevision>();
            foreach (var conflictRevisionId in conflicts)
            {
                var revision = doc.GetRevision(conflictRevisionId);
                conflictingRevisions.AddItem(revision);
            }
            return conflictingRevisions;
        }


    #endregion
    
    }

}
