//
// QueryRow.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/
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
        public IEnumerable<SavedRevision> GetConflictingRevisions()
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
