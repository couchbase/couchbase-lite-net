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
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
	public class UnsavedRevision : Revision
	{
		private IDictionary<string, object> properties;

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		protected internal UnsavedRevision(Document document, SavedRevision parentRevision
			) : base(document)
		{
			if (parentRevision == null)
			{
				parentRevID = null;
			}
			else
			{
				parentRevID = parentRevision.GetId();
			}
			IDictionary<string, object> parentRevisionProperties;
			if (parentRevision == null)
			{
				parentRevisionProperties = null;
			}
			else
			{
				parentRevisionProperties = parentRevision.GetProperties();
			}
			if (parentRevisionProperties == null)
			{
				properties = new Dictionary<string, object>();
				properties.Put("_id", document.GetId());
				if (parentRevID != null)
				{
					properties.Put("_rev", parentRevID);
				}
			}
			else
			{
				properties = new Dictionary<string, object>(parentRevisionProperties);
			}
		}

		/// <summary>Set whether this revision is a deletion or not (eg, marks doc as deleted)
		/// 	</summary>
		[InterfaceAudience.Public]
		public virtual void SetIsDeletion(bool isDeletion)
		{
			if (isDeletion == true)
			{
				properties.Put("_deleted", true);
			}
			else
			{
				Sharpen.Collections.Remove(properties, "_deleted");
			}
		}

		/// <summary>Get the id of the owning document.</summary>
		/// <remarks>Get the id of the owning document.  In the case of an unsaved revision, may return null.
		/// 	</remarks>
		/// <returns></returns>
		[InterfaceAudience.Public]
		public override string GetId()
		{
			return null;
		}

		/// <summary>Set the properties for this revision</summary>
		[InterfaceAudience.Public]
		public virtual void SetProperties(IDictionary<string, object> properties)
		{
			this.properties = properties;
		}

		/// <summary>Saves the new revision to the database.</summary>
		/// <remarks>
		/// Saves the new revision to the database.
		/// This will throw an exception with a 412 error if its parent (the revision it was created from)
		/// is not the current revision of the document.
		/// Afterwards you should use the returned Revision instead of this object.
		/// </remarks>
		/// <returns>A new Revision representing the saved form of the revision.</returns>
		/// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public virtual SavedRevision Save()
		{
			return document.PutProperties(properties, parentRevID);
		}

		/// <summary>Creates or updates an attachment.</summary>
		/// <remarks>
		/// Creates or updates an attachment.
		/// The attachment data will be written to the database when the revision is saved.
		/// </remarks>
		/// <param name="attachment">A newly-created Attachment (not yet associated with any revision)
		/// 	</param>
		/// <param name="name">The attachment name.</param>
		[InterfaceAudience.Public]
		public virtual void AddAttachment(Attachment attachment, string name)
		{
			IDictionary<string, object> attachments = (IDictionary<string, object>)properties
				.Get("_attachments");
			if (attachments == null)
			{
				attachments = new Dictionary<string, object>();
			}
			attachments.Put(name, attachment);
			properties.Put("_attachments", attachments);
			attachment.SetName(name);
			attachment.SetRevision(this);
		}

		/// <summary>Deletes any existing attachment with the given name.</summary>
		/// <remarks>
		/// Deletes any existing attachment with the given name.
		/// The attachment will be deleted from the database when the revision is saved.
		/// </remarks>
		/// <param name="name">The attachment name.</param>
		[InterfaceAudience.Public]
		public virtual void RemoveAttachment(string name)
		{
			AddAttachment(null, name);
		}

		/// <summary>Sets the userProperties of the Revision.</summary>
		/// <remarks>
		/// Sets the userProperties of the Revision.
		/// Set replaces all properties except for those with keys prefixed with '_'.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual void SetUserProperties(IDictionary<string, object> userProperties)
		{
			IDictionary<string, object> newProps = new Dictionary<string, object>();
			newProps.PutAll(userProperties);
			foreach (string key in properties.Keys)
			{
				if (key.StartsWith("_"))
				{
					newProps.Put(key, properties.Get(key));
				}
			}
			// Preserve metadata properties
			properties = newProps;
		}

		/// <summary>Sets the attachment with the given name.</summary>
		/// <remarks>Sets the attachment with the given name. The Attachment data will be written to the Database when the Revision is saved.
		/// 	</remarks>
		/// <param name="name">The name of the Attachment to set.</param>
		/// <param name="contentType">The content-type of the Attachment.</param>
		/// <param name="contentStream">The Attachment content.  The InputStream will be closed after it is no longer needed.
		/// 	</param>
		[InterfaceAudience.Public]
		public virtual void SetAttachment(string name, string contentType, InputStream contentStream
			)
		{
			Attachment attachment = new Attachment(contentStream, contentType);
			AddAttachment(attachment, name);
		}

		/// <summary>Sets the attachment with the given name.</summary>
		/// <remarks>Sets the attachment with the given name. The Attachment data will be written to the Database when the Revision is saved.
		/// 	</remarks>
		/// <param name="name">The name of the Attachment to set.</param>
		/// <param name="contentType">The content-type of the Attachment.</param>
		/// <param name="contentStreamURL">The URL that contains the Attachment content.</param>
		[InterfaceAudience.Public]
		public virtual void SetAttachment(string name, string contentType, Uri contentStreamURL
			)
		{
			try
			{
				InputStream inputStream = contentStreamURL.OpenStream();
				SetAttachment(name, contentType, inputStream);
			}
			catch (IOException e)
			{
				Log.E(Database.Tag, "Error opening stream for url: " + contentStreamURL);
				throw new RuntimeException(e);
			}
		}

		[InterfaceAudience.Public]
		public override IDictionary<string, object> GetProperties()
		{
			return properties;
		}

		[InterfaceAudience.Public]
		public override SavedRevision GetParentRevision()
		{
			if (parentRevID == null || parentRevID.Length == 0)
			{
				return null;
			}
			return document.GetRevision(parentRevID);
		}

		[InterfaceAudience.Public]
		public override string GetParentRevisionId()
		{
			return parentRevID;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public override IList<SavedRevision> GetRevisionHistory()
		{
			// (Don't include self in the array, because this revision doesn't really exist yet)
			SavedRevision parent = GetParentRevision();
			return parent != null ? parent.GetRevisionHistory() : new AList<SavedRevision>();
		}
	}
}
