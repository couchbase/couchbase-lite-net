/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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
using System.Collections.Generic;
using Couchbase;
using Sharpen;

namespace Couchbase
{
	/// <summary>Stores information about a revision -- its docID, revID, and whether it's deleted.
	/// 	</summary>
	/// <remarks>
	/// Stores information about a revision -- its docID, revID, and whether it's deleted.
	/// It can also store the sequence number and document contents (they can be added after creation).
	/// </remarks>
	public class CBLRevision
	{
		private string docId;

		private string revId;

		private bool deleted;

		private CBLBody body;

		private long sequence;

		private CBLDatabase database;

		public CBLRevision(string docId, string revId, bool deleted, CBLDatabase database
			)
		{
			this.docId = docId;
			this.revId = revId;
			this.deleted = deleted;
			this.database = database;
		}

		public CBLRevision(CBLBody body, CBLDatabase database) : this((string)body.GetPropertyForKey
			("_id"), (string)body.GetPropertyForKey("_rev"), (((bool)body.GetPropertyForKey(
			"_deleted") != null) && ((bool)body.GetPropertyForKey("_deleted") == true)), database
			)
		{
			this.body = body;
		}

		public CBLRevision(IDictionary<string, object> properties, CBLDatabase database) : 
			this(new CBLBody(properties), database)
		{
		}

		public virtual IDictionary<string, object> GetProperties()
		{
			IDictionary<string, object> result = null;
			if (body != null)
			{
				result = body.GetProperties();
			}
			return result;
		}

		public virtual void SetProperties(IDictionary<string, object> properties)
		{
			this.body = new CBLBody(properties);
			// this is a much more simplified version that what happens on the iOS.  it was
			// done this way due to time constraints, so at some point this needs to be
			// revisited to port the remaining functionality.
			IDictionary<string, object> attachments = (IDictionary<string, object>)properties
				.Get("_attachments");
			if (attachments != null && attachments.Count > 0)
			{
				foreach (string attachmentName in attachments.Keys)
				{
					IDictionary<string, object> attachment = (IDictionary<string, object>)attachments
						.Get(attachmentName);
					// if there is actual data in this attachment, no need to try to install it
					if (attachment.ContainsKey("data"))
					{
						continue;
					}
					CBLStatus status = database.InstallPendingAttachment(attachment);
					if (status.IsSuccessful() == false)
					{
						string msg = string.Format("Unable to install pending attachment: %s.  Status: %d"
							, attachment.ToString(), status.GetCode());
						throw new InvalidOperationException(msg);
					}
				}
			}
		}

		public virtual byte[] GetJson()
		{
			byte[] result = null;
			if (body != null)
			{
				result = body.GetJson();
			}
			return result;
		}

		public virtual void SetJson(byte[] json)
		{
			this.body = new CBLBody(json);
		}

		public override bool Equals(object o)
		{
			bool result = false;
			if (o is Couchbase.CBLRevision)
			{
				Couchbase.CBLRevision other = (Couchbase.CBLRevision)o;
				if (docId.Equals(other.docId) && revId.Equals(other.revId))
				{
					result = true;
				}
			}
			return result;
		}

		public override int GetHashCode()
		{
			return docId.GetHashCode() ^ revId.GetHashCode();
		}

		public virtual string GetDocId()
		{
			return docId;
		}

		public virtual void SetDocId(string docId)
		{
			this.docId = docId;
		}

		public virtual string GetRevId()
		{
			return revId;
		}

		public virtual void SetRevId(string revId)
		{
			this.revId = revId;
		}

		public virtual bool IsDeleted()
		{
			return deleted;
		}

		public virtual void SetDeleted(bool deleted)
		{
			this.deleted = deleted;
		}

		public virtual CBLBody GetBody()
		{
			return body;
		}

		public virtual void SetBody(CBLBody body)
		{
			this.body = body;
		}

		public virtual Couchbase.CBLRevision CopyWithDocID(string docId, string revId)
		{
			System.Diagnostics.Debug.Assert(((docId != null) && (revId != null)));
			System.Diagnostics.Debug.Assert(((this.docId == null) || (this.docId.Equals(docId
				))));
			Couchbase.CBLRevision result = new Couchbase.CBLRevision(docId, revId, deleted, database
				);
			IDictionary<string, object> properties = GetProperties();
			if (properties == null)
			{
				properties = new Dictionary<string, object>();
			}
			properties.Put("_id", docId);
			properties.Put("_rev", revId);
			result.SetProperties(properties);
			return result;
		}

		public virtual void SetSequence(long sequence)
		{
			this.sequence = sequence;
		}

		public virtual long GetSequence()
		{
			return sequence;
		}

		public override string ToString()
		{
			return "{" + this.docId + " #" + this.revId + (deleted ? "DEL" : string.Empty) + 
				"}";
		}

		/// <summary>Generation number: 1 for a new document, 2 for the 2nd revision, ...</summary>
		/// <remarks>
		/// Generation number: 1 for a new document, 2 for the 2nd revision, ...
		/// Extracted from the numeric prefix of the revID.
		/// </remarks>
		public virtual int GetGeneration()
		{
			return GenerationFromRevID(revId);
		}

		public static int GenerationFromRevID(string revID)
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
