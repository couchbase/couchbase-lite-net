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
using Couchbase.Lite.Util;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Store;
using System.Diagnostics;

namespace Couchbase.Lite 
{
    /// <summary>
    /// A result row for a Couchbase Lite <see cref="Couchbase.Lite.View"/> <see cref="Couchbase.Lite.Query"/>.
    /// </summary>
    public sealed class QueryRow 
    {

        #region Constants

        private const string TAG = "QueryRow";

        #endregion

        #region Variables

        private IRevisionInformation _documentRevision;
        private IQueryRowStore _storage;
        private object _parsedKey, _parsedValue;

        #endregion

    #region Constructors

        internal QueryRow(string documentId, long sequence, object key, object value, IRevisionInformation revision, IQueryRowStore storage)
        {
            // Don't initialize _database yet. I might be instantiated on a background thread (if the
            // query is async) which has a different CBLDatabase instance than the original caller.
            // Instead, the database property will be filled in when I'm added to a CBLQueryEnumerator.
            SourceDocumentId = documentId;
            SequenceNumber = sequence;
            _key = key;
            _value = value;
            _documentRevision = revision;
            _storage = storage;
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
                if (DocumentId == null || Database == null) {
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
        public object Key 
        { 
            get {
                var key = _parsedKey;
                if (key == null) {
                    key = _key;
                    var keyData = key as IEnumerable<byte>;
                    if (keyData != null) {
                        key = Manager.GetObjectMapper().ReadValue<object>(keyData);
                        _parsedKey = key;
                    }
                }

                return key;
            }
        }
        private object _key;

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.QueryRow"/>'s value.
        /// </summary>
        /// <value>Rhe <see cref="Couchbase.Lite.QueryRow"/>'s value.</value>
        public object Value 
        { 
            get {
                return ValueAs<object>();
            }
        }
        private object _value;

        /// <summary>
        /// Gets the Id of the associated <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The Id of the associated <see cref="Couchbase.Lite.Document"/>.</value>
        public String DocumentId {
            get {
                // Get the doc id from either the embedded document contents, or the '_id' value key.
                // Failing that, there's no document linking, so use the regular old SourceDocumentId
                if (_documentRevision != null) {
                    return _documentRevision.DocID;
                }

                var valueDic = Value as IDictionary<string, object>;
                if (valueDic != null) {
                    var docId = valueDic.GetCast<string>("_id");
                    return docId ?? SourceDocumentId;
                }

                return SourceDocumentId;
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
                // Get the revision id from either the embedded document contents,
                // or the '_rev' or 'rev' value key:
                if (_documentRevision != null) {
                    return _documentRevision.RevID;
                }

                var value = Value as IDictionary<string, object>;
                var rev = value == null ? null : value.GetCast<string>("_rev");
                if (value != null && rev == null) {
                    rev = value.GetCast<string>("rev");
                }

                return rev;
            }
        }

        /// <summary>
        /// Gets the properties of the associated <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The properties of the associated <see cref="Couchbase.Lite.Document"/>.</value>
        public IDictionary<String, Object> DocumentProperties { 
            get {
                return _documentRevision != null ? _documentRevision.GetProperties() : null;
            }
        }

        /// <summary>
        /// Gets the sequence number of the associated <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <value>The sequence number.</value>
        public Int64 SequenceNumber { get; private set; }

        /// <summary>
        /// Returns the value of the QueryRow and interprets it as the given type
        /// </summary>
        /// <returns>The value of the QueryRow and interprets it as the given type.</returns>
        /// <typeparam name="T">The type to interpret the result as</typeparam>
        public T ValueAs<T>()
        {
            var value = _parsedValue;
            if (value == null || !(value is T)) {
                value = _value;
                var valueData = value as IEnumerable<byte>;
                if (valueData != null) {
                    // _value may start out as unparsed Collatable data
                    var storage = _storage;
                    Debug.Assert(storage != null);
                    if (storage.RowValueIsEntireDoc(valueData)) {
                        // Value is a placeholder ("*") denoting that the map function emitted "doc" as
                        // the value. So load the body of the revision now:
                        if (_documentRevision != null) {
                            value = _documentRevision.GetProperties();
                        } else {
                            Debug.Assert(SequenceNumber != 0);
                            value = storage.DocumentProperties(SourceDocumentId, SequenceNumber);
                            if (value == null) {
                                Log.W(TAG, "Couldn't load doc for row value");
                            }
                        }
                    } else {
                        value = storage.ParseRowValue<T>(valueData);
                    }
                }

                _parsedValue = value;
            }

            return (T)value;
        }

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
            var value = Value as IDictionary<string, object>;
            if (value == null) {
                return null;
            }

            var conflicts = value.GetCast<IList<string>>("_conflicts", new List<string>());
            return from revID in conflicts
                            select doc.GetRevision(revID);
        }

        /// <summary>
        /// Returns the query row formatted as a JSON object
        /// </summary>
        /// <returns>The query row formatted as a JSON object</returns>
        public IDictionary<string, object> AsJSONDictionary()
        {
            var result = new Dictionary<string, object>();
            if (Value != null || SourceDocumentId != null) {
                result.Put("key", Key);
                if (Value != null) {
                    result.Put("value", Value);
                }
                result.Put("id", SourceDocumentId);
                if (DocumentProperties != null) {
                    result.Put("doc", DocumentProperties);
                }
            }
            else {
                result.Put("key", Key);
                result.Put("error", "not_found");
            }

            return result;
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
            if (obj == this) {
                return true;
            }

            var other = obj as QueryRow;
            if (other == null) {
                return false;
            }
                
            var documentPropertiesEqual = Misc.IsEqual(DocumentProperties, other.DocumentProperties);

            if (Database == other.Database &&
                Misc.IsEqual(Key, other.Key) &&
                Misc.IsEqual(SourceDocumentId, other.SourceDocumentId) &&
                documentPropertiesEqual) {
                // If values were emitted, compare them. Otherwise we have nothing to go on so check
                // if _anything_ about the doc has changed (i.e. the sequences are different.)
                if (Value != null || other.Value != null) {
                    return Value.Equals(other.Value);
                }
                else {
                    return SequenceNumber == other.SequenceNumber;
                }
            }
            return false;
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="Couchbase.Lite.QueryRow"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a
        /// hash table.</returns>
        public override int GetHashCode()
        {
            return DocumentProperties.GetHashCode();
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.QueryRow"/>.
        /// </summary>
        /// <returns>A <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.QueryRow"/>.</returns>
        public override string ToString()
        {
            return AsJSONDictionary().ToString();
        }

    #endregion

    }

}
