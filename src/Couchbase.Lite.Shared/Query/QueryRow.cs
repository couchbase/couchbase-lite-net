//
// QueryRow.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
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
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;

namespace Couchbase.Lite 
{
    /// <summary>
    /// A result row for a Couchbase Lite <see cref="Couchbase.Lite.View"/> <see cref="Couchbase.Lite.Query"/>.
    /// </summary>
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

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.QueryRow"/>'s <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.QueryRow"/>'s <see cref="Couchbase.Lite.View"/>.</value>
        public Database Database { get; internal set; }

        /// <summary>
        /// Gets the associated <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Document"/> associated with the <see cref="Couchbase.Lite.QueryRow"/>'s <see cref="Couchbase.Lite.View"/>.</value>
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

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.QueryRow"/>'s key.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.QueryRow"/>'s key.</value>
        public Object Key { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.QueryRow"/>'s value.
        /// </summary>
        /// <value>Rhe <see cref="Couchbase.Lite.QueryRow"/>'s value.</value>
        public Object Value { get; private set; }

        /// <summary>
        /// Gets the Id of the associated <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The Id of the associated <see cref="Couchbase.Lite.Document"/>.</value>
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

        /// <summary>
        /// Gets the Id of the <see cref="Couchbase.Lite.Document"/> that caused the 
        /// <see cref="Couchbase.Lite.QueryRow"/> to be emitted into the View. 
        /// This will be the same as the documentId property, unless the map function 
        /// caused a related <see cref="Couchbase.Lite.Document"/> to be linked by adding 
        /// an '_id' key to the emmitted value. In this case, documentId will refer to 
        /// the linked <see cref="Couchbase.Lite.Document"/>, while sourceDocumentId always 
        /// refers to the original <see cref="Couchbase.Lite.Document"/>. In a reduced or grouped 
        /// <see cref="Couchbase.Lite.Query"/>, sourceDocumentId will be null because the rows 
        /// don't correspond to individual <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The source document identifier.</value>
        public String SourceDocumentId { get; private set; }

        /// <summary>
        /// Gets the Id of the associated <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <value>The Id of the associated <see cref="Couchbase.Lite.Revision"/>.</value>
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

        /// <summary>
        /// Gets the properties of the associated <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The properties of the associated <see cref="Couchbase.Lite.Document"/>.</value>
        public IDictionary<String, Object> DocumentProperties { get; private set; }

        /// <summary>
        /// Gets the sequence number of the associated <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <value>The sequence number.</value>
        public Int64 SequenceNumber { get; private set; }

        /// <summary>
        /// Gets the conflicting <see cref="Couchbase.Lite.Revision"/>s of the associated <see cref="Couchbase.Lite.Document"/>. 
        /// </summary>
        /// <remarks>
        /// Gets the conflicting <see cref="Couchbase.Lite.Revision"/>s of the associated <see cref="Couchbase.Lite.Document"/>. 
        /// The first <see cref="Couchbase.Lite.Revision"/> in the array will be the default 'winning' <see cref="Couchbase.Lite.Revision"/> 
        /// that shadows the <see cref="Couchbase.Lite.Revision"/>s. This is only valid in an all-documents <see cref="Couchbase.Lite.Query"/> 
        /// whose allDocsMode is set to ShowConflicts or OnlyConflicts, otherwise it returns null.
        /// </remarks>
        /// <returns>The conflicting <see cref="Couchbase.Lite.Revision"/>s of the associated <see cref="Couchbase.Lite.Document"/></returns>
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
                    bool isEqual;
                    if (Value is IDictionary<string, object> && other.Value is IDictionary<string, object>)
                    {
                        return Misc.PropertiesEqual((IDictionary<string, object>)Value, (IDictionary<string, object>)other.Value);
                    }
                    else
                    {
                        return Value.Equals(other.Value);
                    }
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
