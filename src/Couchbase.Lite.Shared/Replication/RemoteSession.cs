//
//  RemoteSession.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal
{
    internal sealed class RemoteSessionContructorOptions : ConstructorOptions
    {
        public Guid Id { get; set; }

        [RequiredProperty]
        public Uri BaseUrl { get; set; }

        public CancellationTokenSource CancellationTokenSource { get; set; }

        public TaskFactory WorkExecutor { get; set; }

        public RemoteSessionContructorOptions()
        {
            Id = Guid.NewGuid();
        }
    }

    internal sealed class RemoteSession : IDisposable
    {
        private const string Tag = nameof(RemoteSession);

        internal readonly ConcurrentDictionary<HttpRequestMessage, Task> _requests = 
            new ConcurrentDictionary<HttpRequestMessage, Task>();

        private Leasable<CouchbaseLiteHttpClient> _client;
        private CancellationTokenSource _remoteRequestCancellationSource;
        private readonly Uri _baseUrl;
        private readonly Guid _id;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly TaskFactory _workExecutor;

        public IHttpClientFactory ClientFactory { get; set; }

        public CookieStore CookieStore { get; private set; }

        public IAuthorizer Authenticator { get; set; }

        public RemoteServerVersion ServerType { get; set; }

        public IDictionary<string, object> RequestHeaders { get; set; } = new Dictionary<string, object>();

        public bool Disposed { get; private set; }

        public int RequestCount
        {
            get {
                return _requests.Count;
            }
        }

        public RemoteSession(RemoteSessionContructorOptions options)
        {
            options.Validate();
            _workExecutor = options.WorkExecutor ?? new TaskFactory(TaskScheduler.Default);
            _cancellationTokenSource = options.CancellationTokenSource == null ? new CancellationTokenSource() : CancellationTokenSource.CreateLinkedTokenSource(options.CancellationTokenSource.Token);
            _id = options.Id;
            _baseUrl = options.BaseUrl;
        }

        private RemoteSession(RemoteSession source)
        {
            _workExecutor = source._workExecutor;
            _cancellationTokenSource = source._cancellationTokenSource;
            _id = source._id;
            _baseUrl = source._baseUrl;
            Authenticator = source.Authenticator;
            RequestHeaders = source.RequestHeaders;
            ServerType = source.ServerType;
            CookieStore = source.CookieStore;
        }

        public static RemoteSession Clone(RemoteSession source)
        {
            if(!source.Disposed) {
                return source;
            }

            return new RemoteSession(source);
        }

        public void Setup(ReplicationOptions options)
        {
            _remoteRequestCancellationSource?.Cancel();
            _remoteRequestCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            ClientFactory.SocketTimeout = options.SocketTimeout;
            var clientObj = ClientFactory.GetHttpClient(CookieStore, options.RetryStrategy);
            clientObj.Timeout = options.RequestTimeout;
            clientObj.SetConcurrencyLimit(options.MaxOpenHttpConnections);
            _client = clientObj;
        }

        public void Dispose()
        {
            if(Disposed) {
                return;
            }

            _remoteRequestCancellationSource?.Cancel();
            _client.Dispose();
        }

        public void CancelRequests()
        {
            _remoteRequestCancellationSource?.Cancel();
            _remoteRequestCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
        }

        internal void SetupHttpClientFactory(IHttpClientFactory newValue, Database db, string checkpointId)
        {
            if(newValue != null) {
                ClientFactory = newValue;
            } else {
                var manager = db?.Manager;
                var managerClientFactory = manager?.DefaultHttpClientFactory;
                ClientFactory = managerClientFactory ?? new CouchbaseLiteHttpClientFactory();
            }

            CookieStore = new CookieStore(db, checkpointId);
        }

        internal Task<HttpResponseMessage> SendAsyncRequest(HttpRequestMessage message, HttpCompletionOption option, CancellationToken token)
        {
            var client = default(CouchbaseLiteHttpClient);
            if(_client.AcquireTemp(out client)) {
                foreach(var header in RequestHeaders) {
                    var str = header.Value as string;
                    if(str != null) {
                        message.Headers.Add(header.Key, str);
                    }
                }
                return client.SendAsync(message, option, token);
            } else {
                Log.To.Sync.W(Tag, "Aborting message sent after disposal");
            }

            return null;
        }

        internal HttpRequestMessage SendAsyncRequest(HttpMethod method, string relativePath, object body, RemoteRequestCompletionBlock completionHandler, bool ignoreCancel = false)
        {
            try {
                var url = _baseUrl.Append(relativePath);
                return SendAsyncRequest(method, url, body, completionHandler, ignoreCancel);
            } catch(UriFormatException e) {
                throw Misc.CreateExceptionAndLog(Log.To.Sync, e, Tag, "Malformed URL for async request");
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Sync, e, Tag, "Error sending async request {0}",
                    new SecureLogString(relativePath, LogMessageSensitivity.PotentiallyInsecure));
            }
        }

        internal HttpRequestMessage SendAsyncRequest(HttpMethod method, Uri url, object body, RemoteRequestCompletionBlock completionHandler, bool ignoreCancel)
        {
            var message = new HttpRequestMessage(method, url);
            var mapper = Manager.GetObjectMapper();
            message.Headers.Add("Accept", new[] { "multipart/related", "application/json" });

            var bytes = default(byte[]);
            if(body != null) {
                bytes = mapper.WriteValueAsBytes(body).ToArray();
                var byteContent = new ByteArrayContent(bytes);
                message.Content = byteContent;
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            var token = ignoreCancel ? CancellationToken.None : _remoteRequestCancellationSource.Token;
            Log.To.Sync.V(Tag, "{0} - Sending {1} request to: {2}", _id, method, new SecureLogUri(url));
            var client = default(CouchbaseLiteHttpClient);
            if(!_client.AcquireFor(TimeSpan.FromSeconds(1), out client)) {
                Log.To.Sync.I(Tag, "Client is disposed, aborting request to {0}", new SecureLogUri(url));
                return null;
            }

            client.Authenticator = Authenticator;
            var t = client.SendAsync(message, token).ContinueWith(response =>
            {
                try {
                    HttpResponseMessage result = null;
                    Exception error = null;
                    if(!response.IsFaulted && !response.IsCanceled) {
                        result = response.Result;
                        UpdateServerType(result);
                    } else if(response.IsFaulted) {
                        Log.To.Sync.W(Tag, String.Format("Http Message failed to send, or got error response, " +
                            "passing to callback... {0}, ",
                            new SecureLogUri(message.RequestUri)), response.Exception);
                        if(bytes != null) {
                            try {
                                Log.To.Sync.W(Tag, "\tFailed content: {0}", new SecureLogString(bytes, LogMessageSensitivity.PotentiallyInsecure));
                            } catch(ObjectDisposedException) { }
                        }
                    }

                    if(completionHandler != null) {
                        object fullBody = null;

                        try {
                            if(response.Status != TaskStatus.RanToCompletion) {
                                Log.To.Sync.V(Tag, "SendAsyncRequest did not run to completion.");
                            }

                            if(response.IsCanceled) {
                                error = new WebException("SendAsyncRequest was cancelled", System.Net.WebExceptionStatus.RequestCanceled);
                            } else {
                                error = Misc.Flatten(response.Exception).FirstOrDefault();
                            }

                            if(error == null) {
                                if(!result.IsSuccessStatusCode) {
                                    result = response.Result;
                                    error = new HttpResponseException(result.StatusCode);
                                }
                            }

                            if(error == null) {
                                var content = result.Content;
                                if(content != null) {
                                    fullBody = mapper.ReadValue<object>(content.ReadAsStreamAsync().Result);
                                }

                                error = null;
                            }
                        } catch(Exception e) {
                            error = e;
                            Log.To.Sync.W(Tag, "SendAsyncRequest got an exception while processing response, " +
                                "passing it on to the callback.", e);
                        }

                        completionHandler(fullBody, error);
                    }

                    if(result != null) {
                        result.Dispose();
                    }
                } finally {
                    Task dummy;
                    _requests.TryRemove(message, out dummy);
                    message.Dispose();
                }
            }, token);

            _requests.AddOrUpdate(message, k => t, (k, v) => t);
            return message;
        }

        internal void SendAsyncMultipartDownloaderRequest(HttpMethod method, string relativePath, object body, Database db, RemoteRequestCompletionBlock onCompletion)
        {
            try {
                var url = _baseUrl.Append(relativePath);

                var message = new HttpRequestMessage(method, url);
                message.Headers.Add("Accept", "*/*");
                AddRequestHeaders(message);

                var client = default(CouchbaseLiteHttpClient);
                if(!_client.AcquireFor(TimeSpan.FromSeconds(1), out client)) {
                    Log.To.Sync.I(Tag, "Client is disposed, aborting request to {0}", new SecureLogString(relativePath, LogMessageSensitivity.PotentiallyInsecure));
                    return;
                }

                client.Authenticator = Authenticator;
                var request = client.SendAsync(message, _cancellationTokenSource.Token).ContinueWith(new Action<Task<HttpResponseMessage>>(responseMessage =>
                {
                    object fullBody = null;
                    Exception error = null;
                    try {
                        if(responseMessage.IsFaulted) {
                            error = responseMessage.Exception.InnerException;
                            if(onCompletion != null) {
                                onCompletion(null, error);
                            }

                            return;
                        }

                        var response = responseMessage.Result;
                        // add in cookies to global store
                        //CouchbaseLiteHttpClientFactory.Instance.AddCoIokies(clientFactory.HttpHandler.CookieContainer.GetCookies(url));

                        var status = response.StatusCode;
                        if((Int32)status.GetStatusCode() >= 300) {
                            Log.To.Sync.W(Tag, "Got error {0}", status.GetStatusCode());
                            Log.To.Sync.W(Tag, "Request was for: " + message);
                            Log.To.Sync.W(Tag, "Status reason: " + response.ReasonPhrase);
                            Log.To.Sync.W(Tag, "Passing error onto callback...");
                            error = new HttpResponseException(status);
                            if(onCompletion != null) {
                                onCompletion(null, error);
                            }
                        } else {
                            var entity = response.Content;
                            var contentTypeHeader = response.Content.Headers.ContentType;
                            Stream inputStream = null;
                            if(contentTypeHeader != null && contentTypeHeader.ToString().Contains("multipart/related")) {
                                try {
                                    var reader = new MultipartDocumentReader(db);
                                    var contentType = contentTypeHeader.ToString();
                                    reader.SetContentType(contentType);

                                    var inputStreamTask = entity.ReadAsStreamAsync();
                                    //inputStreamTask.Wait(90000, CancellationTokenSource.Token);
                                    inputStream = inputStreamTask.Result;

                                    const int bufLen = 1024;
                                    var buffer = new byte[bufLen];

                                    int numBytesRead = 0;
                                    while((numBytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0) {
                                        if(numBytesRead != bufLen) {
                                            var bufferToAppend = new Couchbase.Lite.Util.ArraySegment<Byte>(buffer, 0, numBytesRead);
                                            reader.AppendData(bufferToAppend);
                                        } else {
                                            reader.AppendData(buffer);
                                        }
                                    }

                                    reader.Finish();
                                    fullBody = reader.GetDocumentProperties();

                                    if(onCompletion != null) {
                                        onCompletion(fullBody, error);
                                    }
                                } catch(Exception ex) {
                                    Log.To.Sync.W(Tag, "SendAsyncMultipartDownloaderRequest got an exception, aborting...", ex);
                                } finally {
                                    try {
                                        inputStream.Close();
                                    } catch(Exception) { }
                                }
                            } else {
                                if(entity != null) {
                                    try {
                                        var readTask = entity.ReadAsStreamAsync();
                                        //readTask.Wait(); // TODO: This should be scaled based on content length.
                                        inputStream = readTask.Result;
                                        fullBody = Manager.GetObjectMapper().ReadValue<Object>(inputStream);
                                        if(onCompletion != null)
                                            onCompletion(fullBody, error);
                                    } catch(Exception ex) {
                                        Log.To.Sync.W(Tag, "SendAsyncMultipartDownloaderRequest got an exception, aborting...", ex);
                                    } finally {
                                        try {
                                            inputStream.Close();
                                        } catch(Exception) { }
                                    }
                                }
                            }
                        }
                    } catch(Exception e) {
                        Log.To.Sync.W(Tag, "Got exception during SendAsyncMultipartDownload, aborting...");
                        error = e;
                    } finally {
                        Task dummy;
                        _requests.TryRemove(message, out dummy);
                        responseMessage.Result.Dispose();
                    }
                }), _workExecutor.Scheduler);
                _requests.TryAdd(message, request);
            } catch(UriFormatException e) {
                Log.To.Sync.W(Tag, "Malformed URL for async request, aborting...", e);
            }
        }

        internal void SendAsyncMultipartRequest(HttpMethod method, string relativePath, MultipartContent multiPartEntity, RemoteRequestCompletionBlock completionHandler)
        {
            Uri url = null;
            try {
                url = _baseUrl.Append(relativePath);
            } catch(UriFormatException) {
                Log.To.Sync.E(Tag, "Invalid path received for request: {0}, throwing...",
                    new SecureLogString(relativePath, LogMessageSensitivity.PotentiallyInsecure));
                throw new ArgumentException("Invalid path", "relativePath");
            }

            var message = new HttpRequestMessage(method, url);
            message.Content = multiPartEntity;
            message.Headers.Add("Accept", "*/*");

            var client = default(CouchbaseLiteHttpClient);
            if(!_client.AcquireFor(TimeSpan.FromSeconds(1), out client)) {
                Log.To.Sync.I(Tag, "Client is disposed, aborting request to {0}", new SecureLogString(relativePath, LogMessageSensitivity.PotentiallyInsecure));
                return;
            }

            var _lastError = default(Exception);
            client.Authenticator = Authenticator;
            var t = client.SendAsync(message, _cancellationTokenSource.Token).ContinueWith(response =>
            {
                multiPartEntity.Dispose();
                if(response.Status != TaskStatus.RanToCompletion) {
                    _lastError = response.Exception;
                    Log.To.Sync.W(Tag, "SendAsyncRequest did not run to completion, returning null...");
                    return Task.FromResult((Stream)null);
                }
                if((int)response.Result.StatusCode > 300) {
                    _lastError = new HttpResponseException(response.Result.StatusCode);
                    Log.To.Sync.W(Tag, "Server returned HTTP Error, returning null...");
                    return Task.FromResult((Stream)null);
                }
                return response.Result.Content.ReadAsStreamAsync();
            }, _cancellationTokenSource.Token).ContinueWith(response =>
            {
                try {
                    var hasEmptyResult = response.Result == null || response.Result.Result == null || response.Result.Result.Length == 0;
                    if(response.Status != TaskStatus.RanToCompletion) {
                        Log.To.Sync.W(Tag, "SendAsyncRequest phase two did not run to completion, continuing...");
                    } else if(hasEmptyResult) {
                        Log.To.Sync.W(Tag, "Server returned an empty response, continuing...");
                    }

                    if(completionHandler != null) {
                        object fullBody = null;
                        if(!hasEmptyResult) {
                            var mapper = Manager.GetObjectMapper();
                            fullBody = mapper.ReadValue<Object>(response.Result.Result);
                        }

                        completionHandler(fullBody, response.Exception ?? _lastError);
                    }
                } finally {
                    Task dummy;
                    _requests.TryRemove(message, out dummy);
                }
            }, _cancellationTokenSource.Token);
            _requests.TryAdd(message, t);
        }

        private void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach(string requestHeaderKey in RequestHeaders.Keys) {
                request.Headers.Add(requestHeaderKey, RequestHeaders.Get(requestHeaderKey).ToString());
            }
        }

        private void UpdateServerType(HttpResponseMessage response)
        {
            var server = response.Headers.Server;
            if(server != null && server.Any()) {
                var serverString = String.Join(" ", server.Select(pi => pi.Product).Where(pi => pi != null).ToStringArray());
                ServerType = new RemoteServerVersion(serverString);
                Log.To.Sync.I(Tag, "{0}: Server Version: {0}", ServerType);
            }
        }
    }
}
