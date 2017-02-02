//
// MultipartDocumentReader.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Couchbase.Lite;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Support
{
    internal class MultipartDocumentReader : IMultipartReaderDelegate
    {
        private const string TAG = "MultipartDocumentReader";

        private MultipartReader multipartReader;

        private BlobStoreWriter curAttachment;

        private List<Byte> jsonBuffer;

        private bool _jsonCompressed;

        private IDictionary<String, Object> document;

        private Database database;

        private IDictionary<String, BlobStoreWriter> attachmentsByName;

        private IDictionary<String, BlobStoreWriter> attachmentsByDigest;

        public MultipartDocumentReader(Database database)
        {
            this.database = database;
        }

        // Convenience method for testing
        internal static IDictionary<string, object> ReadToDatabase(IEnumerable<byte> data, IDictionary<string, string> headers, 
            Database db)
        {
            var realized = data.ToArray();
            if (realized.Length == 0) {
                throw new CouchbaseLiteException(StatusCode.BadJson);
            }

            var reader = new MultipartDocumentReader(db);
            reader.SetHeaders(headers);
            reader.AppendData(data);
            reader.Finish();

            return reader.document;
        }

        public IDictionary<String, Object> GetDocumentProperties()
        {
            return document;
        }

        public void SetHeaders(IDictionary<string, string> headers)
        {
            var contentType = headers.Get("Content-Type");
            if (contentType != null && contentType.StartsWith("multipart/")) {
                // Multipart, so initialize the parser:
                Log.To.Sync.V(TAG, "{0} has attachments, {1}", this, contentType);
                try {
                    multipartReader = new MultipartReader(contentType, this);
                } catch (ArgumentException e) {
                    throw Misc.CreateExceptionAndLog(Log.To.Sync, e, StatusCode.NotAcceptable, TAG,
                        "Unable to create MultipartReader");
                }

                attachmentsByName = new Dictionary<string, BlobStoreWriter>();
                attachmentsByDigest = new Dictionary<string, BlobStoreWriter>();
                return;
            } else if (contentType == null || contentType.StartsWith("application/json") ||
                contentType.StartsWith("text/plain")) {
                // No multipart, so no attachments. Body is pure JSON. (We allow text/plain because CouchDB
                // sends JSON responses using the wrong content-type.)
                StartJsonBuffer(headers);
                return;
            }

            Log.To.Sync.E(TAG, "Invalid Content-Type header ({0}) received in SetHeaders, throwing...", contentType);
            throw new ArgumentException("Invalid Content-Type header", "headers");
        }

        public void ParseJsonBuffer()
        {
            try {
                var json = jsonBuffer;
                jsonBuffer = null;
                if(_jsonCompressed) {
                    json = json.Decompress().ToList();
                    if(json == null) {
                        throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.UpStreamError, TAG,
                            "Received corrupt gzip encoded JSON part");
                    }
                }
                document = Manager.GetObjectMapper().ReadValue<IDictionary<String, Object>>(json.ToArray());
            } catch(CouchbaseLiteException) {
                Log.To.Sync.W(TAG, "Failed to parse multipart JSON, rethrowing...");
                throw;
            } catch (Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Sync, e, StatusCode.BadJson, TAG,
                    "Failed to parse json buffer");
            } 
        }

        public void SetContentType(String contentType)
        {
            if (contentType == null 
                || contentType.StartsWith("application/json", StringComparison.Ordinal)
                || contentType.StartsWith("text/plain", StringComparison.Ordinal)) {
                // No multipart, so no attachments. Body is pure JSON. (We allow text/plain because CouchDB
                // sends JSON responses using the wrong content-type.)
                jsonBuffer = new List<byte>();
            } else if (contentType.StartsWith ("multipart/", StringComparison.InvariantCultureIgnoreCase)) {
                multipartReader = new MultipartReader(contentType, this);
                attachmentsByName = new Dictionary<String, BlobStoreWriter>();
                attachmentsByDigest = new Dictionary<String, BlobStoreWriter>();
            }  else {
                Log.To.Sync.E(TAG, "Invalid contentType in SetContentType ({0}); does not start with multipart/, throwing...",
                    contentType);
                throw new ArgumentException("Does not start with multipart/", "contentType");
            }
        }

        public void AppendData(IEnumerable<byte> data)
        {
            if (multipartReader != null) {
                multipartReader.AppendData(data);
            } else {
                jsonBuffer.AddRange(data);
            }
        }

        public void Finish()
        {
            Log.To.Sync.V(TAG, "{0} finished loading ({1} attachments)", 
                this, attachmentsByDigest == null ? 0 : attachmentsByDigest.Count);
            if (multipartReader != null) {
                if (!multipartReader.Finished) {
                    throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.UpStreamError, TAG,
                        "{0} received incomplete MIME response", this);
                }

                RegisterAttachments();
            } else {
                ParseJsonBuffer();
            }
        }

        private void RegisterAttachments()
        {
            var attachmentsBoxed = document.Get("_attachments");
            if(attachmentsBoxed == null) {
                return;
            }

            var attachments = attachmentsBoxed.AsDictionary<string, object>();
            if (attachments == null) {
                throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.AttachmentError, TAG, "_attachments property is not a dictionary");
            }

            var numAttachmentsInDoc = 0;
            var nuAttachments = new Dictionary<string, object>(attachments.Count);
            foreach (var attmt in attachments) {
                var attachmentName = attmt.Key;
                var attachment = attmt.Value.AsDictionary<string, object>();


                long length = attachment.GetCast<long>("encoded_length", -1L);
                if(length == -1) {
                    length = attachment.GetCast<long>("length", -1L);
                }

                if(length == -1) {
                    throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.AttachmentError, TAG, $"Attachment '{attachmentName}' has invalid length property");
                }

                if (attachment.GetCast<bool>("follows")) {
                    // Check that each attachment in the JSON corresponds to an attachment MIME body.
                    // Look up the attachment by either its MIME Content-Disposition header or digest
                    var digest = attachment.GetCast<string>("digest");
                    var writer = attachmentsByName.Get(attachmentName);
                    if (writer != null) {
                        // Identified the MIME body by the filename in its Disposition header:
                        var actualDigest = writer.SHA1DigestString();
                        if (digest != null && !digest.Equals(actualDigest) && !digest.Equals(writer.MD5DigestString())) {
                            throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.AttachmentError, TAG, "Attachment '{0}' has incorrect digest ({1}; should be either {2} or {3})",
                                new SecureLogString(attachmentName, LogMessageSensitivity.PotentiallyInsecure),
                                digest, actualDigest, writer.MD5DigestString());
                        }

                        attachment["digest"] = actualDigest;
                    } else if(digest != null) {
                        writer = attachmentsByDigest.Get(digest);
                        if(writer == null) {
                            throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.AttachmentError, TAG, "Attachment '{0}' does not appear in MIME body ",
                                new SecureLogString(attachmentName, LogMessageSensitivity.PotentiallyInsecure));
                        }

                    } else if(attachments.Count == 1 && attachmentsByDigest.Count == 1) {
                        // Else there's only one attachment, so just assume it matches & use it:
                        writer = attachmentsByDigest.Values.First();
                        attachment["digest"] = writer.SHA1DigestString();
                    } else {
                        // No digest metatata, no filename in MIME body; give up:
                        throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.AttachmentError, TAG, "Attachment '{0}' has no digest metadata; cannot identify MIME body",
                            new SecureLogString(attachmentName, LogMessageSensitivity.PotentiallyInsecure));
                    }

                    // Check that the length matches:
                    if (writer.GetLength() != length) {
                        throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.AttachmentError, TAG, "Attachment '{0}' has incorrect length field {1} (should be {2})",
                            new SecureLogString(attachmentName, LogMessageSensitivity.PotentiallyInsecure),
                            length, writer.GetLength());
                    }
                    
                    ++numAttachmentsInDoc;
                } else if (attachment.Get("data") != null && length > 1000) {
                    // This isn't harmful but it's quite inefficient of the server
                    var msg = String.Format("Attachment '{0}' sent inline (len={1}).  Large attachments "
                                + "should be sent in MIME parts for reduced memory overhead.", attachmentName, length);
                    Log.To.Sync.W(TAG, msg);
                }

                nuAttachments[attachmentName] = attachment;
            }

            if (numAttachmentsInDoc < attachmentsByDigest.Count) {
                throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.AttachmentError, TAG, "More MIME bodies ({0}) than attachments ({1}) ", attachmentsByDigest.Count, numAttachmentsInDoc);
            }

            document["_attachments"] = nuAttachments;
            // If everything's copacetic, hand over the (uninstalled) blobs to the database to remember:
            database.RememberAttachmentWritersForDigests(attachmentsByDigest);
        }

        public void StartedPart(IDictionary<String, String> headers)
        {
            if (document == null) {
                StartJsonBuffer(headers);
            } else {
                Log.To.Sync.V(TAG, "{0} starting attachment #{1}...", this, attachmentsByDigest.Count + 1);
                curAttachment = database.AttachmentWriter;
                if (curAttachment == null) {
                    throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.AttachmentError, TAG,
                        "Cannot create blob store writer for the attachment");
                }

                var name = default(string);
                var contentDisposition = headers.Get("Content-Disposition");
                if (contentDisposition != null && contentDisposition.StartsWith("attachment; filename=")) {
                    // TODO: Parse this less simplistically. Right now it assumes it's in exactly the same
                    // format generated by -[CBL_Pusher uploadMultipartRevision:]. CouchDB (as of 1.2) doesn't
                    // output any headers at all on attachments so there's no compatibility issue yet.
                    var contentDispositionUnquoted = Misc.UnquoteString(contentDisposition);
                    name = contentDispositionUnquoted.Substring(21);
                    if (name != null) {
                        attachmentsByName[name] = curAttachment;
                    }
                }

                var contentEncoding = headers.Get("Content-Encoding");
                if (contentEncoding == "gzip") {
                    if (name != null) {
                        try {
                            var attachEncoding = document.GetCast<IDictionary<string, object>>("_attachments").
                            GetCast <IDictionary<string, object>>(name).GetCast<string>("encoding");
                            if (attachEncoding != "gzip") {
                                throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.UnsupportedType, TAG,
                                    "Attachment '{0}' MIME body is gzipped but attachment isn't",
                                    new SecureLogString(name, LogMessageSensitivity.PotentiallyInsecure));
                            }
                        } catch (NullReferenceException) {
                            throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.UnsupportedType, TAG,
                                "NullReferenceException caught; _attachments was not present or encoding was " +
                                "not present in _attachments");
                        }
                    }
                } else if (contentEncoding != null) {
                    throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.UnsupportedType, TAG,
                        "Received unsupported Content-Encoding '{0}'", contentEncoding);
                }
            }
        }

        public void AppendToPart(IEnumerable<Byte> data)
        {
            if (jsonBuffer != null) {
                jsonBuffer.AddRange(data);
            } else if (curAttachment != null) {
                curAttachment.AppendData(data.ToArray());
            }
        }

        public void FinishedPart()
        {
            if (jsonBuffer != null) {
                ParseJsonBuffer();
            } else {
                curAttachment.Finish();
                var sha1String = curAttachment.SHA1DigestString();
                Log.To.Sync.V(TAG, "{0} finished attachment #{1}: {2}", this, attachmentsByDigest.Count + 1, curAttachment);
                attachmentsByDigest[sha1String] = curAttachment;
                curAttachment = null;
            }
        }

        private void StartJsonBuffer(IDictionary<string, string> headers)
        {
            jsonBuffer = new List<Byte>(1024);
            var contentEncoding = headers.Get("Content-Encoding");
            _jsonCompressed = contentEncoding != null && contentEncoding.Contains("gzip");
        }
    }
}