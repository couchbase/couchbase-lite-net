/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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

using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>A Couchbase Lite Document Revision.</summary>
	/// <remarks>
	/// A Couchbase Lite Document Revision.
	/// Stores information about a revision -- its docID, revID, and whether it's deleted.
	/// It can also store the sequence number and document contents (they can be added after creation).
	/// </remarks>
	public abstract class Revision
	{
		/// <summary>
		/// re
		/// The sequence number of this revision.
		/// </summary>
		/// <remarks>
		/// re
		/// The sequence number of this revision.
		/// </remarks>
		protected internal long sequence;

		/// <summary>The document this is a revision of</summary>
		protected internal Document document;

		/// <summary>The ID of the parentRevision.</summary>
		/// <remarks>The ID of the parentRevision.</remarks>
		protected internal string parentRevID;

		/// <summary>The revision this one is a child of.</summary>
		/// <remarks>The revision this one is a child of.</remarks>
		protected internal SavedRevision parentRevision;

		/// <summary>Constructor</summary>
		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal Revision() : base()
		{
		}

		/// <summary>Constructor</summary>
		/// <exclude></exclude>
		[InterfaceAudience.Private]
		protected internal Revision(Document document)
		{
			this.document = document;
		}

		/// <summary>Get the revision's owning database.</summary>
		/// <remarks>Get the revision's owning database.</remarks>
		[InterfaceAudience.Public]
		public virtual Database GetDatabase()
		{
			return document.GetDatabase();
		}

		/// <summary>Get the document this is a revision of.</summary>
		/// <remarks>Get the document this is a revision of.</remarks>
		[InterfaceAudience.Public]
		public virtual Document GetDocument()
		{
			return document;
		}

		/// <summary>Gets the Revision's id.</summary>
		/// <remarks>Gets the Revision's id.</remarks>
		[InterfaceAudience.Public]
		public abstract string GetId();

		/// <summary>
		/// Does this revision mark the deletion of its document?
		/// (In other words, does it have a "_deleted" property?)
		/// </summary>
		[InterfaceAudience.Public]
		public virtual bool IsDeletion()
		{
			object deleted = GetProperty("_deleted");
			if (deleted == null)
			{
				return false;
			}
			bool deletedBool = (bool)deleted;
			return deletedBool;
		}

		/// <summary>The contents of this revision of the document.</summary>
		/// <remarks>
		/// The contents of this revision of the document.
		/// Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
		/// </remarks>
		/// <returns>contents of this revision of the document.</returns>
		[InterfaceAudience.Public]
		public abstract IDictionary<string, object> GetProperties();

		/// <summary>The user-defined properties, without the ones reserved by CouchDB.</summary>
		/// <remarks>
		/// The user-defined properties, without the ones reserved by CouchDB.
		/// This is based on -properties, with every key whose name starts with "_" removed.
		/// </remarks>
		/// <returns>user-defined properties, without the ones reserved by CouchDB.</returns>
		[InterfaceAudience.Public]
		public virtual IDictionary<string, object> GetUserProperties()
		{
			IDictionary<string, object> result = new Dictionary<string, object>();
			IDictionary<string, object> sourceMap = GetProperties();
			foreach (string key in sourceMap.Keys)
			{
				if (!key.StartsWith("_"))
				{
					result.Put(key, sourceMap.Get(key));
				}
			}
			return result;
		}

		/// <summary>The names of all attachments</summary>
		/// <returns></returns>
		[InterfaceAudience.Public]
		public virtual IList<string> GetAttachmentNames()
		{
			IDictionary<string, object> attachmentMetadata = GetAttachmentMetadata();
			AList<string> result = new AList<string>();
			if (attachmentMetadata != null)
			{
				Sharpen.Collections.AddAll(result, attachmentMetadata.Keys);
			}
			return result;
		}

		/// <summary>All attachments, as Attachment objects.</summary>
		/// <remarks>All attachments, as Attachment objects.</remarks>
		[InterfaceAudience.Public]
		public virtual IList<Attachment> GetAttachments()
		{
			IList<Attachment> result = new AList<Attachment>();
			IList<string> attachmentNames = GetAttachmentNames();
			foreach (string attachmentName in attachmentNames)
			{
				result.AddItem(GetAttachment(attachmentName));
			}
			return result;
		}

		/// <summary>Shorthand for getProperties().get(key)</summary>
		[InterfaceAudience.Public]
		public virtual object GetProperty(string key)
		{
			return GetProperties().Get(key);
		}

		/// <summary>Looks up the attachment with the given name (without fetching its contents yet).
		/// 	</summary>
		/// <remarks>Looks up the attachment with the given name (without fetching its contents yet).
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual Attachment GetAttachment(string name)
		{
			IDictionary<string, object> attachmentsMetadata = GetAttachmentMetadata();
			if (attachmentsMetadata == null)
			{
				return null;
			}
			IDictionary<string, object> attachmentMetadata = (IDictionary<string, object>)attachmentsMetadata
				.Get(name);
			return new Attachment(this, name, attachmentMetadata);
		}

		/// <summary>Gets the parent Revision.</summary>
		/// <remarks>Gets the parent Revision.</remarks>
		[InterfaceAudience.Public]
		public abstract SavedRevision GetParent();

		/// <summary>Gets the parent Revision's id.</summary>
		/// <remarks>Gets the parent Revision's id.</remarks>
		[InterfaceAudience.Public]
		public abstract string GetParentId();

		/// <summary>Returns the history of this document as an array of CBLRevisions, in forward order.
		/// 	</summary>
		/// <remarks>
		/// Returns the history of this document as an array of CBLRevisions, in forward order.
		/// Older revisions are NOT guaranteed to have their properties available.
		/// </remarks>
		/// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public abstract IList<SavedRevision> GetRevisionHistory();

		/// <summary>Compare this revision to the given revision to check for equality.</summary>
		/// <remarks>
		/// Compare this revision to the given revision to check for equality.
		/// The comparison makes sure that both revisions have the same revision ID.
		/// </remarks>
		/// <param name="the">revision to check for equality against</param>
		/// <returns>true if equal, false otherwise</returns>
		[InterfaceAudience.Public]
		public override bool Equals(object o)
		{
			bool result = false;
			if (o is SavedRevision)
			{
				SavedRevision other = (SavedRevision)o;
				if (document.GetId().Equals(other.GetDocument().GetId()) && GetId().Equals(other.
					GetId()))
				{
					result = true;
				}
			}
			return result;
		}

		/// <summary>Custom hashCode based on the hash code of the Document Id and the Revision Id
		/// 	</summary>
		[InterfaceAudience.Public]
		public override int GetHashCode()
		{
			return document.GetId().GetHashCode() ^ GetId().GetHashCode();
		}

		/// <summary>
		/// Returns a string representation of this Revision, including the Document Id, the Revision Id
		/// and whether or not this Revision is a deletion.
		/// </summary>
		/// <remarks>
		/// Returns a string representation of this Revision, including the Document Id, the Revision Id
		/// and whether or not this Revision is a deletion.
		/// </remarks>
		[InterfaceAudience.Public]
		public override string ToString()
		{
			return "{" + this.document.GetId() + " #" + this.GetId() + (IsDeletion() ? "DEL" : 
				string.Empty) + "}";
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal virtual IDictionary<string, object> GetAttachmentMetadata()
		{
			return (IDictionary<string, object>)GetProperty("_attachments");
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal virtual void SetSequence(long sequence)
		{
			this.sequence = sequence;
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal virtual long GetSequence()
		{
			return sequence;
		}

		/// <summary>Generation number: 1 for a new document, 2 for the 2nd revision, ...</summary>
		/// <remarks>
		/// Generation number: 1 for a new document, 2 for the 2nd revision, ...
		/// Extracted from the numeric prefix of the revID.
		/// </remarks>
		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal virtual int GetGeneration()
		{
			return GenerationFromRevID(GetId());
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		internal static int GenerationFromRevID(string revID)
		{
			int generation = 0;
			int dashPos = revID.IndexOf("-");
			if (dashPos > 0)
			{
				generation = System.Convert.ToInt32(Sharpen.Runtime.Substring(revID, 0, dashPos));
			}
			return generation;
		}
	}
}
