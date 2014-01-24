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

        public Document Document { 
            get         {
                if (DocumentId == null)
                {
                    return null;
                }
                var document = Database.GetDocument(DocumentId);
                document.LoadCurrentRevisionFrom(this);
                return document;
            }

        }

        public Object Key { get; private set; }

        public Object Value { get; private set; }

        public String DocumentId {
            get {
                // _documentProperties may have been 'redirected' from a different document
                if (DocumentProperties == null) return SourceDocumentId;

                var id = DocumentProperties.Get("_id");
                if (id != null && id is string)
                {
                    return (string)id;
                }
                else
                {
                    return SourceDocumentId;
                }
            }
        }

        public String SourceDocumentId { get; private set; }

        public String DocumentRevisionId {
            get {
                string rev = null;
                if (DocumentProperties != null && DocumentProperties.ContainsKey("_rev"))
                {
                    rev = (string)DocumentProperties.Get("_rev");
                }
                if (rev == null)
                {
                    if (Value is IDictionary)
                    {
                        var mapValue = (IDictionary<string, object>)Value;
                        rev = (string)mapValue.Get("_rev");
                        if (rev == null)
                        {
                            rev = (string)mapValue.Get("rev");
                        }
                    }
                }
                return rev;
            }
        }

        public IDictionary<String, Object> DocumentProperties { get; private set; }

        public Int64 SequenceNumber { get; private set; }

        /// <summary>
        /// Returns all conflicting revisions of the document, or nil if the
        /// document is not in conflict.
        /// </summary>
        /// <remarks>
        /// Returns all conflicting revisions of the document, or nil if the
        /// document is not in conflict.
        /// The first object in the array will be the default "winning" revision that shadows the others.
        /// This is only valid in an allDocuments query whose allDocsMode is set to Query.AllDocsMode.SHOW_CONFLICTS
        /// or Query.AllDocsMode.ONLY_CONFLICTS; otherwise it returns an empty list.
        /// </remarks>
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

        /// <summary>
        /// This is used implicitly by -[LiveQuery update] to decide whether the query result has changed
        /// enough to notify the client.
        /// </summary>
        /// <remarks>
        /// This is used implicitly by -[LiveQuery update] to decide whether the query result has changed
        /// enough to notify the client. So it's important that it not give false positives, else the app
        /// won't get notified of changes.
        /// </remarks>
        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }
            if (!(obj is QueryRow))
            {
                return false;
            }

            var other = (QueryRow)obj;
            var documentPropertiesBothNull = (DocumentProperties == null && other.DocumentProperties == null);
            var documentPropertiesEqual = documentPropertiesBothNull || DocumentProperties.Equals(other.DocumentProperties);

            if (Database == other.Database && Key.Equals(other.Key) && SourceDocumentId.Equals(other.SourceDocumentId) && documentPropertiesEqual)
            {
                // If values were emitted, compare them. Otherwise we have nothing to go on so check
                // if _anything_ about the doc has changed (i.e. the sequences are different.)
                if (Value != null || other.Value != null)
                {
                    return Value.Equals(other.Value);
                }
                else
                {
                    return SequenceNumber == other.SequenceNumber;
                }
            }
            return false;
        }

        public override string ToString()
        {
            return AsJSONDictionary().ToString();
        }

    #endregion

    #region Non-public Members

        public virtual IDictionary<string, object> AsJSONDictionary()
        {
            var result = new Dictionary<string, object>();
            if (Value != null || SourceDocumentId != null)
            {
                result.Put("key", Key);
                if (Value != null)
                {
                    result.Put("value", Value);
                }
                result.Put("id", SourceDocumentId);
                if (DocumentProperties != null)
                {
                    result.Put("doc", DocumentProperties);
                }
            }
            else
            {
                result.Put("key", Key);
                result.Put("error", "not_found");
            }
            return result;
        }

    #endregion
    }

}
