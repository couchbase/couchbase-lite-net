//
// Replication.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;


namespace Couchbase.Lite
{

    #region Enums

    /// <summary>
    /// Describes the status of a <see cref="Couchbase.Lite.Replication"/>.
    /// <list type="table">
    /// <listheader>
    /// <term>Name</term>
    /// <description>Description</description>
    /// </listheader>
    /// <item>
    /// <term>Stopped</term>
    /// <description>
    /// The <see cref="Couchbase.Lite.Replication"/> is finished or hit a fatal error.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Offline</term>
    /// <description>
    /// The remote host is currently unreachable.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Idle</term>
    /// <description>
    /// The continuous <see cref="Couchbase.Lite.Replication"/> is caught up and
    /// waiting for more changes.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Active</term>
    /// <description>
    /// The <see cref="Couchbase.Lite.Replication"/> is actively transferring data.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    public enum ReplicationStatus {
        Stopped,
        Offline,
        Idle,
        Active
    }

    #endregion

    /// <summary>
    /// A Couchbase Lite pull or push <see cref="Couchbase.Lite.Replication"/>
    /// between a local and a remote <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public abstract partial class Replication
    {

    #region Constants

        internal static readonly string ChannelsQueryParam = "channels";
        internal static readonly string ByChannelFilterName = "sync_gateway/bychannel";
        internal static readonly string ReplicatorDatabaseName = "_replicator";

    #endregion

    #region Constructors

        protected Replication(Database db, Uri remote, bool continuous, TaskFactory workExecutor)
            : this(db, remote, continuous, null, workExecutor) { }

        /// <summary>Private Constructor</summary>
        protected Replication(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory, TaskFactory workExecutor, CancellationTokenSource tokenSource = null)
        {
            LocalDatabase = db;
            Continuous = continuous;
            // NOTE: Consider running a separate scheduler for all http requests.
            WorkExecutor = workExecutor;
            var ts = tokenSource != null
                ? CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token)
                : new CancellationTokenSource();
            CancellationTokenSource = ts;
            RemoteUrl = remote;
            Status = ReplicationStatus.Stopped;
            online = true;
            RequestHeaders = new Dictionary<String, Object>();
            requests = new HashSet<HttpClient>();

            // FIXME: Refactor to visitor pattern.
            if (RemoteUrl.GetQuery() != null && !RemoteUrl.GetQuery().IsEmpty())
            {
                var uri = new Uri(remote.ToString());
                var personaAssertion = URIUtils.GetQueryParameter(uri, PersonaAuthorizer.QueryParameter);

                if (personaAssertion != null && !personaAssertion.IsEmpty())
                {
                    var email = PersonaAuthorizer.RegisterAssertion(personaAssertion);
                    var authorizer = new PersonaAuthorizer(email);
                    Authenticator = authorizer;
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

                    Authenticator = authorizer;
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

            Batcher = new Batcher<RevisionInternal>(workExecutor, InboxCapacity, ProcessorDelay, inbox =>
            {
                try 
                {
                    Log.V(Tag, "*** BEGIN ProcessInbox ({0} sequences)", inbox.Count);
                    ProcessInbox (new RevisionList(inbox));
                    Log.V(Tag, "*** END ProcessInbox (lastSequence={0})", LastSequence);
                    UpdateActive();
                } 
                catch (Exception e) 
                {
                    Log.E(Tag, "ERROR: ProcessInbox failed: ", e);
                    throw new RuntimeException(e);
                }
            }, CancellationTokenSource);

            SetClientFactory(clientFactory);
        }

    #endregion

    #region Constants

        const int ProcessorDelay = 500; //Milliseconds
        internal const int InboxCapacity = 100;
        const int RetryDelay = 60; // Seconds
        const int SaveLastSequenceDelay = 2; //Seconds

        readonly string Tag = "Replication";

    #endregion

    #region Non-public Members

        private static Int32 lastSessionID = 0;
        private string remoteCheckpointDocID = null;

        readonly protected TaskFactory WorkExecutor;

        protected IHttpClientFactory clientFactory;

        protected internal String  sessionID;

        protected void SetLastError(Exception error) {
            if (LastError != error)
            {
                Log.E(Tag, " Progress: set error = ", error);
                LastError = error;
                NotifyChangeListeners();
            }
        }

        protected internal Boolean lastSequenceChanged;

        private String lastSequence;
        protected internal String LastSequence
        {
            get { return lastSequence; }
            set
            {
                if (value != null && !value.Equals(lastSequence))
                {
                    Log.V(Tag, "Setting LastSequence to " + value + " from( " + lastSequence + ")");
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

        internal Boolean savingCheckpoint;
        internal Boolean overdueForSave;
        internal IDictionary<String, Object> remoteCheckpoint;
        internal volatile Boolean online;
        internal volatile Boolean offline_inprogress;

        internal Boolean continuous;

        internal Int32 completedChangesCount;
        internal Int32 changesCount;
        internal Int32 asyncTaskCount;
        internal Boolean active;

        internal CookieContainer CookieContainer
        {
            get
            {
                return clientFactory.GetCookieContainer();
            }
        }

        internal string ServerType { get; set; }
        internal Batcher<RevisionInternal> Batcher { get; set; }
        protected CancellationTokenSource CancellationTokenSource { get; set; }
        private CancellationTokenSource RetryIfReadyTokenSource { get; set; }
        private Task RetryIfReadyTask { get; set; }

        protected internal IDictionary<String, Object> RequestHeaders { get; set; }

        // FIXME: This probably should become IDictionary<HttpRequestMessage, Task>
        protected internal ICollection<HttpClient> requests;

        private Int32 revisionsFailed;

        readonly object asyncTaskLocker = new object ();

        // FIXME: This is never assigned, as a result Start never initializes revisionBodyTransformationFunction

        internal Func<RevisionInternal, RevisionInternal> revisionBodyTransformationFunction;

        protected void SafeIncrementCompletedChangesCount()
        {
            SafeAddToCompletedChangesCount(1);
        }

        protected void SafeAddToCompletedChangesCount(int value)
        {
            if (value == 0) 
            {
                return;
            }

            Log.V(Tag, ">>>Updating completedChangesCount from {0} by {1}", completedChangesCount, value);
            Interlocked.Add(ref completedChangesCount, value);
            Log.V(Tag, "<<<Updated completedChanges count to {0}", completedChangesCount);
            NotifyChangeListeners();
        }

        protected void SafeAddToChangesCount(int value)
        {
            if (value == 0) 
            {
                return;
            }

            Log.V(Tag, ">>>Updating changesCount from {0} by {1}", changesCount, value);
            Interlocked.Add(ref changesCount, value);
            Log.V(Tag, "<<<Updated changesCount to {0}", changesCount);
            NotifyChangeListeners();
        }

        protected void SetClientFactory(IHttpClientFactory clientFactory)
        {
            if (clientFactory != null)
            {
                this.clientFactory = clientFactory;
            }
            else
            {
                Manager manager = null;
                if (LocalDatabase != null)
                {
                    manager = LocalDatabase.Manager;
                }

                IHttpClientFactory managerClientFactory = null;
                if (manager != null)
                {
                    managerClientFactory = manager.DefaultHttpClientFactory;
                }

                if (managerClientFactory != null)
                {
                    this.clientFactory = managerClientFactory;
                }
                else
                {
                    CookieStore cookieStore = null;
                    if (manager != null)
                    {
                        cookieStore = manager.SharedCookieStore;
                    }

                    if (cookieStore == null)
                    {
                        cookieStore = new CookieStore();
                    }

                    this.clientFactory = new CouchbaseLiteHttpClientFactory(cookieStore);
                }
            }
        }

        void NotifyChangeListeners()
        {
            UpdateProgress();

            var evt = Changed;
            if (evt == null) return;

            var args = new ReplicationChangeEventArgs(this);

            // Ensure callback runs on captured context, which should be the UI thread.
            Log.D(Tag, "Firing NotifyChangeListeners event! [{0} -> {1}]", TaskScheduler.Current.GetType().Name, LocalDatabase.Manager.CapturedContext.Scheduler.GetType().Name);
            LocalDatabase.Manager.CapturedContext.StartNew(()=>evt(this, args));
        }

        // This method will be used by Router & Reachability Manager
        internal virtual bool GoOffline()
        {
            if (!online || offline_inprogress)
            {
                return false;
            }

            if (LocalDatabase == null)
            {
                return false;
            }

            offline_inprogress = true;

            LocalDatabase.Manager.RunAsync(() =>
            {
                Log.D(Tag, "Going offline");

                online = false;
                // FIXME: Shouldn't we let batcher drain?
                StopRemoteRequests();
                UpdateProgress();
                NotifyChangeListeners();
                offline_inprogress = false;
            });

            return true;
        }

        // This method will be used by Router & Reachability Manager
        internal virtual bool GoOnline()
        {
            if (online)
            {
                return false;
            }

            if (LocalDatabase == null)
            {
                return false;
            }

            LocalDatabase.Manager.RunAsync(() =>
            {
                Log.D(Tag, "Going online");
                online = true;
                if (IsRunning)
                {
                    lastSequence = null;
                    SetLastError(null);
                }

                CheckSession();
                NotifyChangeListeners();
            });

            return true;
        }

        internal void StopRemoteRequests()
        {
            IList<HttpClient> remoteRequests;
            lock(requests)
            {
                remoteRequests = new List<HttpClient>(requests);
            }

            foreach(var client in remoteRequests)
            {
                client.CancelPendingRequests();
            }
            Task.WaitAll(((SingleThreadTaskScheduler)WorkExecutor.Scheduler).ScheduledTasks.ToArray());
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
            Log.D(Tag, "ClearDbRef...");
            if (LocalDatabase != null && savingCheckpoint && LastSequence != null)
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
            if (Authenticator != null && Authenticator.UsesCookieBasedLogin)
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
            Log.D(Tag, "checkSessionAtPath() calling asyncTaskStarted()");
            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Get, sessionPath, null, (result, e) => {
                try
                {
                    if (e != null)
                    {
                        if (e is WebException && ((WebException)e).Status == WebExceptionStatus.ProtocolError && ((HttpWebResponse)((WebException)e).Response).StatusCode == HttpStatusCode.NotFound
                            && sessionPath.Equals("/_session", StringComparison.InvariantCultureIgnoreCase)) {
                            CheckSessionAtPath ("_session");
                            return;
                        }
                        Log.E(Tag, "Session check failed", e);
                        SetLastError(e);
                    }
                    else
                    {
                        var response = ((JObject)result).ToObject<IDictionary<string, object>>();
                        var userCtx = ((JObject)response.Get("userCtx")).ToObject<IDictionary<string, object>>();
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
                    Log.D(Tag, "checkSessionAtPath() calling asyncTaskFinished()");
                    AsyncTaskFinished (1);
                }
            });
        }

        protected internal virtual void Login()
        {
            var loginParameters = Authenticator.LoginParametersForSite(RemoteUrl);
            if (loginParameters == null)
            {
                Log.D(Tag, String.Format("{0}: {1} has no login parameters, so skipping login", this, Authenticator));
                FetchRemoteCheckpointDoc();
                return;
            }

            var loginPath = Authenticator.LoginPathForSite(RemoteUrl);
            Log.D(Tag, string.Format("{0}: Doing login with {1} at {2}", this, Authenticator.GetType(), loginPath));

            Log.D(Tag, string.Format("{0} | {1} : login() calling asyncTaskStarted()", this, Sharpen.SharpenThread.CurrentThread()));
            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Post, loginPath, loginParameters, (result, e) => {
                try
                {
                    if (e != null)
                    {
                        Log.D (Tag, string.Format ("{0}: Login failed for path: {1}", this, loginPath));
                        SetLastError(e);
                    }
                    else
                    {
                        Log.D (Tag, string.Format ("{0}: Successfully logged in!", this));
                        FetchRemoteCheckpointDoc ();
                    }
                }
                finally
                {
                    Log.D(Tag, "login() calling asyncTaskFinished()");
                    AsyncTaskFinished (1);
                }
            });
        }

        internal void FetchRemoteCheckpointDoc()
        {
            lastSequenceChanged = false;
            var checkpointId = RemoteCheckpointDocID();
            var localLastSequence = LocalDatabase.LastSequenceWithCheckpointId(checkpointId);

            Log.D(Tag, "fetchRemoteCheckpointDoc() calling asyncTaskStarted()");

            AsyncTaskStarted();

            SendAsyncRequest(HttpMethod.Get, "/_local/" + checkpointId, null, (response, e) => {
                try
                {
                    if (e != null && !Is404 (e)) {
                        Log.D (Tag, " error getting remote checkpoint: " + e);
                        SetLastError(e);
                    } else {
                        if (e != null && Is404 (e)) {
                            Log.D (Tag, " 404 error getting remote checkpoint " + RemoteCheckpointDocID () + ", calling maybeCreateRemoteDB");
                            MaybeCreateRemoteDB ();
                        }

                        IDictionary<string, object> result = null;

                        if (response != null) {
                            var responseData = (JObject)response;
                            result = responseData.ToObject<IDictionary<string, object>>();
                            remoteCheckpoint = result;
                        }
                        remoteCheckpoint = result;
                        string remoteLastSequence = null;

                        if (result != null) {
                            remoteLastSequence = (string)result.Get("lastSequence");
                        }

                        if (remoteLastSequence != null && remoteLastSequence.Equals (localLastSequence)) {
                            LastSequence = localLastSequence;
                            Log.V (Tag, "Replicating from lastSequence=" + LastSequence);
                        } else {
                            Log.V (Tag, "lastSequence mismatch: I had " + localLastSequence + ", remote had " + remoteLastSequence);
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
                    Log.D(Tag, "fetchRemoteCheckpointDoc() calling asyncTaskFinished()");
                    AsyncTaskFinished (1);
                }
            });
        }

        private static bool Is404(Exception e)
        {
            var result = false;
            if (e is Couchbase.Lite.HttpResponseException)
                return ((HttpResponseException)e).StatusCode == HttpStatusCode.NotFound;
            return (e is HttpResponseException) && ((HttpResponseException)e).StatusCode == HttpStatusCode.NotFound;
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
                Log.D(Tag + ".AsyncTaskStarted", "asyncTaskCount: " + asyncTaskCount);
                if (asyncTaskCount++ == 0)
                {
                    UpdateActive();
                }
                Log.D(Tag + ".AsyncTaskStarted", "updated to " + asyncTaskCount);
            }
        }

        internal void AsyncTaskFinished(Int32 numTasks)
        {
            var cancel = CancellationTokenSource.IsCancellationRequested;
            if (cancel)
                return;

            lock (asyncTaskLocker)
            {
                Log.D(Tag + ".AsyncTaskFinished", "AsyncTaskCount: {0} > {1}".Fmt(asyncTaskCount, asyncTaskCount - numTasks));
                asyncTaskCount -= numTasks;
                Debug.Assert(asyncTaskCount >= 0);
                if (asyncTaskCount == 0)
                {
                    UpdateActive();
                }
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
                    Log.W(Tag, "batcher object is null");
                }

                var newActive = batcherCount > 0 || asyncTaskCount > 0;
                if (active != newActive)
                {
                    Log.D(Tag, "Progress: set active = " + newActive + " asyncTaskCount: " + asyncTaskCount + " batcherCount: " + batcherCount );
                    active = newActive;
                    NotifyChangeListeners();

                    if (!active)
                    {
                        if (!continuous)
                        {
                            Log.D(Tag, "since !continuous, calling Stopped()");
                            WorkExecutor.StartNew(Stopping);
                        }
                        else if (LastError != null) /*(revisionsFailed > 0)*/
                        {
                            string msg = string.Format("{0}: Failed to xfer {1} revisions, will retry in {2} sec", this, revisionsFailed, RetryDelay);
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

        internal virtual void Stopping()
        {
            Log.V(Tag, "STOPPING");

            IsRunning = false;

            NotifyChangeListeners();

            SaveLastSequence();

            Log.V(Tag, "Set batcher to null");

            Batcher = null;

            if (LocalDatabase != null)
            {
                var reachabilityManager = LocalDatabase.Manager.NetworkReachabilityManager;
                if (reachabilityManager != null)
                {
                    reachabilityManager.StopListening();
                }
            }

            ClearDbRef();

            Log.V(Tag, "STOPPED");
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

            Log.D(Tag, "saveLastSequence() called. lastSequence: " + LastSequence);

            var body = new Dictionary<String, Object>();
            if (remoteCheckpoint != null)
            {
                body.PutAll(remoteCheckpoint);
            }
            body["lastSequence"] = LastSequence;
            var remoteCheckpointDocID = RemoteCheckpointDocID();
            if (String.IsNullOrEmpty(remoteCheckpointDocID))
            {
                Log.W(Tag, "remoteCheckpointDocID is null, aborting saveLastSequence()");
                return;
            }

            savingCheckpoint = true;
            //Log.D(Tag, "put remote _local document.  checkpointID: " + remoteCheckpointDocID);
            SendAsyncRequest(HttpMethod.Put, "/_local/" + remoteCheckpointDocID, body, (result, e) => {
                savingCheckpoint = false;
                if (e != null)
                {
                    Log.V (Tag, "Unable to save remote checkpoint", e as HttpResponseException);
                }

                if (LocalDatabase == null)
                {
                    Log.W(Tag, "Database is null, ignoring remote checkpoint response");
                    return;
                }

                if (!LocalDatabase.Open())
                {
                    Log.W(Tag, "Database is closed, ignoring remote checkpoint response");
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
                    Log.D(Tag, "Save checkpoint response: " + result.ToString());
                    var response = ((JObject)result).ToObject<IDictionary<string, object>>();
                    body.Put ("_rev", response.Get ("rev"));
                    remoteCheckpoint = body;
                    LocalDatabase.SetLastSequence(LastSequence, RemoteCheckpointDocID(), !IsPull);
                }

                if (overdueForSave) {
                    SaveLastSequence ();
                }
            });
        }

        internal void SendAsyncRequest(HttpMethod method, string relativePath, object body, RemoteRequestCompletionBlock completionHandler, CancellationTokenSource requestTokenSource = null)
        {
            try
            {
                var urlStr = BuildRelativeURLString(relativePath);
                var url = new Uri(urlStr);
                SendAsyncRequest(method, url, body, completionHandler, requestTokenSource);
            }
            catch (UriFormatException e)
            {
                Log.E(Tag, "Malformed URL for async request", e);
                throw;
            }
            catch (Exception e)
            {
                Log.E(Tag, "Unhandled exception", e);
                throw;
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

        internal void SendAsyncRequest(HttpMethod method, Uri url, Object body, RemoteRequestCompletionBlock completionHandler, CancellationTokenSource requestTokenSource = null)
        {
            var message = new HttpRequestMessage(method, url);
            var mapper = Manager.GetObjectMapper();
            message.Headers.Add("Accept", new[] { "multipart/related", "application/json" });

            var client = clientFactory.GetHttpClient();

            var authHeader = AuthUtils.GetAuthenticationHeaderValue(Authenticator, message.RequestUri);
            if (authHeader != null)
            {
                client.DefaultRequestHeaders.Authorization = authHeader;
            }

            if (body != null)
            {
                var bytes = mapper.WriteValueAsBytes(body).ToArray();
                var byteContent = new ByteArrayContent(bytes);
                message.Content = byteContent;
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            var token = requestTokenSource != null 
                ? requestTokenSource.Token
                : CancellationTokenSource.Token;

            Log.D(Tag, "Sending async {0} request to: {1}", method, url);
            client.SendAsync(message, token)
                .ContinueWith(response =>
                {
                    try 
                    {
                        lock(requests)
                        {
                            requests.Remove(client);
                        }
                        HttpResponseMessage result = null;
                        Exception error = null;
                        if (!response.IsFaulted)
                        {
                            result = response.Result;
                            UpdateServerType(result);
                        }
                        else
                        {
                            error = response.Exception.InnerException;
                            Log.E(Tag, "Http Message failed to send: {0}", message);
                            Log.E(Tag, "Http exception", response.Exception.InnerException);
                            if (message.Content != null) {
                                Log.E(Tag, "\tFailed content: {0}", message.Content.ReadAsStringAsync().Result);
                            }
                        }

                        if (completionHandler != null)
                        {
                            object fullBody = null;

                            try
                            {
                                if (response.Status != TaskStatus.RanToCompletion) {
                                    Log.D(Tag, "SendAsyncRequest did not run to completion.", response.Exception);
                                }

                                error = error is AggregateException
                                    ? response.Exception.Flatten()
                                    : response.Exception;

                                if (error == null )
                                {
                                    if (!result.IsSuccessStatusCode) 
                                    {
                                        result = response.Result;
                                        error = new HttpResponseException(result.StatusCode);
                                    }
                                }

                                if (error == null)
                                {
                                    var content = result.Content;
                                    if (content != null)
                                    {
                                        fullBody = mapper.ReadValue<object>(content.ReadAsStreamAsync().Result);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                error = e;
                                Log.E(Tag, "SendAsyncRequest has an error occurred.", e);
                            }

                            if (response.Status == TaskStatus.Canceled)
                            {
                                fullBody = null;
                                error = new Exception("SendAsyncRequest Task has been canceled.");
                            }

                            completionHandler(fullBody, error);
                        }

                        return result;
                    }
                    finally
                    {
                        client.Dispose();
                    }
                }, token, TaskContinuationOptions.None, WorkExecutor.Scheduler);

            lock(requests)
            {
                requests.Add(client);
            }
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

                var client = clientFactory.GetHttpClient();

                var authHeader = AuthUtils.GetAuthenticationHeaderValue(Authenticator, message.RequestUri);
                if (authHeader != null)
                {
                    client.DefaultRequestHeaders.Authorization = authHeader;
                }

                client.SendAsync(message, CancellationTokenSource.Token).ContinueWith(new Action<Task<HttpResponseMessage>>(responseMessage =>
                {
                    object fullBody = null;
                    Exception error = null;
                    try
                    {
                        var response = responseMessage.Result;
                        // add in cookies to global store
                        //CouchbaseLiteHttpClientFactory.Instance.AddCookies(clientFactory.HttpHandler.CookieContainer.GetCookies(url));

                        var status = response.StatusCode;
                        if ((Int32)status.GetStatusCode() >= 300)
                        {
                            Log.E(Tag, "Got error " + Sharpen.Extensions.ToString(status.GetStatusCode()));
                            Log.E(Tag, "Request was for: " + message);
                            Log.E(Tag, "Status reason: " + response.ReasonPhrase);
                            error = new WebException(response.ReasonPhrase);
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
                                    var reader = new MultipartDocumentReader(LocalDatabase);
                                    var contentType = contentTypeHeader.ToString();
                                    reader.SetContentType(contentType);

                                    var inputStreamTask = entity.ReadAsStreamAsync();
                                    //inputStreamTask.Wait(90000, CancellationTokenSource.Token);
                                    inputStream = inputStreamTask.Result;

                                    const int bufLen = 1024;
                                    var buffer = new byte[bufLen];

                                    int numBytesRead = 0;
                                    while ((numBytesRead = inputStream.Read(buffer)) != -1)
                                    {
                                        if (numBytesRead != bufLen)
                                        {
                                            var bufferToAppend = new ArraySegment<Byte>(buffer, 0, numBytesRead);
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
                                catch (Exception ex)
                                {
                                    Log.E(Tag, "SendAsyncMultipartDownloaderRequest has an error occurred.", ex);
                                }
                                finally
                                {
                                    try
                                    {
                                        inputStream.Close();
                                    }
                                    catch (Exception) { }
                                }
                            }
                            else
                            {
                                if (entity != null)
                                {
                                    try
                                    {
                                        var readTask = entity.ReadAsStreamAsync();
                                        //readTask.Wait(); // TODO: This should be scaled based on content length.
                                        inputStream = readTask.Result;
                                        fullBody = Manager.GetObjectMapper().ReadValue<Object>(inputStream);
                                        if (onCompletion != null)
                                            onCompletion(fullBody, error);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.E(Tag, "SendAsyncMultipartDownloaderRequest has an error occurred.", ex);
                                    }
                                    finally
                                    {
                                        try
                                        {
                                            inputStream.Close();
                                        }
                                        catch (Exception) { }
                                    }
                                }
                            }
                        }
                    }
                    catch (ProtocolViolationException e)
                    {
                        Log.E(Tag, "client protocol exception", e);
                        error = e;
                    }
                    catch (IOException e)
                    {
                        Log.E(Tag, "IO Exception", e);
                        error = e;
                    }
                    finally
                    {
                        client.Dispose();
                    }
                }), WorkExecutor.Scheduler);
            }
            catch (UriFormatException e)
            {
                Log.E(Tag, "Malformed URL for async request", e);
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

            var client = clientFactory.GetHttpClient();

            var authHeader = AuthUtils.GetAuthenticationHeaderValue(Authenticator, message.RequestUri);
            if (authHeader != null)
            {
                client.DefaultRequestHeaders.Authorization = authHeader;
            }

            client.SendAsync(message, CancellationTokenSource.Token)
                .ContinueWith(response=> {
                    if (response.Status != TaskStatus.RanToCompletion)
                    {
                        Log.E(Tag, "SendAsyncRequest did not run to completion.", response.Exception);
                        client.Dispose();
                        return null;
                    }
                    if ((Int32)response.Result.StatusCode > 300) {
                        SetLastError(new HttpResponseException(response.Result.StatusCode));
                        Log.E(Tag, "Server returned HTTP Error", LastError);
                        client.Dispose();
                        return null;
                    }
                    return response.Result.Content.ReadAsStreamAsync();
                }, CancellationTokenSource.Token)
                .ContinueWith(response=> {
                    try 
                    {
                        var hasEmptyResult = response.Result == null || response.Result.Result == null || response.Result.Result.Length == 0;
                        if (response.Status != TaskStatus.RanToCompletion) {
                            Log.E (Tag, "SendAsyncRequest did not run to completion.", response.Exception);
                        } else if (hasEmptyResult) {
                            Log.E (Tag, "Server returned an empty response.", response.Exception ?? LastError);
                        }
                        if (completionHandler != null) {
                            object fullBody = null;
                            if (!hasEmptyResult)
                            {
                                var mapper = Manager.GetObjectMapper();
                                fullBody = mapper.ReadValue<Object> (response.Result.Result);
                            }
                            completionHandler (fullBody, response.Exception);
                        }
                    }
                    finally
                    {
                        client.Dispose();
                    }
                }, CancellationTokenSource.Token);
        }

        internal void UpdateServerType(HttpResponseMessage response)
        {
            var server = response.Headers.Server;
            if (server != null && server.Any())
            {
				ServerType = String.Join(" ", server.Select(pi => pi.Product).Where(pi => pi != null));
                Log.V(Tag, "Server Version: " + ServerType);
            }
        }

        protected internal bool CheckServerCompatVersion(string minVersion)
        {
            if (String.IsNullOrWhiteSpace(ServerType))
            {
                return false;
            }

            const string prefix = "Couchbase Sync Gateway/";
            if (ServerType.StartsWith (prefix, StringComparison.Ordinal))
            {
                var version = ServerType.Substring (prefix.Length);
                return string.Compare (version, minVersion, StringComparison.Ordinal) >= 0;
            }

            return false;
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
            if (remoteCheckpointDocID != null) 
            {
                return remoteCheckpointDocID;
            }
            else
            {
                // TODO: Needs to be consistent with -hasSameSettingsAs: --
                // TODO: If a.remoteCheckpointID == b.remoteCheckpointID then [a hasSameSettingsAs: b]

                if (LocalDatabase == null) {
                    return null;
                }

                // canonicalization: make sure it produces the same checkpoint id regardless of
                // ordering of filterparams / docids
                IDictionary<String, Object> filterParamsCanonical = null;
                if (FilterParams != null) {
                    filterParamsCanonical = new SortedDictionary<String, Object>(FilterParams);
                }

                List<String> docIdsSorted = null;
                if (DocIds != null) {
                    docIdsSorted = new List<String>(DocIds);
                    docIdsSorted.Sort();
                }

                // use a treemap rather than a dictionary for purposes of canonicalization
                var spec = new SortedDictionary<String, Object>();
                spec.Put("localUUID", LocalDatabase.PrivateUUID());
                spec.Put("remoteURL", RemoteUrl.AbsoluteUri);
                spec.Put("push", !IsPull);
                spec.Put("continuous", Continuous);

                if (Filter != null) {
                    spec.Put("filter", Filter);
                }
                if (filterParamsCanonical != null) {
                    spec.Put("filterParams", filterParamsCanonical);
                }
                if (docIdsSorted != null) {
                    spec.Put("docids", docIdsSorted);
                }

                IEnumerable<byte> inputBytes = null;
                try {
                    inputBytes = Manager.GetObjectMapper().WriteValueAsBytes(spec);
                } catch (IOException e) {
                    throw new RuntimeException(e);
                }

                remoteCheckpointDocID = Misc.HexSHA1Digest(inputBytes);
                return remoteCheckpointDocID;

            }
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
            Log.D(Tag, "Refreshing remote checkpoint to get its _rev...");
            savingCheckpoint = true;

            Log.D(Tag, "RefreshRemoteCheckpointDoc() calling asyncTaskStarted()");
            AsyncTaskStarted();

            SendAsyncRequest(HttpMethod.Get, "/_local/" + RemoteCheckpointDocID(), null, (result, e) =>
            {
                try
                {
                    if (LocalDatabase == null)
                    {
                        Log.W(Tag, "db == null while refreshing remote checkpoint.  aborting");
                        return;
                    }

                    savingCheckpoint = false;

                    if (e != null && GetStatusFromError(e) != StatusCode.NotFound)
                    {
                        Log.E(Tag, "Error refreshing remote checkpoint", e);
                    }
                    else
                    {
                        Log.D(Tag, "Refreshed remote checkpoint: " + result);
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

        internal protected void RevisionFailed()
        {
            revisionsFailed++;
        }

        protected internal Status StatusFromBulkDocsResponseItem(IDictionary<string, object> item)
        {
            try
            {
                if (!item.ContainsKey("error"))
                {
                    return new Status(StatusCode.Ok);
                }

                var errorStr = item.Get("error") as string;
                if (errorStr == null || errorStr.IsEmpty())
                {
                    return new Status(StatusCode.Ok);
                }

                // 'status' property is nonstandard; TouchDB returns it, others don't.
                var statusString = item.Get("status") as string;
                if (String.IsNullOrWhiteSpace(statusString))
                {
                    var status = Convert.ToInt32(statusString);
                    if (status >= 400)
                    {
                        return new Status((StatusCode)status);
                    }
                }

                // If no 'status' present, interpret magic hardcoded CouchDB error strings:
                if (errorStr.Equals("unauthorized", StringComparison.InvariantCultureIgnoreCase))
                {
                    return new Status(StatusCode.Unauthorized);
                }
                else
                {
                    if (errorStr.Equals("forbidden", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return new Status(StatusCode.Forbidden);
                    }
                    else
                    {
                        if (errorStr.Equals("conflict", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return new Status(StatusCode.Conflict);
                        }
                        else
                        {
                            return new Status(StatusCode.UpStreamError);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.E(Database.Tag, "Exception getting status from " + item, e);
            }
            return new Status(StatusCode.Ok);
        }



        internal RevisionInternal TransformRevision(RevisionInternal rev)
        {
            if (revisionBodyTransformationFunction != null)
            {
                try
                {
                    var generation = rev.GetGeneration();
                    var xformed = revisionBodyTransformationFunction(rev);
                    if (xformed == null)
                    {
                        return null;
                    }

                    if (xformed != rev)
                    {
                        Debug.Assert((xformed.GetDocId().Equals(rev.GetDocId())));
                        Debug.Assert((xformed.GetRevId().Equals(rev.GetRevId())));
                        Debug.Assert((xformed.GetProperties().Get("_revisions").Equals(rev.GetProperties().Get("_revisions"))));

                        if (xformed.GetProperties().ContainsKey("_attachments"))
                        {
                            // Insert 'revpos' properties into any attachments added by the callback:
                            var mx = new RevisionInternal(xformed.GetProperties(), LocalDatabase);
                            xformed = mx;
                            mx.MutateAttachments((name, info) => {
                                if (info.Get("revpos") != null)
                                {
                                    return info;
                                }

                                if (info.Get("data") == null)
                                {
                                    throw new InvalidOperationException("Transformer added attachment without adding data");
                                }

                                var newInfo = new Dictionary<string, object>(info);
                                newInfo["revpos"] = generation;
                                return newInfo;
                            });
                        }
                    }
                }
                catch(Exception e)
                {
                    Log.W(Tag, String.Format("Exception transforming a revision of doc '{0}'", rev.GetDocId()), e);
                }
            }
            return rev;
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
                Log.D(Tag, "RETRYING, to transfer missed revisions...");
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

        private void SetupRevisionBodyTransformationFunction()
        {
            var xformer = TransformationFunction;
            if (xformer != null)
            {
                revisionBodyTransformationFunction = (rev) =>
                {
                    var properties = rev.GetProperties();

                    var xformedProperties = xformer(properties);
                    if (xformedProperties == null) 
                    {
                        return null;
                    }
                    if (xformedProperties != properties) {
                        Debug.Assert (xformedProperties != null);
                        Debug.Assert (xformedProperties ["_id"].Equals (properties ["_id"]));
                        Debug.Assert (xformedProperties ["_rev"].Equals (properties ["_rev"]));

                        var nuRev = new RevisionInternal (rev.GetProperties (), LocalDatabase);
                        nuRev.SetProperties (xformedProperties);
                        return nuRev;
                    }
                    return rev;
                };
            }
        }

    #endregion

    #region Instance Members

        public PropertyTransformationDelegate TransformationFunction { get; set; }

        /// <summary>
        /// Gets the local <see cref="Couchbase.Lite.Database"/> being replicated to/from.
        /// </summary>
        /// <value>The local <see cref="Couchbase.Lite.Database"/> being replicated to/from.</value>
        public Database LocalDatabase { get; private set; }

        /// <summary>
        /// Gets the remote URL being replicated to/from.
        /// </summary>
        /// <value>The remote URL being replicated to/from.</value>
        public Uri RemoteUrl { get; private set; }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Replication"/> pulls from,
        /// as opposed to pushes to, the target.
        /// </summary>
        /// <value>
        /// <c>true</c> if the <see cref="Couchbase.Lite.Replication"/>
        /// is pull; otherwise, <c>false</c>.
        /// </value>
        public abstract Boolean IsPull { get; }

        /// <summary>
        /// Gets or sets whether the target <see cref="Couchbase.Lite.Database"/> should be created
        /// if it doesn't already exist. This only has an effect if the target supports it.
        /// </summary>
        /// <value><c>true</c> if the target <see cref="Couchbase.Lite.Database"/> should be created if
        /// it doesn't already exist; otherwise, <c>false</c>.</value>
        public abstract Boolean CreateTarget { get; set; }

        /// <summary>
        /// Gets or sets whether the <see cref="Couchbase.Lite.Replication"/> operates continuously,
        /// replicating changes as the source <see cref="Couchbase.Lite.Database"/> is modified.
        /// </summary>
        /// <value><c>true</c> if continuous; otherwise, <c>false</c>.</value>
        public Boolean Continuous
        {
            get { return continuous; }
            set { if (!IsRunning) continuous = value; }
        }

        /// <summary>
        /// Gets or sets the name of an optional filter function to run on the source
        /// <see cref="Couchbase.Lite.Database"/>. Only documents for which the function
        /// returns true are replicated.
        /// </summary>
        /// <value>
        /// The name of an optional filter function to run on the source
        /// <see cref="Couchbase.Lite.Database"/>.
        /// </value>
        public String Filter { get; set; }

        /// <summary>
        /// Gets or sets the parameters to pass to the filter function.
        /// </summary>
        /// <value>The parameters to pass to the filter function.</value>
        public IDictionary<String, Object> FilterParams { get; set; }

        /// <summary>
        /// Gets or sets the list of Sync Gateway channel names to filter by for pull <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        /// <remarks>
        /// Gets or sets the list of Sync Gateway channel names to filter by for pull <see cref="Couchbase.Lite.Replication"/>.
        /// A null value means no filtering, and all available channels will be replicated.
        /// Only valid for pull replications whose source database is on a Couchbase Sync Gateway server.
        /// This is a convenience property that just sets the values of filter and filterParams.
        /// </remarks>
        /// <value>The list of Sync Gateway channel names to filter by for pull <see cref="Couchbase.Lite.Replication"/>.</value>
        public IEnumerable<String> Channels {
            get
            {
                if (FilterParams == null || FilterParams.IsEmpty())
                {
                    return new List<string>();
                }

                var p = FilterParams.ContainsKey(ChannelsQueryParam)
                    ? (string)FilterParams[ChannelsQueryParam]
                    : null;
                if (!IsPull || Filter == null || !Filter.Equals(ByChannelFilterName) || p == null || p.IsEmpty())
                {
                    return new List<string>();
                }

                var pArray = p.Split(new Char[] {','});
                return pArray.ToList<string>();
            }
            set
            {
                if (value != null && value.Any())
                {
                    if (!IsPull)
                    {
                        Log.W(Tag, "filterChannels can only be set in pull replications");
                        return;
                    }

                    Filter = ByChannelFilterName;
                    var filterParams = new Dictionary<string, object>();
                    filterParams.Put(ChannelsQueryParam, String.Join(",", value));
                    FilterParams = filterParams;
                }
                else if (Filter != null && Filter.Equals(ByChannelFilterName))
                {
                    Filter = null;
                    FilterParams = null;
                }
            }
        }

        /// <summary>
        /// Gets or sets the ids of the <see cref="Couchbase.Lite.Document"/>s to replicate.
        /// </summary>
        /// <value>The ids of the <see cref="Couchbase.Lite.Document"/>s to replicate.</value>
        public abstract IEnumerable<String> DocIds { get; set; }

        /// <summary>
        /// Gets or sets the extra HTTP headers to send in <see cref="Couchbase.Lite.Replication"/>
        /// requests to the remote <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <value>
        /// the extra HTTP headers to send in <see cref="Couchbase.Lite.Replication"/> requests
        /// to the remote <see cref="Couchbase.Lite.Database"/>.
        /// </value>
        public abstract IDictionary<String, String> Headers { get; set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Replication"/>'s current status.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Replication"/>'s current status.</value>
        public ReplicationStatus Status { get; set; }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Replication"/> is running.
        /// Continuous <see cref="Couchbase.Lite.Replication"/>s never actually stop,
        /// instead they go idle waiting for new data to appear.
        /// </summary>
        /// <value>
        /// <c>true</c> if <see cref="Couchbase.Lite.Replication"/> is running; otherwise, <c>false</c>.
        /// </value>
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
        }

        /// <summary>
        /// If the <see cref="Couchbase.Lite.Replication"/> is active, gets the number of changes to be processed, otherwise 0.
        /// </summary>
        /// <value>The changes count.</value>
        public Int32 ChangesCount {
            get { return changesCount; }
        }

        /// <summary>
        /// Gets or sets the authenticator.
        /// </summary>
        /// <value>The authenticator.</value>
        public IAuthenticator Authenticator { get; set; }

        //Methods

        /// <summary>
        /// Starts the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public void Start()
        {
            Log.V(Tag, "Replication Start");

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

            SetupRevisionBodyTransformationFunction();

            sessionID = string.Format("repl{0:000}", ++lastSessionID);
            Log.V(Tag, "STARTING ...");
            IsRunning = true;
            LastSequence = null;

            CheckSession();

            var reachabilityManager = LocalDatabase.Manager.NetworkReachabilityManager;
            reachabilityManager.StatusChanged += (sender, e) =>
            {
                if (e.Status == NetworkReachabilityStatus.Reachable)
                {
                    GoOnline();
                }
                else
                {
                    GoOffline();
                }
            };
            reachabilityManager.StartListening();
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

            Log.V(Tag, "STOP...");

            continuous = false;

            if (Batcher != null)
            {
                Batcher.Clear(); // no sense processing any pending changes
            }

            StopRemoteRequests();

            CancelPendingRetryIfReady();

            if (LocalDatabase != null)
            {
                LocalDatabase.ForgetReplication(this);
            }

            if (IsRunning && asyncTaskCount <= 0)
            {
                Log.V(Tag, "calling stopped()");
                Stopping();
            }
            else
            {
                Log.V(Tag, "not calling stopped().  running: " + IsRunning + " asyncTaskCount: " + asyncTaskCount);
            }
        }

        /// <summary>
        /// Restarts the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public void Restart()
        {
            // TODO: add the "started" flag and check it here
            Stop();
            Start();
        }

        /// <summary>Sets an HTTP cookie for the Replication.</summary>
        /// <param name="name">The name of the cookie.</param>
        /// <param name="value">The value of the cookie.</param>
        /// <param name="path">The path attribute of the cookie.  If null or empty, will use remote.getPath()
        ///     </param>
        /// <param name="expirationDate">The expiration date of the cookie.</param>
        /// <param name="secure">Whether the cookie should only be sent using a secure protocol (e.g. HTTPS).
        ///     </param>
        /// <param name="httpOnly">(ignored) Whether the cookie should only be used when transmitting HTTP, or HTTPS, requests thus restricting access from other, non-HTTP APIs.
        ///     </param>
        public void SetCookie(string name, string value, string path, DateTime expirationDate, bool secure, bool httpOnly)
        {
            var cookie = new Cookie(name, value)
            {
                Expires = expirationDate,
                Secure = secure,
                HttpOnly = httpOnly,
                Domain = RemoteUrl.GetHost()
            };

            cookie.Path = !string.IsNullOrEmpty(path) 
                ? path 
                : RemoteUrl.PathAndQuery;

            var cookies = new CookieCollection { cookie };
            clientFactory.AddCookies(cookies);
        }

        public void DeleteCookie(String name)
        {
            clientFactory.DeleteCookie(RemoteUrl, name);
        }

        /// <summary>
        /// Adds or Removed a <see cref="Couchbase.Lite.Database"/> change delegate
        /// that will be called whenever the <see cref="Couchbase.Lite.Replication"/>
        /// changes.
        /// </summary>
        public event EventHandler<ReplicationChangeEventArgs> Changed;
    }
    #endregion

    #region EventArgs Subclasses

        ///
        /// <see cref="Couchbase.Lite.Replication"/> Change Event Arguments.
        ///
        public class ReplicationChangeEventArgs : EventArgs
        {
            //Properties
            /// <summary>
            /// Gets the <see cref="Couchbase.Lite.Replication"/> that raised the event.
            /// </summary>
            /// <value>The <see cref="Couchbase.Lite.Replication"/> that raised the event.</value>
            public Replication Source { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="Couchbase.Lite.ReplicationChangeEventArgs"/> class.
            /// </summary>
            /// <param name="sender">The <see cref="Couchbase.Lite.Replication"/> that raised the event.</param>
            public ReplicationChangeEventArgs (Replication sender)
            {
                Source = sender;
            }
        }

    #endregion

    #region Delegates

    public delegate IDictionary<string, object> PropertyTransformationDelegate(IDictionary<string, object> propertyBag);

    #endregion
}
