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

using System.Collections;
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>A result row from a CouchbaseLite view query.</summary>
	/// <remarks>
	/// A result row from a CouchbaseLite view query.
	/// Full-text and geo queries return subclasses -- see CBLFullTextQueryRow and CBLGeoQueryRow.
	/// </remarks>
	public class QueryRow
	{
		/// <summary>The row's key: this is the first parameter passed to the emit() call that generated the row.
		/// 	</summary>
		/// <remarks>The row's key: this is the first parameter passed to the emit() call that generated the row.
		/// 	</remarks>
		private object key;

		/// <summary>The row's value: this is the second parameter passed to the emit() call that generated the row.
		/// 	</summary>
		/// <remarks>The row's value: this is the second parameter passed to the emit() call that generated the row.
		/// 	</remarks>
		private object value;

		/// <summary>The database sequence number of the associated doc/revision.</summary>
		/// <remarks>The database sequence number of the associated doc/revision.</remarks>
		private long sequence;

		/// <summary>The ID of the document that caused this view row to be emitted.</summary>
		/// <remarks>
		/// The ID of the document that caused this view row to be emitted.
		/// This is the value of the "id" property of the JSON view row.
		/// It will be the same as the .documentID property, unless the map function caused a
		/// related document to be linked by adding an "_id" key to the emitted value; in this
		/// case .documentID will refer to the linked document, while sourceDocumentID always
		/// refers to the original document.
		/// In a reduced or grouped query the value will be nil, since the rows don't correspond
		/// to individual documents.
		/// </remarks>
		private string sourceDocumentId;

		/// <summary>The properties of the document this row was mapped from.</summary>
		/// <remarks>
		/// The properties of the document this row was mapped from.
		/// To get this, you must have set the .prefetch property on the query; else this will be nil.
		/// (You can still get the document properties via the .document property, of course. But it
		/// takes a separate call to the database. So if you're doing it for every row, using
		/// .prefetch and .documentProperties is faster.)
		/// </remarks>
		private IDictionary<string, object> documentProperties;

		private Database database;

		/// <summary>
		/// Constructor
		/// The database property will be filled in when I'm added to a QueryEnumerator.
		/// </summary>
		/// <remarks>
		/// Constructor
		/// The database property will be filled in when I'm added to a QueryEnumerator.
		/// </remarks>
		[InterfaceAudience.Private]
		internal QueryRow(string documentId, long sequence, object key, object value, IDictionary
			<string, object> documentProperties)
		{
			this.sourceDocumentId = documentId;
			this.sequence = sequence;
			this.key = key;
			this.value = value;
			this.documentProperties = documentProperties;
		}

		/// <summary>Gets the Database that owns the Query's View.</summary>
		/// <remarks>Gets the Database that owns the Query's View.</remarks>
		[InterfaceAudience.Public]
		public virtual Database GetDatabase()
		{
			return database;
		}

		/// <summary>The document this row was mapped from.</summary>
		/// <remarks>
		/// The document this row was mapped from.  This will be nil if a grouping was enabled in
		/// the query, because then the result rows don't correspond to individual documents.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual Document GetDocument()
		{
			if (GetDocumentId() == null)
			{
				return null;
			}
			Document document = database.GetDocument(GetDocumentId());
			document.LoadCurrentRevisionFrom(this);
			return document;
		}

		/// <summary>The row's key: this is the first parameter passed to the emit() call that generated the row.
		/// 	</summary>
		/// <remarks>The row's key: this is the first parameter passed to the emit() call that generated the row.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual object GetKey()
		{
			return key;
		}

		/// <summary>The row's value: this is the second parameter passed to the emit() call that generated the row.
		/// 	</summary>
		/// <remarks>The row's value: this is the second parameter passed to the emit() call that generated the row.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual object GetValue()
		{
			return value;
		}

		/// <summary>The ID of the document described by this view row.</summary>
		/// <remarks>
		/// The ID of the document described by this view row.  This is not necessarily the same as the
		/// document that caused this row to be emitted; see the discussion of the .sourceDocumentID
		/// property for details.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual string GetDocumentId()
		{
			// _documentProperties may have been 'redirected' from a different document
			if (documentProperties != null && documentProperties.Get("_id") != null && documentProperties
				.Get("_id") is string)
			{
				return (string)documentProperties.Get("_id");
			}
			else
			{
				return sourceDocumentId;
			}
		}

		/// <summary>The ID of the document that caused this view row to be emitted.</summary>
		/// <remarks>
		/// The ID of the document that caused this view row to be emitted.  This is the value of
		/// the "id" property of the JSON view row. It will be the same as the .documentID property,
		/// unless the map function caused a related document to be linked by adding an "_id" key to
		/// the emitted value; in this case .documentID will refer to the linked document, while
		/// sourceDocumentID always refers to the original document.  In a reduced or grouped query
		/// the value will be nil, since the rows don't correspond to individual documents.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual string GetSourceDocumentId()
		{
			return sourceDocumentId;
		}

		/// <summary>The revision ID of the document this row was mapped from.</summary>
		/// <remarks>The revision ID of the document this row was mapped from.</remarks>
		[InterfaceAudience.Public]
		public virtual string GetDocumentRevisionId()
		{
			string rev = null;
			if (documentProperties != null && documentProperties.ContainsKey("_rev"))
			{
				rev = (string)documentProperties.Get("_rev");
			}
			if (rev == null)
			{
				if (value is IDictionary)
				{
					IDictionary<string, object> mapValue = (IDictionary<string, object>)value;
					rev = (string)mapValue.Get("_rev");
					if (rev == null)
					{
						rev = (string)mapValue.Get("rev");
					}
				}
			}
			return rev;
		}

		/// <summary>The properties of the document this row was mapped from.</summary>
		/// <remarks>
		/// The properties of the document this row was mapped from.
		/// To get this, you must have set the -prefetch property on the query; else this will be nil.
		/// The map returned is immutable (run through Collections.unmodifiableMap)
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual IDictionary<string, object> GetDocumentProperties()
		{
			return documentProperties != null ? Sharpen.Collections.UnmodifiableMap(documentProperties
				) : null;
		}

		/// <summary>The local sequence number of the associated doc/revision.</summary>
		/// <remarks>The local sequence number of the associated doc/revision.</remarks>
		[InterfaceAudience.Public]
		public virtual long GetSequenceNumber()
		{
			return sequence;
		}

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
		[InterfaceAudience.Public]
		public virtual IList<SavedRevision> GetConflictingRevisions()
		{
			Document doc = database.GetDocument(sourceDocumentId);
			IDictionary<string, object> valueTmp = (IDictionary<string, object>)value;
			IList<string> conflicts = (IList<string>)valueTmp.Get("_conflicts");
			if (conflicts == null)
			{
				conflicts = new AList<string>();
			}
			IList<SavedRevision> conflictingRevisions = new AList<SavedRevision>();
			foreach (string conflictRevisionId in conflicts)
			{
				SavedRevision revision = doc.GetRevision(conflictRevisionId);
				conflictingRevisions.AddItem(revision);
			}
			return conflictingRevisions;
		}

		/// <summary>Compare this against the given QueryRow for equality.</summary>
		/// <remarks>
		/// Compare this against the given QueryRow for equality.
		/// Implentation Note: This is used implicitly by -[LiveQuery update] to decide whether
		/// the query result has changed enough to notify the client. So it's important that it
		/// not give false positives, else the app won't get notified of changes.
		/// </remarks>
		/// <param name="o">the QueryRow to compare this instance with.</param>
		/// <returns>true if equal, false otherwise.</returns>
		[InterfaceAudience.Public]
		public override bool Equals(object @object)
		{
			if (@object == this)
			{
				return true;
			}
			if (!(@object is Couchbase.Lite.QueryRow))
			{
				return false;
			}
			Couchbase.Lite.QueryRow other = (Couchbase.Lite.QueryRow)@object;
			bool documentPropertiesBothNull = (documentProperties == null && other.GetDocumentProperties
				() == null);
			bool documentPropertiesEqual = documentPropertiesBothNull || documentProperties.Equals
				(other.GetDocumentProperties());
			if (database == other.database && key.Equals(other.GetKey()) && sourceDocumentId.
				Equals(other.GetSourceDocumentId()) && documentPropertiesEqual)
			{
				// If values were emitted, compare them. Otherwise we have nothing to go on so check
				// if _anything_ about the doc has changed (i.e. the sequences are different.)
				if (value != null || other.GetValue() != null)
				{
					return value.Equals(other.GetValue());
				}
				else
				{
					return sequence == other.sequence;
				}
			}
			return false;
		}

		/// <summary>Return a string representation of this QueryRow.</summary>
		/// <remarks>
		/// Return a string representation of this QueryRow.
		/// The string is returned in JSON format.
		/// </remarks>
		/// <returns>the JSON string representing this QueryRow</returns>
		[InterfaceAudience.Public]
		public override string ToString()
		{
			return AsJSONDictionary().ToString();
		}

		[InterfaceAudience.Private]
		internal virtual void SetDatabase(Database database)
		{
			this.database = database;
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		public virtual IDictionary<string, object> AsJSONDictionary()
		{
			IDictionary<string, object> result = new Dictionary<string, object>();
			if (value != null || sourceDocumentId != null)
			{
				result.Put("key", key);
				if (value != null)
				{
					result.Put("value", value);
				}
				result.Put("id", sourceDocumentId);
				if (documentProperties != null)
				{
					result.Put("doc", documentProperties);
				}
			}
			else
			{
				result.Put("key", key);
				result.Put("error", "not_found");
			}
			return result;
		}
	}
}
