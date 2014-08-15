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
using System.Net;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System;
using System.Linq;
using System.Net.Http;
using Couchbase.Lite.Replication;
using System.Threading.Tasks;

namespace Couchbase.Lite.Replicator
{

    internal class BulkDownloader : RemoteRequest, IMultipartReaderDelegate
    {
        const string Tag = "BulkDownloader";

        private Database _db;

        private MultipartReader _topReader;

        private MultipartDocumentReader _docReader;

        private int _docCount;

        private BulkDownloaderDelegate _onDocument;

        /// <exception cref="System.Exception"></exception>
        public BulkDownloader(TaskFactory workExecutor, IHttpClientFactory clientFactory, Uri dbURL, IList<RevisionInternal> revs, Database database, IDictionary<string, object> requestHeaders)            
            : base(workExecutor, clientFactory, "POST", new Uri(BuildRelativeURLString(dbURL, "/_bulk_get?revs=true&attachments=true")), HelperMethod(revs, database), database, requestHeaders)
        {
            _db = database;
        }

        public void AppendToPart (IEnumerable<byte> data)
        {
            throw new System.NotImplementedException ();
        }

        public void Run()
        {
            var httpClient = clientFactory.GetHttpClient();
            PreemptivelySetAuthCredentials(httpClient);
            request.Headers.Add("Content-Type", "application/json");
            request.Headers.Add("Accept", "multipart/related");
            //TODO: implement gzip support for server response see issue #172
            //request.addHeader("X-Accept-Part-Encoding", "gzip");
            AddRequestHeaders(request);
            SetBody(request);
            ExecuteRequest(httpClient, request);
        }

        private string Description()
        {
            return this.GetType().FullName + "[" + url.AbsolutePath + "]";
        }

        protected override internal void ExecuteRequest(HttpClient httpClient, HttpRequestMessage request)
        {
            object fullBody = null;
            Exception error = null;
            HttpWebResponse response = null;
            try
            {
                if (request.IsAborted)
                {
                    RespondWithResult(fullBody, new Exception(string.Format("%s: Request %s has been aborted"
                        , this, request)), response);
                    return;
                }
                response = httpClient.Execute(request);
                try
                {
                    // add in cookies to global store
                    if (httpClient is DefaultHttpClient)
                    {
                        DefaultHttpClient defaultHttpClient = (DefaultHttpClient)httpClient;
                        clientFactory.AddCookies(defaultHttpClient.GetCookieStore().GetCookies());
                    }
                }
                catch (Exception e)
                {
                    Log.E(Log.TagRemoteRequest, "Unable to add in cookies to global store", e);
                }
                StatusLine status = response.GetStatusLine();
                if (status.GetStatusCode() >= 300)
                {
                    Log.E(Log.TagRemoteRequest, "Got error status: %d for %s.  Reason: %s", status.GetStatusCode
                        (), request, status.GetReasonPhrase());
                    error = new HttpResponseException(status.GetStatusCode(), status.GetReasonPhrase(
                    ));
                }
                else
                {
                    var entity = response.Result;
                    var contentTypeHeader = entity.ContentType;
                    InputStream inputStream = null;
                    if (contentTypeHeader != null && contentTypeHeader.GetValue().Contains("multipart/"
                    ))
                    {
                        Log.V(Tag, "contentTypeHeader = %s", contentTypeHeader.GetValue());
                        try
                        {
                            _topReader = new MultipartReader(contentTypeHeader.GetValue(), this);
                            inputStream = entity.GetContent();
                            int bufLen = 1024;
                            byte[] buffer = new byte[bufLen];
                            int numBytesRead = 0;
                            while ((numBytesRead = inputStream.Read(buffer)) != -1)
                            {
                                if (numBytesRead != bufLen)
                                {
                                    byte[] bufferToAppend = Arrays.CopyOfRange(buffer, 0, numBytesRead);
                                    _topReader.AppendData(bufferToAppend);
                                }
                                else
                                {
                                    _topReader.AppendData(buffer);
                                }
                            }
                            _topReader.Finished();
                            RespondWithResult(fullBody, error, response);
                        }
                        finally
                        {
                            try
                            {
                                inputStream.Close();
                            }
                            catch (IOException)
                            {
                            }
                        }
                    }
                    else
                    {
                        Log.V(Tag, "contentTypeHeader is not multipart = %s", contentTypeHeader.GetValue
                            ());
                        if (entity != null)
                        {
                            try
                            {
                                inputStream = entity.Content;
                                fullBody = Manager.GetObjectMapper().ReadValue<object>(inputStream);
                                RespondWithResult(fullBody, error, response);
                            }
                            finally
                            {
                                try
                                {
                                    inputStream.Close();
                                }
                                catch (IOException)
                                {
                                }
                            }
                        }
                    }
                }
            }
            catch (IOException e)
            {
                Log.E(Log.TagRemoteRequest, "io exception", e);
                error = e;
                RespondWithResult(fullBody, e, response);
            }
            catch (Exception e)
            {
                Log.E(Log.TagRemoteRequest, "%s: executeRequest() Exception: ", e, this);
                error = e;
                RespondWithResult(fullBody, e, response);
            }
        }

        /// <summary>This method is called when a part's headers have been parsed, before its data is parsed.
        ///     </summary>
        /// <remarks>This method is called when a part's headers have been parsed, before its data is parsed.
        ///     </remarks>
        public void StartedPart(IDictionary<string, string> headers)
        {
            if (_docReader != null)
            {
                throw new InvalidOperationException("_docReader is already defined");
            }
            Log.V(Tag, "{0}: Starting new document; headers =%s", this, headers);
            Log.V(Tag, "{0}: Starting new document; ID={1}".Fmt(this, headers.Get("X-Doc-Id")));
            _docReader = new MultipartDocumentReader(_db);
            _docReader.SetContentType((string)headers.Get("Content-Type"));
            _docReader.StartedPart(headers);
        }

        /// <summary>This method is called to append data to a part's body.</summary>
        /// <remarks>This method is called to append data to a part's body.</remarks>
        public virtual void AppendToPart(byte[] data)
        {
            if (_docReader == null)
            {
                throw new InvalidOperationException("_docReader is not defined");
            }
            _docReader.AppendData(data);
        }

        /// <summary>This method is called when a part is complete.</summary>
        /// <remarks>This method is called when a part is complete.</remarks>
        public virtual void FinishedPart()
        {
            Log.V(Tag, "{0}: Finished document".Fmt(this));
            if (_docReader == null)
            {
                throw new InvalidOperationException("_docReader is not defined");
            }
            _docReader.Finish();
            ++_docCount;
            _onDocument(_docReader.GetDocumentProperties());
            _docReader = null;
        }

        private static IDictionary<string, object> HelperMethod(IList<RevisionInternal> revs, Database database)
        {
            Func<RevisionInternal, IDictionary<String, Object>> invoke = source =>
            {
                const bool hasAttachment = false;
                var attsSince = database.GetPossibleAncestorRevisionIDs(source, Puller.MaxNumberOfAttsSince, hasAttachment);

                if (!hasAttachment.Get () || attsSince.Count == 0) 
                {
                    attsSince = null;
                }

                var mapped = new Dictionary<string, object> ();
                mapped.Put ("id", source.GetDocId ());
                mapped.Put ("rev", source.GetRevId ());
                mapped.Put ("atts_since", attsSince);

                return mapped;
            };

                // Build up a JSON body describing what revisions we want:
            var keys = revs.Select(invoke);       
            
            var retval = new Dictionary<string, object>();
            retval.Put("docs", keys);
            return retval;
        }

        private static string BuildRelativeURLString(Uri remote, string relativePath)
        {
            // the following code is a band-aid for a system problem in the codebase
            // where it is appending "relative paths" that start with a slash, eg:
            //     http://dotcom/db/ + /relpart == http://dotcom/db/relpart
            // which is not compatible with the way the java url concatonation works.
            var remoteUrlString = remote.AbsolutePath;
            if (remoteUrlString.EndsWith ("/", StringComparison.Ordinal) 
                && relativePath.StartsWith ("/", StringComparison.Ordinal))
            {
                remoteUrlString = remoteUrlString.Substring(0, remoteUrlString.Length - 1);
            }
            return remoteUrlString + relativePath;
        }
    }
}
