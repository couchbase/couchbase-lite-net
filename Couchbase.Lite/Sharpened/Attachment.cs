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
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>A Couchbase Lite Document Attachment.</summary>
	/// <remarks>A Couchbase Lite Document Attachment.</remarks>
	public class Attachment
	{
		/// <summary>The owning document revision.</summary>
		/// <remarks>The owning document revision.</remarks>
		private Revision revision;

		/// <summary>Whether or not this attachment is gzipped</summary>
		private bool gzipped;

		/// <summary>The owning document.</summary>
		/// <remarks>The owning document.</remarks>
		private Document document;

		/// <summary>The filename.</summary>
		/// <remarks>The filename.</remarks>
		private string name;

		/// <summary>The CouchbaseLite metadata about the attachment, that lives in the document.
		/// 	</summary>
		/// <remarks>The CouchbaseLite metadata about the attachment, that lives in the document.
		/// 	</remarks>
		private IDictionary<string, object> metadata;

		/// <summary>The body data.</summary>
		/// <remarks>The body data.</remarks>
		private InputStream body;

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		internal Attachment(InputStream contentStream, string contentType)
		{
			this.body = contentStream;
			metadata = new Dictionary<string, object>();
			metadata.Put("content_type", contentType);
			metadata.Put("follows", true);
			this.gzipped = false;
		}

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		internal Attachment(Revision revision, string name, IDictionary<string, object> metadata
			)
		{
			this.revision = revision;
			this.name = name;
			this.metadata = metadata;
			this.gzipped = false;
		}

		/// <summary>Get the owning document revision.</summary>
		/// <remarks>Get the owning document revision.</remarks>
		[InterfaceAudience.Public]
		public virtual Revision GetRevision()
		{
			return revision;
		}

		/// <summary>Get the owning document.</summary>
		/// <remarks>Get the owning document.</remarks>
		[InterfaceAudience.Public]
		public virtual Document GetDocument()
		{
			return revision.GetDocument();
		}

		/// <summary>Get the filename.</summary>
		/// <remarks>Get the filename.</remarks>
		[InterfaceAudience.Public]
		public virtual string GetName()
		{
			return name;
		}

		/// <summary>Get the MIME type of the contents.</summary>
		/// <remarks>Get the MIME type of the contents.</remarks>
		[InterfaceAudience.Public]
		public virtual string GetContentType()
		{
			return (string)metadata.Get("content_type");
		}

		/// <summary>Get the content (aka 'body') data.</summary>
		/// <remarks>Get the content (aka 'body') data.</remarks>
		/// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public virtual InputStream GetContent()
		{
			if (body != null)
			{
				return body;
			}
			else
			{
				Database db = revision.GetDatabase();
				Couchbase.Lite.Attachment attachment = db.GetAttachmentForSequence(revision.GetSequence
					(), this.name);
				body = attachment.GetContent();
				return body;
			}
		}

		/// <summary>Get the length in bytes of the contents.</summary>
		/// <remarks>Get the length in bytes of the contents.</remarks>
		[InterfaceAudience.Public]
		public virtual long GetLength()
		{
			long length = (long)metadata.Get("length");
			if (length != null)
			{
				return length;
			}
			else
			{
				return 0;
			}
		}

		/// <summary>The CouchbaseLite metadata about the attachment, that lives in the document.
		/// 	</summary>
		/// <remarks>The CouchbaseLite metadata about the attachment, that lives in the document.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual IDictionary<string, object> GetMetadata()
		{
			return Sharpen.Collections.UnmodifiableMap(metadata);
		}

		[InterfaceAudience.Private]
		internal virtual void SetName(string name)
		{
			this.name = name;
		}

		[InterfaceAudience.Private]
		internal virtual void SetRevision(Revision revision)
		{
			this.revision = revision;
		}

		[InterfaceAudience.Private]
		internal virtual InputStream GetBodyIfNew()
		{
			return body;
		}

		/// <summary>
		/// Goes through an _attachments dictionary and replaces any values that are Attachment objects
		/// with proper JSON metadata dicts.
		/// </summary>
		/// <remarks>
		/// Goes through an _attachments dictionary and replaces any values that are Attachment objects
		/// with proper JSON metadata dicts. It registers the attachment bodies with the blob store and sets
		/// the metadata 'digest' and 'follows' properties accordingly.
		/// </remarks>
		[InterfaceAudience.Private]
		internal static IDictionary<string, object> InstallAttachmentBodies(IDictionary<string
			, object> attachments, Database database)
		{
			IDictionary<string, object> updatedAttachments = new Dictionary<string, object>();
			foreach (string name in attachments.Keys)
			{
				object value = attachments.Get(name);
				if (value is Couchbase.Lite.Attachment)
				{
					Couchbase.Lite.Attachment attachment = (Couchbase.Lite.Attachment)value;
					IDictionary<string, object> metadataMutable = new Dictionary<string, object>();
					metadataMutable.PutAll(attachment.GetMetadata());
					InputStream body = attachment.GetBodyIfNew();
					if (body != null)
					{
						// Copy attachment body into the database's blob store:
						BlobStoreWriter writer = BlobStoreWriterForBody(body, database);
						metadataMutable.Put("length", (long)writer.GetLength());
						metadataMutable.Put("digest", writer.MD5DigestString());
						metadataMutable.Put("follows", true);
						database.RememberAttachmentWriter(writer);
					}
					updatedAttachments.Put(name, metadataMutable);
				}
				else
				{
					if (value is AttachmentInternal)
					{
						throw new ArgumentException("AttachmentInternal objects not expected here.  Could indicate a bug"
							);
					}
				}
			}
			return updatedAttachments;
		}

		[InterfaceAudience.Private]
		internal static BlobStoreWriter BlobStoreWriterForBody(InputStream body, Database
			 database)
		{
			BlobStoreWriter writer = database.GetAttachmentWriter();
			writer.Read(body);
			writer.Finish();
			return writer;
		}

		[InterfaceAudience.Private]
		public virtual bool GetGZipped()
		{
			return gzipped;
		}

		[InterfaceAudience.Private]
		public virtual void SetGZipped(bool gzipped)
		{
			this.gzipped = gzipped;
		}
	}
}
