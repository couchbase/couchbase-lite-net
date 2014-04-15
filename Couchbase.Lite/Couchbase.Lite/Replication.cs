//
// Replication.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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
using System.Net;
using System.Web;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using Couchbase.Lite.Util;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Support;
using Couchbase.Lite.Internal;
using Sharpen;


namespace Couchbase.Lite
{

    #region Enums

    public enum ReplicationStatus {
        Stopped,
        Offline,
        Idle,
        Active
    }

    #endregion

    public abstract partial class Replication 
    {

    #region Constants

        internal static readonly String ReplicatorDatabaseName = "_replicator";

    #endregion

    #region Constructors

        protected Replication(Database db, Uri remote, bool continuous, TaskFactory workExecutor)
            : this(db, remote, continuous, null, workExecutor) { }

        /// <summary>Private Constructor</summary>
        protected Replication(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory, TaskFactory workExecutor, CancellationTokenSource tokenSource = null)
        {
            LocalDatabase = db;
            Continuous = continuous;
            WorkExecutor = workExecutor;
            CancellationTokenSource = tokenSource ?? new CancellationTokenSource();
            RemoteUrl = remote;
            Status = ReplicationStatus.Stopped;
            online = true;
            RequestHeaders = new Dictionary<String, Object>();

            if (RemoteUrl.GetQuery() != null && !RemoteUrl.GetQuery().IsEmpty())
            {
                var uri = new Uri(remote.ToString());
                var personaAssertion = URIUtils.GetQueryParameter(uri, PersonaAuthorizer.QueryParameter);

                if (personaAssertion != null && !personaAssertion.IsEmpty())
                {
                    var email = PersonaAuthorizer.RegisterAssertion(personaAssertion);
                    var authorizer = new PersonaAuthorizer(email);
                    Authorizer = authorizer;
                }

                var facebookAccessToken = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QueryParameter);

                if (facebookAccessToken != null && !facebookAccessToken.IsEmpty())
                {
                    var email = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QueryParameterEmail);
                    var authorizer = new FacebookAuthorizer(email);
                    Uri remoteWithQueryRemoved = null;

                    try
                    {
                        remoteWithQueryRemoved = new UriBuilder(remote.Scheme, remote.GetHost(), remote.Port, remote.AbsolutePath).Uri;
                    }
                    catch (UriFormatException e)
                    {
                        throw new ArgumentException("Invalid URI format.", "remote", e);
                    }

                    FacebookAuthorizer.RegisterAccessToken(facebookAccessToken, email, remoteWithQueryRemoved.ToString());

                    Authorizer = authorizer;
                }
                // we need to remove the query from the URL, since it will cause problems when
                // communicating with sync gw / couchdb
                try
                {
                    RemoteUrl = new UriBuilder(remote.Scheme, remote.GetHost(), remote.Port, remote.AbsolutePath).Uri;
                }
                catch (UriFormatException e)
                {
                    throw new ArgumentException("Invalid URI format.", "remote", e);
                }
            }

            Batcher = new Batcher<RevisionInternal>(workExecutor, InboxCapacity, ProcessorDelay, 
                inbox => 
                {
                    Log.V (Database.Tag, "*** " + this + ": BEGIN processInbox (" + inbox.Count + " sequences)");
                    ProcessInbox (new RevisionList (inbox));
                    Log.V (Database.Tag, "*** " + this.ToString () + ": END processInbox (lastSequence=" + LastSequence);
                    UpdateActive();
                }, CancellationTokenSource);

            this.clientFactory = clientFactory ?? CouchbaseLiteHttpClientFactory.Instance;
        }

    #endregion

    #region Constants

        const int ProcessorDelay = 500; //Milliseconds
        const int InboxCapacity = 100;
        const int RetryDelay = 60; // Seconds
        const int SaveLastSequenceDelay = 2; //Seconds

        readonly string Tag = "Replication";

    #endregion

    #region Non-public Members

        private static Int32 lastSessionID = 0;

        readonly protected TaskFactory WorkExecutor; // FIXME: Remove this.

        readonly protected IHttpClientFactory clientFactory;

        protected internal String  sessionID;

        protected internal Boolean lastSequenceChanged;

        private String lastSequence;
        protected internal String  LastSequence 
        {
            get { return lastSequence; }
            set 
            {
                if (value != null && !value.Equals(lastSequence))
                {
                    Log.V(Tag, this + ": Setting lastSequence to " + value + " from( " + lastSequence + ")");
                    lastSequence = value;

                    if (!lastSequenceChanged)
                    {
                        lastSequenceChanged = true;
                        Task.Delay(SaveLastSequenceDelay)
                            .ContinueWith(task => 
                            {
                                SaveLastSequence();
                            });
                    }
                }
            }
        }

        protected internal Boolean savingCheckpoint;
        protected internal Boolean overdueForSave;
        protected internal IDictionary<String, Object> remoteCheckpoint;
        protected internal Boolean online;

        protected internal Boolean continuous;

        protected internal Int32 completedChangesCount;
        protected internal Int32 changesCount;
        protected internal Int32 asyncTaskCount;
        protected internal Boolean active;

        internal Authorizer Authorizer { get; set; }
        internal Batcher<RevisionInternal> Batcher { get; set; }
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private CancellationTokenSource RetryIfReadyTokenSource { get; set; }
        private Task RetryIfReadyTask { get; set; }

        protected internal IDictionary<String, Object> RequestHeaders { get; set; }

        private Int32 revisionsFailed;


        readonly object asyncTaskLocker = new object ();

        void NotifyChangeListeners ()
        {
            var evt = Changed;
            if (evt == null) return;

            var args = new ReplicationChangeEventArgs(this);
            evt(this, args);
        }

        //TODO: Do we need this method? It's not in the API Spec.
        internal bool GoOffline()
        {
            if (!online)
            {
                return false;
            }

            Log.D(Database.Tag, this + ": Going offline");
            online = false;

            StopRemoteRequests();

            UpdateProgress();
            NotifyChangeListeners();

            return true;
        }

        //TODO: Do we need this method? It's not in the API Spec.
        internal bool GoOnline()
        {
            if (online)
            {
                return false;
            }

            Log.D(Database.Tag, this + ": Going online");
            online = true;
            if (IsRunning)
            {
                lastSequence = null;
                LastError = null;
            }

            CheckSession();
            NotifyChangeListeners();

            return true;
        }

        internal void StopRemoteRequests()
        {
            // TODO:
        }

        internal void UpdateProgress()
        {
            if (!IsRunning)
            {
                Status = ReplicationStatus.Stopped;
            }
            else
            {
                if (!online)
                {
                    Status = ReplicationStatus.Offline;
                }
                else
                {
                    if (active)
                    {
                        Status = ReplicationStatus.Active;
                    }
                    else
                    {
                        Status = ReplicationStatus.Idle;
                    }
                }
            }
        }

        internal void DatabaseClosing()
        {
            SaveLastSequence();
            Stop();
            ClearDbRef();
        }

        internal void ClearDbRef()
        {
            if (savingCheckpoint && LastSequence != null)
            {
                LocalDatabase.SetLastSequence(LastSequence, RemoteCheckpointDocID(), !IsPull);
                LocalDatabase = null;
            }
        }

        internal void AddToInbox(RevisionInternal rev)
        {
            Batcher.QueueObject(rev);
            UpdateActive();
        }

        internal void CheckSession()
        {
            if (Authorizer != null && Authorizer.UsesCookieBasedLogin)
            {
                CheckSessionAtPath("/_session");
            }
            else
            {
                FetchRemoteCheckpointDoc();
            }
        }

        internal void CheckSessionAtPath(string sessionPath)
        {
            Log.D(Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": checkSessionAtPath() calling asyncTaskStarted()");
            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Get, sessionPath, null, (result, e) => {
                try
                {
                    if (e != null)
                    {
                        if (e is HttpException && ((HttpException)e).GetHttpCode() == 404
                            && sessionPath.Equals("/_session", StringComparison.InvariantCultureIgnoreCase)) {
                            CheckSessionAtPath ("_session");
                            return;
                        }
                        Log.E(Tag, this + ": Session check failed", e);
                        LastError = e;
                    }
                    else
                    {
                        var response = (IDictionary<String, Object>)result;
                        var userCtx = (IDictionary<String, Object>)response.Get ("userCtx");
                        var username = (string)userCtx.Get ("name");

                        if (!string.IsNullOrEmpty (username)) {
                            Log.D (Tag, string.Format ("{0} Active session, logged in as {1}", this, username));
                            FetchRemoteCheckpointDoc ();
                        } else {
                            Log.D (Tag, string.Format ("{0} No active session, going to login", this));
                            Login ();
                        }
                    }
                } 
                finally
                {
                    Log.D(Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": checkSessionAtPath() calling asyncTaskFinished()");
                    AsyncTaskFinished (1);
                }
            });
        }

        protected internal virtual void Login()
        {
            var loginParameters = Authorizer.LoginParametersForSite(RemoteUrl);
            if (loginParameters == null)
            {
                Log.D(Tag, String.Format("{0}: {1} has no login parameters, so skipping login", this, Authorizer));
                FetchRemoteCheckpointDoc();
                return;
            }

            var loginPath = Authorizer.LoginPathForSite(RemoteUrl);
            Log.D(Tag, string.Format("{0}: Doing login with {1} at {2}", this, Authorizer.GetType(), loginPath));

            Log.D(Tag, string.Format("{0} | {1} : login() calling asyncTaskStarted()", this, Sharpen.Thread.CurrentThread()));
            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Post, loginPath, loginParameters, (result, e) => {
                try
                {
                    if (e != null) 
                    {
                        Log.D (Tag, string.Format ("{0}: Login failed for path: {1}", this, loginPath));
                        LastError = e;
                    }
                    else 
                    {
                        Log.D (Tag, string.Format ("{0}: Successfully logged in!", this));
                        FetchRemoteCheckpointDoc ();
                    }
                }
                finally
                {
                    Log.D(Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": login() calling asyncTaskFinished()");
                    AsyncTaskFinished (1);
                }
            });
        }

        internal void FetchRemoteCheckpointDoc()
        {
            lastSequenceChanged = false;
            var localLastSequence = LocalDatabase.LastSequenceWithRemoteURL(RemoteUrl, !IsPull);
            Log.D(Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": fetchRemoteCheckpointDoc() calling asyncTaskStarted()");
            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Get, "/_local/" + RemoteCheckpointDocID(), null, (response, e) => {
                try
                {
                    if (e != null && !Is404 (e)) {
                        Log.D (Tag, this + " error getting remote checkpoint: " + e);
                        LastError = e;
                    } else {
                        if (e != null && Is404 (e)) {
                            Log.D (Tag, this + " 404 error getting remote checkpoint " + RemoteCheckpointDocID () + ", calling maybeCreateRemoteDB");
                            MaybeCreateRemoteDB ();
                        }

                        var responseData = (JObject)response;
                        var result = responseData.ToObject<IDictionary<string, object>>();

                        remoteCheckpoint = result;
                        var remoteLastSequence = String.Empty;

                        if (result != null) {
                            remoteLastSequence = (string)result.Get ("lastSequence");
                        }
                        if (remoteLastSequence != null && remoteLastSequence.Equals (localLastSequence)) {
                            LastSequence = localLastSequence;
                            Log.V (Tag, this + ": Replicating from lastSequence=" + LastSequence);
                        } else {
                            Log.V (Tag, this + ": lastSequence mismatch: I had " + localLastSequence + ", remote had " + remoteLastSequence);
                        }
                        BeginReplicating ();
                    }
                }
                catch (Exception ex)
                {
                    Log.E(Tag, "Error", ex);
                }
                finally
                {
                    AsyncTaskFinished (1);
                }
            });
        }

        private static bool Is404(Exception e)
        {
            return e is HttpException && ((HttpException)e).GetHttpCode () == 404;
        }

        internal abstract void BeginReplicating();

        /// <summary>CHECKPOINT STORAGE:</summary>
        protected internal virtual void MaybeCreateRemoteDB() { }

        // FIXME: No-op.
        abstract internal void ProcessInbox(RevisionList inbox);

        internal void AsyncTaskStarted()
        {   // TODO.ZJG: Replace lock with Interlocked.CompareExchange.
            lock (asyncTaskLocker)
            {
                Log.D(Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": asyncTaskStarted() called, asyncTaskCount: " + asyncTaskCount);
                if (asyncTaskCount++ == 0)
                {
                    UpdateActive();
                }
                Log.D(Database.Tag, "asyncTaskStarted() updated asyncTaskCount to " + asyncTaskCount);
            }
        }

        internal void AsyncTaskFinished(Int32 numTasks)
        {   // TODO.ZJG: Replace lock with Interlocked.CompareExchange.
            lock (asyncTaskLocker) {
                Log.D(Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": asyncTaskFinished() called, asyncTaskCount: "
                    + asyncTaskCount + " numTasks: " + numTasks);

                asyncTaskCount -= numTasks;
                if (asyncTaskCount == 0) 
                {
                    if (!continuous)
                    {
                        UpdateActive();
                    }
                }
                Log.D(Tag, "asyncTaskFinished() updated asyncTaskCount to: " + asyncTaskCount);
            }
        }

        internal void UpdateActive()
        {
            try {
                var batcherCount = 0;
                if (Batcher != null) 
                {
                    batcherCount = Batcher.Count();
                }
                else 
                {
                    Log.W(Tag, this + ": batcher object is null");
                }

                Boolean newActive = batcherCount > 0 || asyncTaskCount > 0;
                if (active != newActive) 
                {
                    Log.D(Tag, this + " Progress: set active = " + newActive + " asyncTaskCount: " + asyncTaskCount + " batcherCount: " + batcherCount );
                    active = newActive;
                    NotifyChangeListeners();

                    if (!active) 
                    {
                        if (!continuous) 
                        {
                            Log.D(Tag, this + " since !continuous, calling stopped()");
                            Stopped();
                        } 
                        else if (LastError != null) /*(revisionsFailed > 0)*/ 
                        {
                            string msg = string.Format("%s: Failed to xfer %d revisions, will retry in %d sec", this, revisionsFailed, RetryDelay);
                            Log.D(Tag, msg);
                            CancelPendingRetryIfReady();
                            ScheduleRetryIfReady();
                        }

                    }
                }
            } 
            catch (Exception e) 
            {
                Log.E(Tag, "Exception in updateActive()", e);
            }
        }

        internal virtual void Stopped()
        {
            Log.V(Tag, ToString() + " STOPPING");
            IsRunning = false;
            completedChangesCount = changesCount = 0;
            NotifyChangeListeners();
            SaveLastSequence();
            Log.V(Tag, this + " set batcher to null");
            Batcher = null;
            ClearDbRef();
            Log.V(Database.Tag, ToString() + " STOPPED");
        }

        internal void SaveLastSequence()
        {
            if (!lastSequenceChanged)
            {
                return;
            }
            if (savingCheckpoint)
            {
                // If a save is already in progress, don't do anything. (The completion block will trigger
                // another save after the first one finishes.)
                overdueForSave = true;
                return;
            }

            lastSequenceChanged = false;
            overdueForSave = false;

            Log.D(Tag, this + " saveLastSequence() called. lastSequence: " + LastSequence);

            var body = new Dictionary<String, Object>();
            if (remoteCheckpoint != null)
            {
                body.PutAll(remoteCheckpoint);
            }
            body["lastSequence"] = LastSequence;
            var remoteCheckpointDocID = RemoteCheckpointDocID();
            if (String.IsNullOrEmpty(remoteCheckpointDocID))
            {
                Log.W(Tag, this + ": remoteCheckpointDocID is null, aborting saveLastSequence()");
                return;
            }

            savingCheckpoint = true;
            Log.D(Tag, this + " put remote _local document.  checkpointID: " + remoteCheckpointDocID);
            SendAsyncRequest(HttpMethod.Put, "/_local/" + remoteCheckpointDocID, body, (result, e) => {
                savingCheckpoint = false;
                if (e != null) 
                {
                    Log.V (Tag, this + ": Unable to save remote checkpoint", e);
                }

                if (LocalDatabase == null)
                {
                    Log.W(Tag, this + ": Database is null, ignoring remote checkpoint response");
                    return;
                }

                if (e != null)
                {
                    switch (GetStatusFromError(e))
                    {
                        case StatusCode.NotFound:
                            {
                                remoteCheckpoint = null;
                                overdueForSave = true;
                                break;
                            }
                        case StatusCode.Conflict:
                            {
                                RefreshRemoteCheckpointDoc();
                                break;
                            }
                        default:
                            {
                                // TODO: On 401 or 403, and this is a pull, remember that remote
                                // TODO: is read-only & don't attempt to read its checkpoint next time.
                                break;
                            }
                    }
                }
                else 
                {
                    var response = (IDictionary<String, Object>)result;
                    body.Put ("_rev", response.Get ("rev"));
                    remoteCheckpoint = body;
                    LocalDatabase.SetLastSequence(LastSequence, RemoteCheckpointDocID(), IsPull);
                }

                if (overdueForSave) {
                    SaveLastSequence ();
                }
            });
        }

        internal void SendAsyncRequest(HttpMethod method, string relativePath, object body, RemoteRequestCompletionBlock completionHandler)
        {
            try
            {
                var urlStr = BuildRelativeURLString(relativePath);
                var url = new Uri(urlStr);
                SendAsyncRequest(method, url, body, completionHandler);
            }
            catch (UriFormatException e)
            {
                Log.E(Tag, "Malformed URL for async request", e);
            }
        }

        internal String BuildRelativeURLString(String relativePath)
        {
            // the following code is a band-aid for a system problem in the codebase
            // where it is appending "relative paths" that start with a slash, eg:
            //     http://dotcom/db/ + /relpart == http://dotcom/db/relpart
            // which is not compatible with the way the java url concatonation works.
            var remoteUrlString = RemoteUrl.ToString();
            if (remoteUrlString.EndsWith ("/", StringComparison.InvariantCultureIgnoreCase) && relativePath.StartsWith ("/", StringComparison.InvariantCultureIgnoreCase))
            {
                remoteUrlString = remoteUrlString.Substring(0, remoteUrlString.Length - 1);
            }
            return remoteUrlString + relativePath;
        }

        internal void SendAsyncRequest(HttpMethod method, Uri url, Object body, RemoteRequestCompletionBlock completionHandler)
        {
            var message = new HttpRequestMessage(method, url);
            var mapper = Manager.GetObjectMapper();
            if (body != null)
            {
                var bytes = mapper.WriteValueAsBytes(body).ToArray();
                var byteContent = new ByteArrayContent(bytes);
                message.Content = byteContent;
            }
            message.Headers.Add("Accept", new[] { "multipart/related", "application/json" });

            PreemptivelySetAuthCredentials(message);

            var client = clientFactory.GetHttpClient();
            client.CancelPendingRequests();
            client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, CancellationTokenSource.Token)
                .ContinueWith(response => {
                    if (response.Status != TaskStatus.RanToCompletion)
                    {
                        Log.E(Tag, "SendAsyncRequest did not run to completion.", response.Exception);
                        return null;
                    }
                    return response.Result;
                }, CancellationTokenSource.Token)
                .ContinueWith(response => {
                    if (completionHandler != null) {
                        var fullBody = mapper.ReadValue<Object>(response.Result.Content.ReadAsStreamAsync().Result);

                        Exception error = response.Exception;
                        if (error == null && !response.Result.IsSuccessStatusCode)
                        {
                            error = new HttpResponseException(response.Result.StatusCode); 
                        }

                        completionHandler (fullBody, response.Exception);
                    }
                });
        }

        void PreemptivelySetAuthCredentials (HttpRequestMessage message)
        {
            // FIXME Not sure we actually need this, since our handler should do it.. Will find out in tests.

            // if the URL contains user info AND if this a DefaultHttpClient
            // then preemptively set the auth credentials
//            if (url.GetUserInfo() != null)
//            {
//                if (url.GetUserInfo().Contains(":") && !url.GetUserInfo().Trim().Equals(":"))
//                {
//                    string[] userInfoSplit = url.GetUserInfo().Split(":");
//                    Credentials creds = new UsernamePasswordCredentials(URIUtils.Decode(userInfoSplit
//                        [0]), URIUtils.Decode(userInfoSplit[1]));
//                    if (httpClient is DefaultHttpClient)
//                    {
//                        DefaultHttpClient dhc = (DefaultHttpClient)httpClient;
//                        IHttpRequestInterceptor preemptiveAuth = new _IHttpRequestInterceptor_167(creds);
//                        dhc.AddRequestInterceptor(preemptiveAuth, 0);
//                    }
//                }
//                else
//                {
//                    Log.W(Database.Tag, "RemoteRequest Unable to parse user info, not setting credentials"
//                    );
//                }
//            }

        }

        private void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach (string requestHeaderKey in RequestHeaders.Keys)
            {
                request.Headers.Add(requestHeaderKey, RequestHeaders.Get(requestHeaderKey).ToString());
            }
        }

        internal void SendAsyncMultipartDownloaderRequest(HttpMethod method, string relativePath, object body, Database db, RemoteRequestCompletionBlock onCompletion)
        {
            try
            {
                var urlStr = BuildRelativeURLString(relativePath);
                var url = new Uri(urlStr);

                var message = new HttpRequestMessage(method, url);
                message.Headers.Add("Accept", "*/*");
                AddRequestHeaders(message);

                var httpClient = clientFactory.GetHttpClient();
                httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, CancellationTokenSource.Token)
                    .ContinueWith(new Action<Task<HttpResponseMessage>>(responseMessage=> {
                        object fullBody = null;
                        Exception error = null;
                        try
                        {
                            var response = responseMessage.Result;
                            // add in cookies to global store
                            CouchbaseLiteHttpClientFactory.Instance.AddCookies(clientFactory.HttpHandler.CookieContainer.GetCookies(url));
                               
                            var status = response.StatusCode;
                            if ((Int32)status.GetStatusCode() >= 300)
                            {
                                Log.E(Database.Tag, "Got error " + Sharpen.Extensions.ToString(status.GetStatusCode
                                    ()));
                                Log.E(Database.Tag, "Request was for: " + message);
                                Log.E(Database.Tag, "Status reason: " + response.ReasonPhrase);
                                error = new HttpException((Int32)status.GetStatusCode(), response.ReasonPhrase);
                            }
                        else
                        {
                            var entity = response.Content;
                            var contentTypeHeader = response.Content.Headers.ContentType;
                            InputStream inputStream = null;
                            if (contentTypeHeader != null && contentTypeHeader.ToString().Contains("multipart/related"))
                            {
                                try
                                {
                                    var reader = new MultipartDocumentReader(responseMessage.Result, LocalDatabase);
                                    reader.SetContentType(contentTypeHeader.MediaType);

                                    var inputStreamTask = entity.ReadAsStreamAsync();
                                    inputStreamTask.Wait(90000, CancellationTokenSource.Token);
                                    
                                    const int bufLen = 1024;
                                    var buffer = new byte[bufLen];
                                    
                                    int numBytesRead = 0;
                                    while ((numBytesRead = inputStream.Read(buffer)) != -1)
                                    {
                                        if (numBytesRead != bufLen)
                                        {
                                            var bufferToAppend = new ArraySegment<Byte>(buffer, 0, numBytesRead).Array;
                                            reader.AppendData(bufferToAppend);
                                        }
                                        else
                                        {
                                            reader.AppendData(buffer);
                                        }
                                    }

                                    reader.Finish();
                                    fullBody = reader.GetDocumentProperties();

                                    if (onCompletion != null)
                                        onCompletion(fullBody, error);
                                }
                                finally
                                {
                                    try
                                    {
                                        inputStream.Close();
                                    }
                                    catch (IOException)
                                    {
                                            // NOTE: swallow?
                                    }
                                }
                            }
                            else
                            {
                                if (entity != null)
                                {
                                    try
                                    {
                                        var readTask = entity.ReadAsStreamAsync();
                                        readTask.Wait(); // TODO: This should be scaled based on content length.
                                        inputStream = readTask.Result;
                                        fullBody = Manager.GetObjectMapper().ReadValue<Object>(inputStream);
                                        if (onCompletion != null)
                                            onCompletion(fullBody, error);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.E(Tag, ex.Message);
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
                    catch (ProtocolViolationException e)
                    {
                        Log.E(Database.Tag, "client protocol exception", e);
                        error = e;
                    }
                    catch (IOException e)
                    {
                        Log.E(Database.Tag, "io exception", e);
                        error = e;
                    }
                    }));
            }
            catch (UriFormatException e)
            {
                Log.E(Database.Tag, "Malformed URL for async request", e);
            }
        }

        internal void SendAsyncMultipartRequest(HttpMethod method, String relativePath, MultipartContent multiPartEntity, RemoteRequestCompletionBlock completionHandler)
        {
            Uri url = null;
            try
            {
                var urlStr = BuildRelativeURLString(relativePath);
                url = new Uri(urlStr);
            }
            catch (UriFormatException e)
            {
                throw new ArgumentException("Invalid URI format.", e);
            }

            var message = new HttpRequestMessage(method, url);
            message.Content = multiPartEntity;
            message.Headers.Add("Accept", "*/*");

            PreemptivelySetAuthCredentials(message);

            var client = clientFactory.GetHttpClient();

            client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, CancellationTokenSource.Token)
                .ContinueWith(response=> {
                    if (response.Status != TaskStatus.RanToCompletion)
                    {
                        Log.E(Tag, "SendAsyncRequest did not run to completion.", response.Exception);
                        return null;
                    }
                    return response.Result.Content.ReadAsStreamAsync();
                }, CancellationTokenSource.Token)
                .ContinueWith(response=> {
                    if (response.Status != TaskStatus.RanToCompletion)
                    {
                        Log.E(Tag, "SendAsyncRequest did not run to completion.", response.Exception);
                    } else if (response.Result.Result == null || response.Result.Result.Length == 0)
                    {
                        Log.E(Tag, "Server returned an empty response.", response.Exception);
                    }
                    if (completionHandler != null) {
                        var mapper = Manager.GetObjectMapper();
                        var fullBody = mapper.ReadValue<Object>(response.Result.Result);
                        completionHandler (fullBody, response.Exception);
                    }
                }, CancellationTokenSource.Token);
        }


        // Pusher overrides this to implement the .createTarget option
        /// <summary>This is the _local document ID stored on the remote server to keep track of state.
        ///     </summary>
        /// <remarks>
        /// This is the _local document ID stored on the remote server to keep track of state.
        /// Its ID is based on the local database ID (the private one, to make the result unguessable)
        /// and the remote database's URL.
        /// </remarks>
        internal String RemoteCheckpointDocID()
        {
            if (LocalDatabase == null)
                return null;

            var input = LocalDatabase.PrivateUUID () + "\n" + RemoteUrl + "\n" + (!IsPull ? "1" : "0");
            return Misc.TDHexSHA1Digest(Runtime.GetBytesForString(input));
        }

        internal StatusCode GetStatusFromError(Exception e)
        {
            var couchbaseLiteException = e as CouchbaseLiteException;
            if (couchbaseLiteException != null)
            {
                return couchbaseLiteException.GetCBLStatus().GetCode();
            }
            return StatusCode.Unknown;
        }

        internal void RefreshRemoteCheckpointDoc()
        {
            Log.D(Tag, this + ": Refreshing remote checkpoint to get its _rev...");
            savingCheckpoint = true;
            AsyncTaskStarted();

            SendAsyncRequest(HttpMethod.Get, "/_local/" + RemoteCheckpointDocID(), null, (result, e) =>
            {
                try
                {
                    if (LocalDatabase == null)
                    {
                        Log.W(Tag, this + ": db == null while refreshing remote checkpoint.  aborting");
                        return;
                    }

                    savingCheckpoint = false;

                    if (e != null && GetStatusFromError(e) != StatusCode.NotFound)
                    {
                        Log.E(Database.Tag, this + ": Error refreshing remote checkpoint", e);
                    }
                    else
                    {
                        Log.D(Database.Tag, this + ": Refreshed remote checkpoint: " + result);
                        remoteCheckpoint = (IDictionary<string, object>)result;
                        lastSequenceChanged = true;
                        SaveLastSequence();
                    }

                } 
                finally
                {
                    AsyncTaskFinished(1);
                }
            }
            );
        }

        internal void RevisionFailed()
        {
            revisionsFailed++;
        }

        /// <summary>
        /// Called after a continuous replication has gone idle, but it failed to transfer some revisions
        /// and so wants to try again in a minute.
        /// </summary>
        /// <remarks>
        /// Called after a continuous replication has gone idle, but it failed to transfer some revisions
        /// and so wants to try again in a minute. Should be overridden by subclasses.
        /// </remarks>
        protected internal virtual void Retry()
        {
            LastError = null;
        }

        protected internal virtual void RetryIfReady()
        {
            if (!IsRunning)
            {
                return;
            }
                
            if (online)
            {
                Log.D(Tag, this + " RETRYING, to transfer missed revisions...");
                revisionsFailed = 0;
                CancelPendingRetryIfReady();
                Retry();
            }
            else
            {
                ScheduleRetryIfReady();
            }
        }

        protected internal virtual void CancelPendingRetryIfReady()
        {
            if (RetryIfReadyTask != null && !RetryIfReadyTask.IsCanceled && RetryIfReadyTokenSource != null)
            {
                RetryIfReadyTokenSource.Cancel();
            }
        }

        protected internal virtual void ScheduleRetryIfReady()
        {
            RetryIfReadyTokenSource = new CancellationTokenSource();
            RetryIfReadyTask = Task.Delay(RetryDelay * 1000)
                .ContinueWith(task => 
                {
                    if (RetryIfReadyTokenSource != null && !RetryIfReadyTokenSource.IsCancellationRequested)
                        RetryIfReady();
                }, RetryIfReadyTokenSource.Token);
        }
            
    #endregion
    
    #region Instance Members

        /// <summary>
        /// Gets the local <see cref="Couchbase.Lite.Database"/> being replicated to\from.
        /// </summary>
        public Database LocalDatabase { get; private set; }

        /// <summary>
        /// Gets the remote URL being replicated to/from.
        /// </summary>
        public Uri RemoteUrl { get; private set; }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Replication"/>  pulls from, as opposed to pushes to, the target.
        /// </summary>
        public abstract Boolean IsPull { get; }

        /// <summary>
        /// Gets or sets whether the target <see cref="Couchbase.Lite.Database"/> will be created if it doesn't already exist.
        /// </summary>
        public abstract Boolean CreateTarget { get; set; }

        /// <summary>
        /// Gets or sets whether the <see cref="Couchbase.Lite.Replication"/> operates continuously, replicating 
        /// changes as the source <see cref="Couchbase.Lite.Database"/> is modified.
        /// </summary>
        public Boolean Continuous 
        { 
            get { return continuous; }
            set { if (!IsRunning) continuous = value; }
        }

        /// <summary>
        /// Gets or sets the name of an optional filter function to run on the source 
        /// <see cref="Couchbase.Lite.Database"/>. Only documents for which the function returns <c>true</c> are replicated.
        /// </summary>
        public String Filter { get; set; }

        /// <summary>
        /// Gets or sets the parameters to pass to the filter function.
        /// </summary>
        public IDictionary<String, String> FilterParams { get; set; }

        /// <summary>
        /// Gets or sets the list of Sync Gateway channel names to filter by for pull <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        /// <remarks>
        /// A null value means no filtering, and all available channels will be replicated.  Only valid for pull 
        /// replications whose source database is on a Couchbase Sync Gateway server. This is a convenience property 
        /// that just sets the values of filter and filterParams.
        /// </remarks>
        public abstract IEnumerable<String> Channels { get; set; }

        /// <summary>
        /// Gets or sets the ids of the <see cref="Couchbase.Lite.Document"/>s to replicate.
        /// </summary>
        public abstract IEnumerable<String> DocIds { get; set; }

        /// <summary>
        /// Gets or sets the extra HTTP headers to send in <see cref="Couchbase.Lite.Replication"/> requests to the 
        /// remote <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        public abstract Dictionary<String, String> Headers { get; set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Replication"/>'s current status.
        /// </summary>
        public ReplicationStatus Status { get; set; }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Replication"/> is running.  Continuous <see cref="Couchbase.Lite.Replication"/> never actually stop, instead they go 
        /// idle waiting for new data to appear.
        /// </summary>
        /// <value><c>true</c> if this instance is running; otherwise, <c>false</c>.</value>
        public Boolean IsRunning { get; protected set; }

        /// <summary>
        /// Gets the last error, if any, that occurred since the <see cref="Couchbase.Lite.Replication"/> was started.
        /// </summary>
        public Exception LastError { get; protected set; }

        /// <summary>
        /// If the <see cref="Couchbase.Lite.Replication"/> is active, gets the number of completed changes that have been processed, otherwise 0.
        /// </summary>
        /// <value>The completed changes count.</value>
        public Int32 CompletedChangesCount {
            get { return completedChangesCount; }
            protected set {
                completedChangesCount = value;
                NotifyChangeListeners();
            }
        }

        /// <summary>
        /// If the <see cref="Couchbase.Lite.Replication"/> is active, gets the number of changes to be processed, otherwise 0.
        /// </summary>
        /// <value>The changes count.</value>
        public Int32 ChangesCount {
            get { return changesCount; }
            protected set {
                changesCount = value;
                NotifyChangeListeners();
            }
        }

        protected void SetLastError(Exception error) {
            if (LastError != error)
            {
                Log.E(Tag, this + " Progress: set error = " + error);
                LastError = error;
                NotifyChangeListeners();
            }
        }

        //Methods

        /// <summary>
        /// Starts the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public void Start()
        {
            if (!LocalDatabase.Open())
            {
                // Race condition: db closed before replication starts
                Log.W(Tag, "Not starting replication because db.isOpen() returned false.");
                return;
            }

            if (IsRunning)
            {
                return;
            }

            LocalDatabase.AddReplication(this);
            LocalDatabase.AddActiveReplication(this);
            sessionID = string.Format("repl{0:000}", ++lastSessionID);
            Log.V(Database.Tag, ToString() + " STARTING ...");
            IsRunning = true;
            LastSequence = null;
            CheckSession();
        }

        /// <summary>
        /// Stops the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public virtual void Stop() 
        {
            if (!IsRunning)
            {
                return;
            }

            Log.V(Database.Tag, ToString() + " STOPPING...");
            Batcher.Clear();
            // no sense processing any pending changes
            continuous = false;
            StopRemoteRequests();
            CancelPendingRetryIfReady();
            LocalDatabase.ForgetReplication(this);
                
            if (IsRunning && asyncTaskCount == 0)
            {
                Stopped();
            }
        }

        public void Restart()
        {
            // TODO: add the "started" flag and check it here
            Stop();
            Start();
        }

        public event EventHandler<ReplicationChangeEventArgs> Changed;

    #endregion
    
    #region EventArgs Subclasses
        public class ReplicationChangeEventArgs : EventArgs 
        {
            //Properties
            public Replication Source { get; private set; }


            public ReplicationChangeEventArgs (Replication sender)
            {
                Source = sender;
            }
        }

    #endregion
    
    }

}

