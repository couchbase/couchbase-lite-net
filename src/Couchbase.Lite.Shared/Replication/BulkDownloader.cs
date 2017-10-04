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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Couchbase.Lite.Revisions;

namespace Couchbase.Lite.Replicator
{
    internal sealed class BulkDownloaderOptions : ConstructorOptions
    {
        [RequiredProperty]
        public RemoteSession Session { get; set; }

        [RequiredProperty]
        public Uri DatabaseUri { get; set; }

        [RequiredProperty]
        public IList<RevisionInternal> Revisions { get; set; }

        [RequiredProperty]
        public Database Database { get; set; }

        [RequiredProperty(CreateDefault=true, ConcreteType=typeof(Dictionary<string, object>))]
        public IDictionary<string, object> RequestHeaders { get; set; }

        [RequiredProperty]
        public IRetryStrategy RetryStrategy { get; set; }

        [RequiredProperty(CreateDefault=true)]
        public CancellationTokenSource TokenSource { get; set; }

        [RequiredProperty]
        public CookieStore CookieStore { get; set; }
    }

    internal class BulkDownloader : IMultipartReaderDelegate
    {
        internal static readonly string Tag = typeof(BulkDownloader).Name;

        private Uri _bulkGetUri;
        private IDictionary<string, object> _requestHeaders;
        private MultipartReader _topReader;
        private CancellationTokenSource _tokenSource;
        private MultipartDocumentReader _docReader;
        private Database _db;
        private readonly RemoteSession _session;
        private readonly object _body;

        private int _docCount;

        public event EventHandler<BulkDownloadEventArgs> DocumentDownloaded
        {
            add { _documentDownloaded = (EventHandler<BulkDownloadEventArgs>)Delegate.Combine(_documentDownloaded, value); }
            remove { _documentDownloaded = (EventHandler<BulkDownloadEventArgs>)Delegate.Remove(_documentDownloaded, value); }
        }
        private EventHandler<BulkDownloadEventArgs> _documentDownloaded;

        public event EventHandler<RemoteRequestEventArgs> Complete
        {
            add { _complete = (EventHandler<RemoteRequestEventArgs>)Delegate.Combine(_complete, value); }
            remove { _complete = (EventHandler<RemoteRequestEventArgs>)Delegate.Remove(_complete, value); }
        }
        private EventHandler<RemoteRequestEventArgs> _complete;

        internal IAuthenticator Authenticator { get; set; }

        /// <exception cref="System.Exception"></exception>
        public BulkDownloader(BulkDownloaderOptions options)
        {
            options.Validate();
            _bulkGetUri = new Uri(AppendRelativeURLString(options.DatabaseUri, "/_bulk_get?revs=true&attachments=true"));
            _db = options.Database;
            
            _requestHeaders = options.RequestHeaders;
            _tokenSource = options.TokenSource ?? new CancellationTokenSource();
            _body = CreatePostBody(options.Revisions, _db);
            _session = options.Session;
        }

        public void Start()
        {
            var requestMessage = CreateConcreteRequest();

            if(!requestMessage.Headers.Contains("User-Agent")) {
                requestMessage.Headers.TryAddWithoutValidation("User-Agent", String.Format("CouchbaseLite/{0} ({1})", Replication.SyncProtocolVersion, Manager.VersionString));
            }

            requestMessage.Headers.Add("Accept", "multipart/related, application/json");           
            foreach (string requestHeaderKey in _requestHeaders.Keys) {
                
                requestMessage.Headers.TryAddWithoutValidation(requestHeaderKey, _requestHeaders[requestHeaderKey].ToString());
            }

            SetBody(requestMessage);

            ExecuteRequest(requestMessage).ContinueWith(t => 
            {
                Log.To.Sync.V(Tag, "RemoteRequest run() finished, url: {0}", _bulkGetUri);
            });
        }

        private void SetBody(HttpRequestMessage request)
        {
            // set body if appropriate
            if (_body != null)  {
                byte[] bodyBytes = null;
                try {
                    bodyBytes = Manager.GetObjectMapper().WriteValueAsBytes(_body).ToArray();
                } catch (Exception e) {
                    Log.To.Sync.E(Tag, "Error serializing body of request", e);
                }

                HttpContent entity = new ByteArrayContent(bodyBytes);
                entity.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                if (bodyBytes.Length > 100) {
                    entity = new CompressedContent(entity, "gzip");
                }

                request.Content = entity;

            } else {
                Log.To.Sync.W(Tag, "No body found for this request to {0}", request.RequestUri);
            }
        }

        private HttpRequestMessage CreateConcreteRequest()
        {
            var httpMethod = HttpMethod.Post;
            var newRequest = new HttpRequestMessage(httpMethod, _bulkGetUri.AbsoluteUri);
            return newRequest;
        }

        private Task ExecuteRequest(HttpRequestMessage request)
        {
            object fullBody = null;
            Exception error = null;
            HttpResponseMessage response = null;
            if (_tokenSource.IsCancellationRequested) {
                RespondWithResult(fullBody, new Exception(string.Format("{0}: Request {1} has been aborted", this, request)), response);
                var tcs = new TaskCompletionSource<bool>();
                tcs.SetCanceled();
                return tcs.Task;
            }
                
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            request.Headers.Add("X-Accept-Part-Encoding", "gzip");

            Log.To.Sync.V(Tag, "Sending request: {0}", request);
            var requestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_tokenSource.Token);
            
            return _session.SendAsyncRequest(request, HttpCompletionOption.ResponseContentRead, requestTokenSource.Token).ContinueWith(t =>
            {
                requestTokenSource.Dispose();
                try {
                    response = t.Result;
                } catch(Exception e) {
                    var err = Misc.Flatten(e).First();
                    Log.To.Sync.E(Tag, "Unhandled exception while getting bulk documents", err);
                    error = err;
                    RespondWithResult(fullBody, err, response);
                    return;
                }

                try {
                    if (response == null) {
                        Log.To.Sync.I(Tag, "Didn't get response for {0}", request);

                        error = new HttpRequestException();
                        RespondWithResult(fullBody, error, response);
                    } else if (!response.IsSuccessStatusCode)  {
                        HttpStatusCode status = response.StatusCode;

                        Log.To.Sync.I(Tag, "Got error status: {0} for {1}.  Reason: {2}", status.GetStatusCode(), request, response.ReasonPhrase);
                        error = new HttpResponseException(status);

                        RespondWithResult(fullBody, error, response);
                    } else {
                        Log.To.Sync.D(Tag, "Processing response: {0}", response);
                        var entity = response.Content;
                        var contentTypeHeader = entity.Headers.ContentType;
                        Stream inputStream = null;
                        if (contentTypeHeader != null && contentTypeHeader.ToString().Contains("multipart/"))
                        {
                            Log.To.Sync.D(Tag, "contentTypeHeader = {0}", contentTypeHeader.ToString());
                            try {
                                _topReader = new MultipartReader(contentTypeHeader.ToString(), this);
                                inputStream = entity.ReadAsStreamAsync().Result;
                                const int bufLen = 1024;
                                var buffer = new byte[bufLen];
                                var numBytesRead = 0;
                                while ((numBytesRead = inputStream.Read(buffer, 0, bufLen)) > 0) {
                                    if (numBytesRead != bufLen) {
                                        var bufferToAppend = new Couchbase.Lite.Util.ArraySegment<byte>(buffer, 0, numBytesRead).ToArray();
                                        _topReader.AppendData(bufferToAppend);
                                    } else {
                                        _topReader.AppendData(buffer);
                                    }
                                }

                                RespondWithResult(fullBody, error, response);
                            } finally {
                                try { 
                                    inputStream.Close();
                                } catch (IOException) { }
                            }
                        } else {
                            Log.To.Sync.D(Tag, "contentTypeHeader is not multipart = {0}", contentTypeHeader.ToString());
                            if (entity != null) {
                                try {
                                    inputStream = entity.ReadAsStreamAsync().Result;
                                    fullBody = Manager.GetObjectMapper().ReadValue<object>(inputStream);
                                    RespondWithResult(fullBody, error, response);
                                } finally {
                                    try {
                                        inputStream.Close();
                                    } catch (IOException) {  }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    var err = (e is AggregateException) ? e.InnerException : e;
                    Log.To.Sync.E(Tag, "Exception while processing bulk download response", err);
                    error = err;
                    RespondWithResult(fullBody, err, response);
                }
            });
        }

        /// <summary>This method is called when a part's headers have been parsed, before its data is parsed.
        ///     </summary>
        /// <remarks>This method is called when a part's headers have been parsed, before its data is parsed.
        ///     </remarks>
        public void StartedPart(IDictionary<string, string> headers)
        {
            if (_docReader != null) {
                Log.To.Sync.E(Tag, "StartedPart called on an already started object");
                throw new InvalidOperationException("StartedPart called on an already started object");
            }

            Log.To.Sync.V(Tag, "{0}: Starting new document; ID={1}", this, headers.Get("X-Doc-ID"));
            _docReader = new MultipartDocumentReader(_db);
            _docReader.SetContentType(headers.Get ("Content-Type"));
            _docReader.StartedPart(headers);
        }

        /// <summary>This method is called to append data to a part's body.</summary>
        /// <remarks>This method is called to append data to a part's body.</remarks>
        public void AppendToPart (IEnumerable<byte> data)
        {
            if (_docReader == null) {
                Log.To.Sync.E(Tag, "AppendPart called on a non-started object");
                throw new InvalidOperationException("AppendPart called on a non-started object");
            }

            _docReader.AppendData(data);
        }

        /// <summary>This method is called when a part is complete.</summary>
        /// <remarks>This method is called when a part is complete.</remarks>
        public virtual void FinishedPart()
        {
            if (_docReader == null) {
                Log.To.Sync.E(Tag, "FinishedPart called on a non-started object");
                throw new InvalidOperationException("FinishedPart called on a non-started object");
            }

            Log.To.Sync.V(Tag, "{0} Finished document", this);
            _docReader.Finish();
            ++_docCount;
            OnDocumentDownloaded(_docReader.GetDocumentProperties());
            _docReader = null;
        }

        protected virtual void OnDocumentDownloaded (IDictionary<string, object> props)
        {
            var handler = _documentDownloaded;
            if (handler != null)
                handler (this, new BulkDownloadEventArgs(props));
        }

        private void RespondWithResult(object result, Exception error, HttpResponseMessage response)
        {
            Log.To.Sync.V(Tag, "{0} finished loading ({1} documents)", this, _docCount);
            OnEvent(_complete, result, error);
            if (response != null) {
                response.Dispose();
            }
        }

        private void OnEvent(EventHandler<RemoteRequestEventArgs> evt, Object result, Exception error)
        {
            if (evt == null)
                return;
            var args = new RemoteRequestEventArgs(result, error);
            evt(this, args);
        }

        private IDictionary<string, object> CreatePostBody(IEnumerable<RevisionInternal> revs, Database database)
        {
            var maxRevTreeDepth = database.GetMaxRevTreeDepth();
            Func<RevisionInternal, IDictionary<string, object>> invoke = source =>
            {
                if(!database.IsOpen) {
                    return null;
                }

                //TODO: Deferred attachments
                ValueTypePtr<bool> haveBodies = false;
                var possibleAncestors = database.Storage.GetPossibleAncestors(source, Puller.MaxAttsSince, haveBodies, true);
                
                var key = new Dictionary<string, object> {
                    ["id"] = source.DocID,
                    ["rev"] = source.RevID.ToString()
                };

                if(possibleAncestors != null) {
                    var bodyKey = haveBodies ? "atts_since" : "revs_from";
                    key[bodyKey] = possibleAncestors;
                } else {
                    if(source.Generation > maxRevTreeDepth) {
                        key["revs_limit"] = maxRevTreeDepth;
                    }
                }

                return key;
            };

            // Build up a JSON body describing what revisions we want:
            
            IEnumerable<IDictionary<string, object>> keys = null;
            try {
                keys = revs.Select(invoke).Where(x => x != null);
            } catch(Exception ex) {
                Log.To.Sync.E(Tag, "Error generating bulk request data.", ex);
            }

            var retval = new Dictionary<string, object>();
            retval["docs"] = keys;
            Log.To.Sync.V(Tag, "Created bulk download request {0}{1}Body: {2}", _bulkGetUri, Environment.NewLine,
                new LogJsonString(keys));
            return retval;
        }

        private static string AppendRelativeURLString(Uri remote, string relativePath)
        {
            var uri = remote.AppendPath(relativePath);
            return uri.AbsoluteUri;
        }

        public override string ToString()
        {
            return String.Format("BulkDownloader ({0})", new SecureLogUri(_bulkGetUri));
        }
    }
}
