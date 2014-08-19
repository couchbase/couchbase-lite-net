// 
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
//using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Apache.Http;
using Apache.Http.Util;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
    public class MultipartDocumentReader : MultipartReaderDelegate
    {
        /// <summary>The response which contains the input stream we need to read from</summary>
        private HttpResponse response;

        private MultipartReader multipartReader;

        private BlobStoreWriter curAttachment;

        private ByteArrayBuffer jsonBuffer;

        private IDictionary<string, object> document;

        private Database database;

        private IDictionary<string, BlobStoreWriter> attachmentsByName;

        private IDictionary<string, BlobStoreWriter> attachmentsByMd5Digest;

        public MultipartDocumentReader(HttpResponse response, Database database)
        {
            this.response = response;
            this.database = database;
        }

        public virtual IDictionary<string, object> GetDocumentProperties()
        {
            return document;
        }

        public virtual void ParseJsonBuffer()
        {
            try
            {
                document = Manager.GetObjectMapper().ReadValue<IDictionary>(jsonBuffer.ToByteArray
                    ());
            }
            catch (IOException e)
            {
                throw new InvalidOperationException("Failed to parse json buffer", e);
            }
            jsonBuffer = null;
        }

        public virtual void SetContentType(string contentType)
        {
            if (contentType.StartsWith("multipart/"))
            {
                multipartReader = new MultipartReader(contentType, this);
                attachmentsByName = new Dictionary<string, BlobStoreWriter>();
                attachmentsByMd5Digest = new Dictionary<string, BlobStoreWriter>();
            }
            else
            {
                if (contentType == null || contentType.StartsWith("application/json") || contentType
                    .StartsWith("text/plain"))
                {
                }
                else
                {
                    // No multipart, so no attachments. Body is pure JSON. (We allow text/plain because CouchDB
                    // sends JSON responses using the wrong content-type.)
                    throw new ArgumentException("contentType must start with multipart/");
                }
            }
        }

        public virtual void AppendData(byte[] data)
        {
            if (multipartReader != null)
            {
                multipartReader.AppendData(data);
            }
            else
            {
                jsonBuffer.Append(data, 0, data.Length);
            }
        }

        public virtual void Finish()
        {
            if (multipartReader != null)
            {
                if (!multipartReader.Finished())
                {
                    throw new InvalidOperationException("received incomplete MIME multipart response"
                        );
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
            int numAttachmentsInDoc = 0;
            IDictionary<string, object> attachments = (IDictionary<string, object>)document.Get
                ("_attachments");
            if (attachments == null)
            {
                return;
            }
            foreach (string attachmentName in attachments.Keys)
            {
                IDictionary<string, object> attachment = (IDictionary<string, object>)attachments
                    .Get(attachmentName);
                int length = 0;
                if (attachment.ContainsKey("length"))
                {
                    length = ((int)attachment.Get("length"));
                }
                if (attachment.ContainsKey("encoded_length"))
                {
                    length = ((int)attachment.Get("encoded_length"));
                }
                if (attachment.ContainsKey("follows") && ((bool)attachment.Get("follows")) == true)
                {
                    // Check that each attachment in the JSON corresponds to an attachment MIME body.
                    // Look up the attachment by either its MIME Content-Disposition header or MD5 digest:
                    string digest = (string)attachment.Get("digest");
                    BlobStoreWriter writer = attachmentsByName.Get(attachmentName);
                    if (writer != null)
                    {
                        // Identified the MIME body by the filename in its Disposition header:
                        string actualDigest = writer.MD5DigestString();
                        if (digest != null && !digest.Equals(actualDigest) && !digest.Equals(writer.SHA1DigestString
                            ()))
                        {
                            string errMsg = string.Format("Attachment '%s' has incorrect MD5 digest (%s; should be %s)"
                                , attachmentName, digest, actualDigest);
                            throw new InvalidOperationException(errMsg);
                        }
                        attachment.Put("digest", actualDigest);
                    }
                    else
                    {
                        if (digest != null)
                        {
                            writer = attachmentsByMd5Digest.Get(digest);
                            if (writer == null)
                            {
                                string errMsg = string.Format("Attachment '%s' does not appear in MIME body (%s; should be %s)"
                                    , attachmentName);
                                throw new InvalidOperationException(errMsg);
                            }
                        }
                        else
                        {
                            if (attachments.Count == 1 && attachmentsByMd5Digest.Count == 1)
                            {
                                // Else there's only one attachment, so just assume it matches & use it:
                                writer = attachmentsByMd5Digest.Values.GetEnumerator().Next();
                                attachment.Put("digest", writer.MD5DigestString());
                            }
                            else
                            {
                                // No digest metatata, no filename in MIME body; give up:
                                string errMsg = string.Format("Attachment '%s' has no digest metadata; cannot identify MIME body"
                                    , attachmentName);
                                throw new InvalidOperationException(errMsg);
                            }
                        }
                    }
                    // Check that the length matches:
                    if (writer.GetLength() != length)
                    {
                        string errMsg = string.Format("Attachment '%s' has incorrect length field %d (should be %d)"
                            , attachmentName, length, writer.GetLength());
                        throw new InvalidOperationException(errMsg);
                    }
                    ++numAttachmentsInDoc;
                }
                else
                {
                    if (attachment.ContainsKey("data") && length > 1000)
                    {
                        Log.W(Log.TagRemoteRequest, "Attachment '%s' sent inline (len=%d).  Large attachments "
                             + "should be sent in MIME parts for reduced memory overhead.", attachmentName);
                    }
                }
            }
            if (numAttachmentsInDoc < attachmentsByMd5Digest.Count)
            {
                string msg = string.Format("More MIME bodies (%d) than attachments (%d) ", attachmentsByMd5Digest
                    .Count, numAttachmentsInDoc);
                throw new InvalidOperationException(msg);
            }
            // hand over the (uninstalled) blobs to the database to remember:
            database.RememberAttachmentWritersForDigests(attachmentsByMd5Digest);
        }

        public virtual void StartedPart(IDictionary<string, string> headers)
        {
            if (document == null)
            {
                jsonBuffer = new ByteArrayBuffer(1024);
            }
            else
            {
                curAttachment = database.GetAttachmentWriter();
                string contentDisposition = headers.Get("Content-Disposition");
                if (contentDisposition != null && contentDisposition.StartsWith("attachment; filename="
                    ))
                {
                    // TODO: Parse this less simplistically. Right now it assumes it's in exactly the same
                    // format generated by -[CBL_Pusher uploadMultipartRevision:]. CouchDB (as of 1.2) doesn't
                    // output any headers at all on attachments so there's no compatibility issue yet.
                    string contentDispositionUnquoted = Misc.UnquoteString(contentDisposition);
                    string name = Sharpen.Runtime.Substring(contentDispositionUnquoted, 21);
                    if (name != null)
                    {
                        attachmentsByName.Put(name, curAttachment);
                    }
                }
            }
        }

        public virtual void AppendToPart(byte[] data)
        {
            if (jsonBuffer != null)
            {
                jsonBuffer.Append(data, 0, data.Length);
            }
            else
            {
                curAttachment.AppendData(data);
            }
        }

        public virtual void FinishedPart()
        {
            if (jsonBuffer != null)
            {
                ParseJsonBuffer();
            }
            else
            {
                curAttachment.Finish();
                string md5String = curAttachment.MD5DigestString();
                attachmentsByMd5Digest.Put(md5String, curAttachment);
                curAttachment = null;
            }
        }
    }
}
