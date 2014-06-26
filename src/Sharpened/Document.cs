//
// Document.cs
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

using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>A CouchbaseLite document.</summary>
	/// <remarks>A CouchbaseLite document.</remarks>
	public sealed class Document
	{
		/// <summary>The document's owning database.</summary>
		/// <remarks>The document's owning database.</remarks>
		private Database database;

		/// <summary>The document's ID.</summary>
		/// <remarks>The document's ID.</remarks>
		private string documentId;

		/// <summary>The current/latest revision.</summary>
		/// <remarks>The current/latest revision. This object is cached.</remarks>
		private SavedRevision currentRevision;

		/// <summary>Change Listeners</summary>
		private IList<Document.ChangeListener> changeListeners = new AList<Document.ChangeListener
			>();

		/// <summary>Constructor</summary>
		/// <param name="database">The document's owning database</param>
		/// <param name="documentId">The document's ID</param>
		/// <exclude></exclude>
		[InterfaceAudience.Private]
		public Document(Database database, string documentId)
		{
			this.database = database;
			this.documentId = documentId;
		}

		/// <summary>Get the document's owning database.</summary>
		/// <remarks>Get the document's owning database.</remarks>
		[InterfaceAudience.Public]
		public Database GetDatabase()
		{
			return database;
		}

		/// <summary>Get the document's ID</summary>
		[InterfaceAudience.Public]
		public string GetId()
		{
			return documentId;
		}

		/// <summary>Is this document deleted? (That is, does its current revision have the '_deleted' property?)
		/// 	</summary>
		/// <returns>boolean to indicate whether deleted or not</returns>
		[InterfaceAudience.Public]
		public bool IsDeleted()
		{
			return GetCurrentRevision().IsDeletion();
		}

		/// <summary>Get the ID of the current revision</summary>
		[InterfaceAudience.Public]
		public string GetCurrentRevisionId()
		{
			SavedRevision rev = GetCurrentRevision();
			if (rev == null)
			{
				return null;
			}
			return rev.GetId();
		}

		/// <summary>Get the current revision</summary>
		[InterfaceAudience.Public]
		public SavedRevision GetCurrentRevision()
		{
			if (currentRevision == null)
			{
				currentRevision = GetRevisionWithId(null);
			}
			return currentRevision;
		}

		/// <summary>Returns the document's history as an array of CBLRevisions.</summary>
		/// <remarks>Returns the document's history as an array of CBLRevisions. (See SavedRevision's method.)
		/// 	</remarks>
		/// <returns>document's history</returns>
		/// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public IList<SavedRevision> GetRevisionHistory()
		{
			if (GetCurrentRevision() == null)
			{
				Log.W(Database.Tag, "getRevisionHistory() called but no currentRevision");
				return null;
			}
			return GetCurrentRevision().GetRevisionHistory();
		}

		/// <summary>Returns all the current conflicting revisions of the document.</summary>
		/// <remarks>
		/// Returns all the current conflicting revisions of the document. If the document is not
		/// in conflict, only the single current revision will be returned.
		/// </remarks>
		/// <returns>all current conflicting revisions of the document</returns>
		/// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public IList<SavedRevision> GetConflictingRevisions()
		{
			return GetLeafRevisions(false);
		}

		/// <summary>
		/// Returns all the leaf revisions in the document's revision tree,
		/// including deleted revisions (i.e.
		/// </summary>
		/// <remarks>
		/// Returns all the leaf revisions in the document's revision tree,
		/// including deleted revisions (i.e. previously-resolved conflicts.)
		/// </remarks>
		/// <returns>all the leaf revisions in the document's revision tree</returns>
		/// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public IList<SavedRevision> GetLeafRevisions()
		{
			return GetLeafRevisions(true);
		}

		/// <summary>The contents of the current revision of the document.</summary>
		/// <remarks>
		/// The contents of the current revision of the document.
		/// This is shorthand for self.currentRevision.properties.
		/// Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
		/// </remarks>
		/// <returns>contents of the current revision of the document.</returns>
		[InterfaceAudience.Public]
		public IDictionary<string, object> GetProperties()
		{
			return GetCurrentRevision().GetProperties();
		}

		/// <summary>The user-defined properties, without the ones reserved by CouchDB.</summary>
		/// <remarks>
		/// The user-defined properties, without the ones reserved by CouchDB.
		/// This is based on -properties, with every key whose name starts with "_" removed.
		/// </remarks>
		/// <returns>user-defined properties, without the ones reserved by CouchDB.</returns>
		[InterfaceAudience.Public]
		public IDictionary<string, object> GetUserProperties()
		{
			return GetCurrentRevision().GetUserProperties();
		}

		/// <summary>Deletes this document by adding a deletion revision.</summary>
		/// <remarks>
		/// Deletes this document by adding a deletion revision.
		/// This will be replicated to other databases.
		/// </remarks>
		/// <returns>boolean to indicate whether deleted or not</returns>
		/// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public bool Delete()
		{
			return GetCurrentRevision().DeleteDocument() != null;
		}

		/// <summary>Purges this document from the database; this is more than deletion, it forgets entirely about it.
		/// 	</summary>
		/// <remarks>
		/// Purges this document from the database; this is more than deletion, it forgets entirely about it.
		/// The purge will NOT be replicated to other databases.
		/// </remarks>
		/// <returns>boolean to indicate whether purged or not</returns>
		/// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public bool Purge()
		{
			IDictionary<string, IList<string>> docsToRevs = new Dictionary<string, IList<string
				>>();
			IList<string> revs = new AList<string>();
			revs.AddItem("*");
			docsToRevs.Put(documentId, revs);
			database.PurgeRevisions(docsToRevs);
			database.RemoveDocumentFromCache(this);
			return true;
		}

		/// <summary>The revision with the specified ID.</summary>
		/// <remarks>The revision with the specified ID.</remarks>
		/// <param name="id">the revision ID</param>
		/// <returns>the SavedRevision object</returns>
		[InterfaceAudience.Public]
		public SavedRevision GetRevision(string id)
		{
			if (currentRevision != null && id.Equals(currentRevision.GetId()))
			{
				return currentRevision;
			}
			EnumSet<Database.TDContentOptions> contentOptions = EnumSet.NoneOf<Database.TDContentOptions
				>();
			RevisionInternal revisionInternal = database.GetDocumentWithIDAndRev(GetId(), id, 
				contentOptions);
			SavedRevision revision = null;
			revision = GetRevisionFromRev(revisionInternal);
			return revision;
		}

		/// <summary>
		/// Creates an unsaved new revision whose parent is the currentRevision,
		/// or which will be the first revision if the document doesn't exist yet.
		/// </summary>
		/// <remarks>
		/// Creates an unsaved new revision whose parent is the currentRevision,
		/// or which will be the first revision if the document doesn't exist yet.
		/// You can modify this revision's properties and attachments, then save it.
		/// No change is made to the database until/unless you save the new revision.
		/// </remarks>
		/// <returns>the newly created revision</returns>
		[InterfaceAudience.Public]
		public UnsavedRevision CreateRevision()
		{
			return new UnsavedRevision(this, GetCurrentRevision());
		}

		/// <summary>Shorthand for getProperties().get(key)</summary>
		[InterfaceAudience.Public]
		public object GetProperty(string key)
		{
			return GetCurrentRevision().GetProperties().Get(key);
		}

		/// <summary>Saves a new revision.</summary>
		/// <remarks>
		/// Saves a new revision. The properties dictionary must have a "_rev" property
		/// whose ID matches the current revision's (as it will if it's a modified
		/// copy of this document's .properties property.)
		/// </remarks>
		/// <param name="properties">the contents to be saved in the new revision</param>
		/// <returns>a new SavedRevision</returns>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public SavedRevision PutProperties(IDictionary<string, object> properties)
		{
			string prevID = (string)properties.Get("_rev");
			bool allowConflict = false;
			return PutProperties(properties, prevID, allowConflict);
		}

		/// <summary>Saves a new revision by letting the caller update the existing properties.
		/// 	</summary>
		/// <remarks>
		/// Saves a new revision by letting the caller update the existing properties.
		/// This method handles conflicts by retrying (calling the block again).
		/// The DocumentUpdater implementation should modify the properties of the new revision and return YES to save or
		/// NO to cancel. Be careful: the DocumentUpdater can be called multiple times if there is a conflict!
		/// </remarks>
		/// <param name="updater">
		/// the callback DocumentUpdater implementation.  Will be called on each
		/// attempt to save. Should update the given revision's properties and then
		/// return YES, or just return NO to cancel.
		/// </param>
		/// <returns>The new saved revision, or null on error or cancellation.</returns>
		/// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public SavedRevision Update(Document.DocumentUpdater updater)
		{
			int lastErrorCode = Status.Unknown;
			do
			{
				UnsavedRevision newRev = CreateRevision();
				if (updater.Update(newRev) == false)
				{
					break;
				}
				try
				{
					SavedRevision savedRev = newRev.Save();
					if (savedRev != null)
					{
						return savedRev;
					}
				}
				catch (CouchbaseLiteException e)
				{
					lastErrorCode = e.GetCBLStatus().GetCode();
				}
			}
			while (lastErrorCode == Status.Conflict);
			return null;
		}

		[InterfaceAudience.Public]
		public void AddChangeListener(Document.ChangeListener changeListener)
		{
			changeListeners.AddItem(changeListener);
		}

		[InterfaceAudience.Public]
		public void RemoveChangeListener(Document.ChangeListener changeListener)
		{
			changeListeners.Remove(changeListener);
		}

		/// <summary>A delegate that can be used to update a Document.</summary>
		/// <remarks>A delegate that can be used to update a Document.</remarks>
		public interface DocumentUpdater
		{
			bool Update(UnsavedRevision newRevision);
		}

		/// <summary>The type of event raised when a Document changes.</summary>
		/// <remarks>
		/// The type of event raised when a Document changes. This event is not raised in response
		/// to local Document changes.
		/// </remarks>
		public class ChangeEvent
		{
			private Document source;

			private DocumentChange change;

			public ChangeEvent(Document source, DocumentChange documentChange)
			{
				this.source = source;
				this.change = documentChange;
			}

			public virtual Document GetSource()
			{
				return source;
			}

			public virtual DocumentChange GetChange()
			{
				return change;
			}
		}

		/// <summary>A delegate that can be used to listen for Document changes.</summary>
		/// <remarks>A delegate that can be used to listen for Document changes.</remarks>
		public interface ChangeListener
		{
			void Changed(Document.ChangeEvent @event);
		}

		/// <summary>Get the document's abbreviated ID</summary>
		/// <exclude></exclude>
		[InterfaceAudience.Private]
		public string GetAbbreviatedId()
		{
			string abbreviated = documentId;
			if (documentId.Length > 10)
			{
				string firstFourChars = Sharpen.Runtime.Substring(documentId, 0, 4);
				string lastFourChars = Sharpen.Runtime.Substring(documentId, abbreviated.Length -
					 4);
				return string.Format("%s..%s", firstFourChars, lastFourChars);
			}
			return documentId;
		}

		/// <exclude></exclude>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		internal IList<SavedRevision> GetLeafRevisions(bool includeDeleted)
		{
			IList<SavedRevision> result = new AList<SavedRevision>();
			RevisionList revs = database.GetAllRevisionsOfDocumentID(documentId, true);
			foreach (RevisionInternal rev in revs)
			{
				// add it to result, unless we are not supposed to include deleted and it's deleted
				if (!includeDeleted && rev.IsDeleted())
				{
				}
				else
				{
					// don't add it
					result.AddItem(GetRevisionFromRev(rev));
				}
			}
			return Sharpen.Collections.UnmodifiableList(result);
		}

		/// <exclude></exclude>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		internal SavedRevision PutProperties(IDictionary<string, object> properties, string
			 prevID, bool allowConflict)
		{
			string newId = null;
			if (properties != null && properties.ContainsKey("_id"))
			{
				newId = (string)properties.Get("_id");
			}
			if (newId != null && !Sharpen.Runtime.EqualsIgnoreCase(newId, GetId()))
			{
				Log.W(Database.Tag, string.Format("Trying to put wrong _id to this: %s properties: %s"
					, this, properties));
			}
			// Process _attachments dict, converting CBLAttachments to dicts:
			IDictionary<string, object> attachments = null;
			if (properties != null && properties.ContainsKey("_attachments"))
			{
				attachments = (IDictionary<string, object>)properties.Get("_attachments");
			}
			if (attachments != null && attachments.Count > 0)
			{
				IDictionary<string, object> updatedAttachments = Attachment.InstallAttachmentBodies
					(attachments, database);
				properties.Put("_attachments", updatedAttachments);
			}
			bool hasTrueDeletedProperty = false;
			if (properties != null)
			{
				hasTrueDeletedProperty = properties.Get("_deleted") != null && ((bool)properties.
					Get("_deleted"));
			}
			bool deleted = (properties == null) || hasTrueDeletedProperty;
			RevisionInternal rev = new RevisionInternal(documentId, null, deleted, database);
			if (properties != null)
			{
				rev.SetProperties(properties);
			}
			RevisionInternal newRev = database.PutRevision(rev, prevID, allowConflict);
			if (newRev == null)
			{
				return null;
			}
			return new SavedRevision(this, newRev);
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal SavedRevision GetRevisionFromRev(RevisionInternal internalRevision)
		{
			if (internalRevision == null)
			{
				return null;
			}
			else
			{
				if (currentRevision != null && internalRevision.GetRevId().Equals(currentRevision
					.GetId()))
				{
					return currentRevision;
				}
				else
				{
					return new SavedRevision(this, internalRevision);
				}
			}
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal SavedRevision GetRevisionWithId(string revId)
		{
			if (revId != null && currentRevision != null && revId.Equals(currentRevision.GetId
				()))
			{
				return currentRevision;
			}
			return GetRevisionFromRev(database.GetDocumentWithIDAndRev(GetId(), revId, EnumSet
				.NoneOf<Database.TDContentOptions>()));
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal void LoadCurrentRevisionFrom(QueryRow row)
		{
			if (row.GetDocumentRevisionId() == null)
			{
				return;
			}
			string revId = row.GetDocumentRevisionId();
			if (currentRevision == null || RevIdGreaterThanCurrent(revId))
			{
				IDictionary<string, object> properties = row.GetDocumentProperties();
				if (properties != null)
				{
					RevisionInternal rev = new RevisionInternal(properties, row.GetDatabase());
					currentRevision = new SavedRevision(this, rev);
				}
			}
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		private bool RevIdGreaterThanCurrent(string revId)
		{
			return (RevisionInternal.CBLCompareRevIDs(revId, currentRevision.GetId()) > 0);
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal void RevisionAdded(DocumentChange documentChange)
		{
			RevisionInternal rev = documentChange.GetWinningRevision();
			if (rev == null)
			{
				return;
			}
			// current revision didn't change
			if (currentRevision != null && !rev.GetRevId().Equals(currentRevision.GetId()))
			{
				currentRevision = new SavedRevision(this, rev);
			}
			foreach (Document.ChangeListener listener in changeListeners)
			{
				listener.Changed(new Document.ChangeEvent(this, documentChange));
			}
		}
	}
}
