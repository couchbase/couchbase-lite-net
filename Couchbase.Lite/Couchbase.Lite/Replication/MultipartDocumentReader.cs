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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System.Net.Http;
using System.Linq;

namespace Couchbase.Lite.Support
{
    internal class MultipartDocumentReader : IMultipartReaderDelegate
	{
		/// <summary>The response which contains the input stream we need to read from</summary>
        private HttpResponseMessage response;

		private MultipartReader multipartReader;

		private BlobStoreWriter curAttachment;

        private List<Byte> jsonBuffer;

		private IDictionary<String, Object> document;

		private Database database;

		private IDictionary<String, BlobStoreWriter> attachmentsByName;

		private IDictionary<String, BlobStoreWriter> attachmentsByMd5Digest;

        public MultipartDocumentReader(HttpResponseMessage response, Database database)
		{
			this.response = response;
			this.database = database;
		}

		public IDictionary<String, Object> GetDocumentProperties()
		{
			return document;
		}

		public void ParseJsonBuffer()
		{
			try
			{
                document = Manager.GetObjectMapper().ReadValue<IDictionary<String, Object>>(jsonBuffer.ToArray());
			}
			catch (IOException e)
			{
				throw new InvalidOperationException("Failed to parse json buffer", e);
			}
			jsonBuffer = null;
		}

		public void SetContentType(String contentType)
		{
            if (!contentType.StartsWith ("multipart/", StringComparison.InvariantCultureIgnoreCase))
			{
				throw new ArgumentException("contentType must start with multipart/");
			}
            multipartReader = new MultipartReader(contentType, this);
            attachmentsByName = new Dictionary<String, BlobStoreWriter>();
            attachmentsByMd5Digest = new Dictionary<String, BlobStoreWriter>();
		}

		public void AppendData(byte[] data)
		{
			if (multipartReader != null)
			{
				multipartReader.AppendData(data);
			}
			else
			{
                jsonBuffer.AddRange(data);
			}
		}

		public void Finish()
		{
			if (multipartReader != null)
			{
				if (!multipartReader.Finished())
				{
					throw new InvalidOperationException("received incomplete MIME multipart response");
				}
				RegisterAttachments();
			}
			else
			{
				ParseJsonBuffer();
			}
		}

		private void RegisterAttachments()
		{
            var numAttachmentsInDoc = 0;
            var attachments = (IDictionary<String, Object>)document.Get("_attachments");

			if (attachments == null)
			{
				return;
			}

            foreach (var attachmentName in attachments.Keys)
			{
                var attachment = (IDictionary<String, Object>)attachments.Get(attachmentName);

				int length = 0;
				if (attachment.ContainsKey("length"))
				{
					length = ((int)attachment.Get("length"));
				}
				if (attachment.ContainsKey("encoded_length"))
				{
					length = ((int)attachment.Get("encoded_length"));
				}
				if (attachment.ContainsKey ("follows") && (bool)attachment.Get ("follows"))
				{
					// Check that each attachment in the JSON corresponds to an attachment MIME body.
					// Look up the attachment by either its MIME Content-Disposition header or MD5 digest:
                    var digest = (String)attachment.Get("digest");

                    var writer = attachmentsByName.Get(attachmentName);
					if (writer != null)
					{
						// Identified the MIME body by the filename in its Disposition header:
                        var actualDigest = writer.MD5DigestString();
						if (digest != null && !digest.Equals(actualDigest) && !digest.Equals(writer.SHA1DigestString()))
						{
                            var errMsg = String.Format("Attachment '{0}' has incorrect MD5 digest ({1}; should be {2})", attachmentName, digest, actualDigest);
							throw new InvalidOperationException(errMsg);
						}
						attachment["digest"] = actualDigest;
					}
					else
					{
						if (digest != null)
						{
							writer = attachmentsByMd5Digest.Get(digest);
							if (writer == null)
							{
                                var errMsg = String.Format("Attachment '{0}' does not appear in MIME body ", attachmentName);
								throw new InvalidOperationException(errMsg);
							}
						}
						else
						{
							if (attachments.Count == 1 && attachmentsByMd5Digest.Count == 1)
							{
								// Else there's only one attachment, so just assume it matches & use it:
                                writer = attachmentsByMd5Digest.Values.First();
								attachment.Put("digest", writer.MD5DigestString());
							}
							else
							{
								// No digest metatata, no filename in MIME body; give up:
                                var errMsg = String.Format("Attachment '{0}' has no digest metadata; cannot identify MIME body", attachmentName);
								throw new InvalidOperationException(errMsg);
							}
						}
					}

					// Check that the length matches:
					if (writer.GetLength() != length)
					{
                        var errMsg = String.Format("Attachment '{0}' has incorrect length field {1} (should be {2})", attachmentName, length, writer.GetLength());
						throw new InvalidOperationException(errMsg);
					}

					++numAttachmentsInDoc;
				}
				else
				{
					if (attachment.ContainsKey("data") && length > 1000)
					{
                        var msg = String.Format("Attachment '{0}' sent inline (len={1}).  Large attachments "
							 + "should be sent in MIME parts for reduced memory overhead.", attachmentName);
						Log.W(Database.Tag, msg);
					}
				}
			}
			if (numAttachmentsInDoc < attachmentsByMd5Digest.Count)
			{
                var msg = String.Format("More MIME bodies ({0}) than attachments ({1}) ", attachmentsByMd5Digest.Count, numAttachmentsInDoc);
				throw new InvalidOperationException(msg);
			}
			// hand over the (uninstalled) blobs to the database to remember:
			database.RememberAttachmentWritersForDigests(attachmentsByMd5Digest);
		}

		public void StartedPart(IDictionary<String, String> headers)
		{
			if (document == null)
			{
                jsonBuffer = new List<Byte>(1024);
			}
			else
			{
				curAttachment = database.GetAttachmentWriter();
                var contentDisposition = headers.Get("Content-Disposition");
				if (contentDisposition != null && contentDisposition.StartsWith("attachment; filename="))
				{
					// TODO: Parse this less simplistically. Right now it assumes it's in exactly the same
					// format generated by -[CBL_Pusher uploadMultipartRevision:]. CouchDB (as of 1.2) doesn't
					// output any headers at all on attachments so there's no compatibility issue yet.
                    var contentDispositionUnquoted = Misc.UnquoteString(contentDisposition);
                    var name = contentDispositionUnquoted.Substring(21);
					if (name != null)
					{
						attachmentsByName[name] = curAttachment;
					}
				}
			}
		}

        public void AppendToPart(IEnumerable<Byte> data)
		{
			if (jsonBuffer != null)
			{
                jsonBuffer.AddRange(data);
			}
			else
			{
                curAttachment.AppendData(data.ToArray());
			}
		}

		public void FinishedPart()
		{
			if (jsonBuffer != null)
			{
				ParseJsonBuffer();
			}
			else
			{
				curAttachment.Finish();
				String md5String = curAttachment.MD5DigestString();
				attachmentsByMd5Digest[md5String] = curAttachment;
				curAttachment = null;
			}
		}
	}
}
