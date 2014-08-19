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
using System.Collections.Generic;
using System.IO;
using Apache.Http;
using Apache.Http.Client;
using Apache.Http.Entity.Mime;
using Apache.Http.Impl.Cookie;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Replicator
{
    /// <summary>A Couchbase Lite pull or push Replication between a local and a remote Database.
    ///     </summary>
    /// <remarks>A Couchbase Lite pull or push Replication between a local and a remote Database.
    ///     </remarks>
    public abstract class Replication : NetworkReachabilityListener
    {
        private static int lastSessionID = 0;

        protected internal bool continuous;

        protected internal string filterName;

        protected internal ScheduledExecutorService workExecutor;

        protected internal Database db;

        protected internal Uri remote;

        protected internal string lastSequence;

        protected internal bool lastSequenceChanged;

        protected internal IDictionary<string, object> remoteCheckpoint;

        protected internal bool savingCheckpoint;

        protected internal bool overdueForSave;

        protected internal bool running;

        protected internal bool active;

        protected internal Exception error;

        protected internal string sessionID;

        protected internal Batcher<RevisionInternal> batcher;

        protected internal int asyncTaskCount;

        protected internal AtomicInteger completedChangesCount;

        private AtomicInteger changesCount;

        protected internal bool online;

        protected internal HttpClientFactory clientFactory;

        private readonly IList<Replication.ChangeListener> changeListeners;

        protected internal IList<string> documentIDs;

        protected internal IDictionary<string, object> filterParams;

        protected internal ExecutorService remoteRequestExecutor;

        protected internal Authenticator authenticator;

        private Replication.ReplicationStatus status = Replication.ReplicationStatus.ReplicationStopped;

        protected internal IDictionary<string, object> requestHeaders;

        private int revisionsFailed;

        private ScheduledFuture retryIfReadyFuture;

        private readonly IDictionary<RemoteRequest, Future> requests;

        private string serverType;

        private string remoteCheckpointDocID;

        private CollectionUtils.Functor<IDictionary<string, object>, IDictionary<string, 
            object>> propertiesTransformationBlock;

        protected internal CollectionUtils.Functor<RevisionInternal, RevisionInternal> revisionBodyTransformationBlock;

        protected internal const int ProcessorDelay = 500;

        protected internal const int InboxCapacity = 100;

        protected internal const int RetryDelay = 60;

        protected internal const int ExecutorThreadPoolSize = 5;

        /// <exclude></exclude>
        public const string ByChannelFilterName = "sync_gateway/bychannel";

        /// <exclude></exclude>
        public const string ChannelsQueryParam = "channels";

        /// <exclude></exclude>
        public const string ReplicatorDatabaseName = "_replicator";

        /// <summary>Options for what metadata to include in document bodies</summary>
        public enum ReplicationStatus
        {
            ReplicationStopped,
            ReplicationOffline,
            ReplicationIdle,
            ReplicationActive
        }

        /// <summary>Private Constructor</summary>
        /// <exclude></exclude>
        [InterfaceAudience.Private]
        internal Replication(Database db, Uri remote, bool continuous, ScheduledExecutorService
             workExecutor) : this(db, remote, continuous, null, workExecutor)
        {
        }

        /// <summary>Private Constructor</summary>
        /// <exclude></exclude>
        [InterfaceAudience.Private]
        internal Replication(Database db, Uri remote, bool continuous, HttpClientFactory 
            clientFactory, ScheduledExecutorService workExecutor)
        {
            this.db = db;
            this.continuous = continuous;
            this.workExecutor = workExecutor;
            this.remote = remote;
            this.remoteRequestExecutor = Executors.NewFixedThreadPool(ExecutorThreadPoolSize);
            this.changeListeners = new CopyOnWriteArrayList<Replication.ChangeListener>();
            this.online = true;
            this.requestHeaders = new Dictionary<string, object>();
            this.requests = new ConcurrentHashMap<RemoteRequest, Future>();
            this.completedChangesCount = new AtomicInteger(0);
            this.changesCount = new AtomicInteger(0);
            if (remote.GetQuery() != null && !remote.GetQuery().IsEmpty())
            {
                URI uri = URI.Create(remote.ToExternalForm());
                string personaAssertion = URIUtils.GetQueryParameter(uri, PersonaAuthorizer.QueryParameter
                    );
                if (personaAssertion != null && !personaAssertion.IsEmpty())
                {
                    string email = PersonaAuthorizer.RegisterAssertion(personaAssertion);
                    PersonaAuthorizer authorizer = new PersonaAuthorizer(email);
                    SetAuthenticator(authorizer);
                }
                string facebookAccessToken = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QueryParameter
                    );
                if (facebookAccessToken != null && !facebookAccessToken.IsEmpty())
                {
                    string email = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QueryParameterEmail
                        );
                    FacebookAuthorizer authorizer = new FacebookAuthorizer(email);
                    Uri remoteWithQueryRemoved = null;
                    try
                    {
                        remoteWithQueryRemoved = new Uri(remote.Scheme, remote.GetHost(), remote.Port, remote
                            .AbsolutePath);
                    }
                    catch (UriFormatException e)
                    {
                        throw new ArgumentException(e);
                    }
                    FacebookAuthorizer.RegisterAccessToken(facebookAccessToken, email, remoteWithQueryRemoved
                        .ToExternalForm());
                    SetAuthenticator(authorizer);
                }
                // we need to remove the query from the URL, since it will cause problems when
                // communicating with sync gw / couchdb
                try
                {
                    this.remote = new Uri(remote.Scheme, remote.GetHost(), remote.Port, remote.AbsolutePath
                        );
                }
                catch (UriFormatException e)
                {
                    throw new ArgumentException(e);
                }
            }
            batcher = new Batcher<RevisionInternal>(workExecutor, InboxCapacity, ProcessorDelay
                , new _BatchProcessor_207(this));
            SetClientFactory(clientFactory);
        }

        private sealed class _BatchProcessor_207 : BatchProcessor<RevisionInternal>
        {
            public _BatchProcessor_207(Replication _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void Process(IList<RevisionInternal> inbox)
            {
                try
                {
                    Log.V(Log.TagSync, "*** %s: BEGIN processInbox (%d sequences)", this, inbox.Count
                        );
                    this._enclosing.ProcessInbox(new RevisionList(inbox));
                    Log.V(Log.TagSync, "*** %s: END processInbox (lastSequence=%s)", this, this._enclosing
                        .lastSequence);
                    Log.V(Log.TagSync, "%s: batcher calling updateActive()", this);
                    this._enclosing.UpdateActive();
                }
                catch (Exception e)
                {
                    Log.E(Log.TagSync, "ERROR: processInbox failed: ", e);
                    throw new RuntimeException(e);
                }
            }

            private readonly Replication _enclosing;
        }

        /// <summary>
        /// Set the HTTP client factory if one was passed in, or use the default
        /// set in the manager if available.
        /// </summary>
        /// <remarks>
        /// Set the HTTP client factory if one was passed in, or use the default
        /// set in the manager if available.
        /// </remarks>
        /// <param name="clientFactory"></param>
        [InterfaceAudience.Private]
        protected internal virtual void SetClientFactory(HttpClientFactory clientFactory)
        {
            Manager manager = null;
            if (this.db != null)
            {
                manager = this.db.GetManager();
            }
            HttpClientFactory managerClientFactory = null;
            if (manager != null)
            {
                managerClientFactory = manager.GetDefaultHttpClientFactory();
            }
            if (clientFactory != null)
            {
                this.clientFactory = clientFactory;
            }
            else
            {
                if (managerClientFactory != null)
                {
                    this.clientFactory = managerClientFactory;
                }
                else
                {
                    PersistentCookieStore cookieStore = db.GetPersistentCookieStore();
                    this.clientFactory = new CouchbaseLiteHttpClientFactory(cookieStore);
                }
            }
        }

        /// <summary>Get the local database which is the source or target of this replication
        ///     </summary>
        [InterfaceAudience.Public]
        public virtual Database GetLocalDatabase()
        {
            return db;
        }

        /// <summary>Get the remote URL which is the source or target of this replication</summary>
        [InterfaceAudience.Public]
        public virtual Uri GetRemoteUrl()
        {
            return remote;
        }

        /// <summary>Is this a pull replication?  (Eg, it pulls data from Sync Gateway -&gt; Device running CBL?)
        ///     </summary>
        [InterfaceAudience.Public]
        public abstract bool IsPull();

        /// <summary>Should the target database be created if it doesn't already exist? (Defaults to NO).
        ///     </summary>
        /// <remarks>Should the target database be created if it doesn't already exist? (Defaults to NO).
        ///     </remarks>
        [InterfaceAudience.Public]
        public abstract bool ShouldCreateTarget();

        /// <summary>Set whether the target database be created if it doesn't already exist?</summary>
        [InterfaceAudience.Public]
        public abstract void SetCreateTarget(bool createTarget);

        /// <summary>
        /// Should the replication operate continuously, copying changes as soon as the
        /// source database is modified? (Defaults to NO).
        /// </summary>
        /// <remarks>
        /// Should the replication operate continuously, copying changes as soon as the
        /// source database is modified? (Defaults to NO).
        /// </remarks>
        [InterfaceAudience.Public]
        public virtual bool IsContinuous()
        {
            return continuous;
        }

        /// <summary>Set whether the replication should operate continuously.</summary>
        /// <remarks>Set whether the replication should operate continuously.</remarks>
        [InterfaceAudience.Public]
        public virtual void SetContinuous(bool continuous)
        {
            if (!IsRunning())
            {
                this.continuous = continuous;
            }
        }

        /// <summary>Name of an optional filter function to run on the source server.</summary>
        /// <remarks>
        /// Name of an optional filter function to run on the source server. Only documents for
        /// which the function returns true are replicated.
        /// For a pull replication, the name looks like "designdocname/filtername".
        /// For a push replication, use the name under which you registered the filter with the Database.
        /// </remarks>
        [InterfaceAudience.Public]
        public virtual string GetFilter()
        {
            return filterName;
        }

        /// <summary>Set the filter to be used by this replication</summary>
        [InterfaceAudience.Public]
        public virtual void SetFilter(string filterName)
        {
            this.filterName = filterName;
        }

        /// <summary>Parameters to pass to the filter function.</summary>
        /// <remarks>Parameters to pass to the filter function.  Should map strings to strings.
        ///     </remarks>
        [InterfaceAudience.Public]
        public virtual IDictionary<string, object> GetFilterParams()
        {
            return filterParams;
        }

        /// <summary>Set parameters to pass to the filter function.</summary>
        /// <remarks>Set parameters to pass to the filter function.</remarks>
        [InterfaceAudience.Public]
        public virtual void SetFilterParams(IDictionary<string, object> filterParams)
        {
            this.filterParams = filterParams;
        }

        /// <summary>List of Sync Gateway channel names to filter by; a nil value means no filtering, i.e.
        ///     </summary>
        /// <remarks>
        /// List of Sync Gateway channel names to filter by; a nil value means no filtering, i.e. all
        /// available channels will be synced.  Only valid for pull replications whose source database
        /// is on a Couchbase Sync Gateway server.  (This is a convenience that just reads or
        /// changes the values of .filter and .query_params.)
        /// </remarks>
        [InterfaceAudience.Public]
        public virtual IList<string> GetChannels()
        {
            if (filterParams == null || filterParams.IsEmpty())
            {
                return new AList<string>();
            }
            string @params = (string)filterParams.Get(ChannelsQueryParam);
            if (!IsPull() || GetFilter() == null || !GetFilter().Equals(ByChannelFilterName) 
                || @params == null || @params.IsEmpty())
            {
                return new AList<string>();
            }
            string[] paramsArray = @params.Split(",");
            return new AList<string>(Arrays.AsList(paramsArray));
        }

        /// <summary>Set the list of Sync Gateway channel names</summary>
        [InterfaceAudience.Public]
        public virtual void SetChannels(IList<string> channels)
        {
            if (channels != null && !channels.IsEmpty())
            {
                if (!IsPull())
                {
                    Log.W(Log.TagSync, "filterChannels can only be set in pull replications");
                    return;
                }
                SetFilter(ByChannelFilterName);
                IDictionary<string, object> filterParams = new Dictionary<string, object>();
                filterParams.Put(ChannelsQueryParam, TextUtils.Join(",", channels));
                SetFilterParams(filterParams);
            }
            else
            {
                if (GetFilter().Equals(ByChannelFilterName))
                {
                    SetFilter(null);
                    SetFilterParams(null);
                }
            }
        }

        /// <summary>Extra HTTP headers to send in all requests to the remote server.</summary>
        /// <remarks>
        /// Extra HTTP headers to send in all requests to the remote server.
        /// Should map strings (header names) to strings.
        /// </remarks>
        [InterfaceAudience.Public]
        public virtual IDictionary<string, object> GetHeaders()
        {
            return requestHeaders;
        }

        /// <summary>Set Extra HTTP headers to be sent in all requests to the remote server.</summary>
        /// <remarks>Set Extra HTTP headers to be sent in all requests to the remote server.</remarks>
        [InterfaceAudience.Public]
        public virtual void SetHeaders(IDictionary<string, object> requestHeadersParam)
        {
            if (requestHeadersParam != null && !requestHeaders.Equals(requestHeadersParam))
            {
                requestHeaders = requestHeadersParam;
            }
        }

        /// <summary>Gets the documents to specify as part of the replication.</summary>
        /// <remarks>Gets the documents to specify as part of the replication.</remarks>
        [InterfaceAudience.Public]
        public virtual IList<string> GetDocIds()
        {
            return documentIDs;
        }

        /// <summary>Sets the documents to specify as part of the replication.</summary>
        /// <remarks>Sets the documents to specify as part of the replication.</remarks>
        [InterfaceAudience.Public]
        public virtual void SetDocIds(IList<string> docIds)
        {
            documentIDs = docIds;
        }

        /// <summary>The replication's current state, one of {stopped, offline, idle, active}.
        ///     </summary>
        /// <remarks>The replication's current state, one of {stopped, offline, idle, active}.
        ///     </remarks>
        [InterfaceAudience.Public]
        public virtual Replication.ReplicationStatus GetStatus()
        {
            return status;
        }

        /// <summary>The number of completed changes processed, if the task is active, else 0 (observable).
        ///     </summary>
        /// <remarks>The number of completed changes processed, if the task is active, else 0 (observable).
        ///     </remarks>
        [InterfaceAudience.Public]
        public virtual int GetCompletedChangesCount()
        {
            return completedChangesCount.Get();
        }

        /// <summary>The total number of changes to be processed, if the task is active, else 0 (observable).
        ///     </summary>
        /// <remarks>The total number of changes to be processed, if the task is active, else 0 (observable).
        ///     </remarks>
        [InterfaceAudience.Public]
        public virtual int GetChangesCount()
        {
            return changesCount.Get();
        }

        /// <summary>True while the replication is running, False if it's stopped.</summary>
        /// <remarks>
        /// True while the replication is running, False if it's stopped.
        /// Note that a continuous replication never actually stops; it only goes idle waiting for new
        /// data to appear.
        /// </remarks>
        [InterfaceAudience.Public]
        public virtual bool IsRunning()
        {
            return running;
        }

        /// <summary>
        /// The error status of the replication, or null if there have not been any errors since
        /// it started.
        /// </summary>
        /// <remarks>
        /// The error status of the replication, or null if there have not been any errors since
        /// it started.
        /// </remarks>
        [InterfaceAudience.Public]
        public virtual Exception GetLastError()
        {
            return error;
        }

        /// <summary>Starts the replication, asynchronously.</summary>
        /// <remarks>Starts the replication, asynchronously.</remarks>
        [InterfaceAudience.Public]
        public virtual void Start()
        {
            if (!db.IsOpen())
            {
                // Race condition: db closed before replication starts
                Log.W(Log.TagSync, "Not starting replication because db.isOpen() returned false."
                    );
                return;
            }
            if (running)
            {
                return;
            }
            db.AddReplication(this);
            db.AddActiveReplication(this);
            CollectionUtils.Functor<IDictionary<string, object>, IDictionary<string, object>>
                 xformer = propertiesTransformationBlock;
            if (xformer != null)
            {
                revisionBodyTransformationBlock = new _Functor_483(this, xformer);
            }
            this.sessionID = string.Format("repl%03d", ++lastSessionID);
            Log.V(Log.TagSync, "%s: STARTING ...", this);
            running = true;
            lastSequence = null;
            CheckSession();
            db.GetManager().GetContext().GetNetworkReachabilityManager().AddNetworkReachabilityListener
                (this);
        }

        private sealed class _Functor_483 : CollectionUtils.Functor<RevisionInternal, RevisionInternal
            >
        {
            public _Functor_483(Replication _enclosing, CollectionUtils.Functor<IDictionary<string
                , object>, IDictionary<string, object>> xformer)
            {
                this._enclosing = _enclosing;
                this.xformer = xformer;
            }

            public RevisionInternal Invoke(RevisionInternal rev)
            {
                IDictionary<string, object> properties = rev.GetProperties();
                IDictionary<string, object> xformedProperties = xformer.Invoke(properties);
                if (xformedProperties == null)
                {
                    rev = null;
                }
                else
                {
                    if (xformedProperties != properties)
                    {
                        System.Diagnostics.Debug.Assert((xformedProperties != null));
                        System.Diagnostics.Debug.Assert((xformedProperties.Get("_id").Equals(properties.Get
                            ("_id"))));
                        System.Diagnostics.Debug.Assert((xformedProperties.Get("_rev").Equals(properties.
                            Get("_rev"))));
                        RevisionInternal nuRev = new RevisionInternal(rev.GetProperties(), this._enclosing
                            .db);
                        nuRev.SetProperties(xformedProperties);
                        rev = nuRev;
                    }
                }
                return rev;
            }

            private readonly Replication _enclosing;

            private readonly CollectionUtils.Functor<IDictionary<string, object>, IDictionary
                <string, object>> xformer;
        }

        /// <summary>Stops replication, asynchronously.</summary>
        /// <remarks>Stops replication, asynchronously.</remarks>
        [InterfaceAudience.Public]
        public virtual void Stop()
        {
            if (!running)
            {
                return;
            }
            Log.V(Log.TagSync, "%s: STOPPING...", this);
            if (batcher != null)
            {
                batcher.Clear();
            }
            else
            {
                // no sense processing any pending changes
                Log.V(Log.TagSync, "%s: stop() called, not calling batcher.clear() since it's null"
                    , this);
            }
            continuous = false;
            StopRemoteRequests();
            CancelPendingRetryIfReady();
            if (db != null)
            {
                db.ForgetReplication(this);
            }
            else
            {
                Log.V(Log.TagSync, "%s: stop() called, not calling db.forgetReplication() since it's null"
                    , this);
            }
            if (running && asyncTaskCount <= 0)
            {
                Log.V(Log.TagSync, "%s: calling stopped()", this);
                Stopped();
            }
            else
            {
                Log.V(Log.TagSync, "%s: not calling stopped().  running: %s asyncTaskCount: %d", 
                    this, running, asyncTaskCount);
            }
        }

        /// <summary>Restarts a completed or failed replication.</summary>
        /// <remarks>Restarts a completed or failed replication.</remarks>
        [InterfaceAudience.Public]
        public virtual void Restart()
        {
            // TODO: add the "started" flag and check it here
            Stop();
            Start();
        }

        /// <summary>Adds a change delegate that will be called whenever the Replication changes.
        ///     </summary>
        /// <remarks>Adds a change delegate that will be called whenever the Replication changes.
        ///     </remarks>
        [InterfaceAudience.Public]
        public virtual void AddChangeListener(Replication.ChangeListener changeListener)
        {
            changeListeners.AddItem(changeListener);
        }

        /// <summary>Return a string representation of this replication.</summary>
        /// <remarks>
        /// Return a string representation of this replication.
        /// The credentials will be masked in order to avoid passwords leaking into logs.
        /// </remarks>
        [InterfaceAudience.Public]
        public override string ToString()
        {
            string maskedRemoteWithoutCredentials = (remote != null ? remote.ToExternalForm()
                 : string.Empty);
            maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@"
                , "://---:---@");
            string name = GetType().Name + "@" + Sharpen.Extensions.ToHexString(GetHashCode()
                ) + "[" + maskedRemoteWithoutCredentials + "]";
            return name;
        }

        /// <summary>Sets an HTTP cookie for the Replication.</summary>
        /// <remarks>Sets an HTTP cookie for the Replication.</remarks>
        /// <param name="name">The name of the cookie.</param>
        /// <param name="value">The value of the cookie.</param>
        /// <param name="path">The path attribute of the cookie.  If null or empty, will use remote.getPath()
        ///     </param>
        /// <param name="maxAge">The maxAge, in milliseconds, that this cookie should be valid for.
        ///     </param>
        /// <param name="secure">Whether the cookie should only be sent using a secure protocol (e.g. HTTPS).
        ///     </param>
        /// <param name="httpOnly">(ignored) Whether the cookie should only be used when transmitting HTTP, or HTTPS, requests thus restricting access from other, non-HTTP APIs.
        ///     </param>
        [InterfaceAudience.Public]
        public virtual void SetCookie(string name, string value, string path, long maxAge
            , bool secure, bool httpOnly)
        {
            DateTime now = new DateTime();
            DateTime expirationDate = Sharpen.Extensions.CreateDate(now.GetTime() + maxAge);
            SetCookie(name, value, path, expirationDate, secure, httpOnly);
        }

        /// <summary>Sets an HTTP cookie for the Replication.</summary>
        /// <remarks>Sets an HTTP cookie for the Replication.</remarks>
        /// <param name="name">The name of the cookie.</param>
        /// <param name="value">The value of the cookie.</param>
        /// <param name="path">The path attribute of the cookie.  If null or empty, will use remote.getPath()
        ///     </param>
        /// <param name="expirationDate">The expiration date of the cookie.</param>
        /// <param name="secure">Whether the cookie should only be sent using a secure protocol (e.g. HTTPS).
        ///     </param>
        /// <param name="httpOnly">(ignored) Whether the cookie should only be used when transmitting HTTP, or HTTPS, requests thus restricting access from other, non-HTTP APIs.
        ///     </param>
        [InterfaceAudience.Public]
        public virtual void SetCookie(string name, string value, string path, DateTime expirationDate
            , bool secure, bool httpOnly)
        {
            if (remote == null)
            {
                throw new InvalidOperationException("Cannot setCookie since remote == null");
            }
            BasicClientCookie2 cookie = new BasicClientCookie2(name, value);
            cookie.SetDomain(remote.GetHost());
            if (path != null && path.Length > 0)
            {
                cookie.SetPath(path);
            }
            else
            {
                cookie.SetPath(remote.AbsolutePath);
            }
            cookie.SetExpiryDate(expirationDate);
            cookie.SetSecure(secure);
            IList<Apache.Http.Cookie.Cookie> cookies = Arrays.AsList((Apache.Http.Cookie.Cookie
                )cookie);
            this.clientFactory.AddCookies(cookies);
        }

        /// <summary>Deletes an HTTP cookie for the Replication.</summary>
        /// <remarks>Deletes an HTTP cookie for the Replication.</remarks>
        /// <param name="name">The name of the cookie.</param>
        [InterfaceAudience.Public]
        public virtual void DeleteCookie(string name)
        {
            this.clientFactory.DeleteCookie(name);
        }

        /// <summary>
        /// The type of event raised by a Replication when any of the following
        /// properties change: mode, running, error, completed, total.
        /// </summary>
        /// <remarks>
        /// The type of event raised by a Replication when any of the following
        /// properties change: mode, running, error, completed, total.
        /// </remarks>
        public class ChangeEvent
        {
            private Replication source;

            public ChangeEvent(Replication source)
            {
                this.source = source;
            }

            public virtual Replication GetSource()
            {
                return source;
            }
        }

        /// <summary>A delegate that can be used to listen for Replication changes.</summary>
        /// <remarks>A delegate that can be used to listen for Replication changes.</remarks>
        public interface ChangeListener
        {
            void Changed(Replication.ChangeEvent @event);
        }

        /// <summary>Removes the specified delegate as a listener for the Replication change event.
        ///     </summary>
        /// <remarks>Removes the specified delegate as a listener for the Replication change event.
        ///     </remarks>
        [InterfaceAudience.Public]
        public virtual void RemoveChangeListener(Replication.ChangeListener changeListener
            )
        {
            changeListeners.Remove(changeListener);
        }

        /// <summary>Set the Authenticator used for authenticating with the Sync Gateway</summary>
        [InterfaceAudience.Public]
        public virtual void SetAuthenticator(Authenticator authenticator)
        {
            this.authenticator = authenticator;
        }

        /// <summary>Get the Authenticator used for authenticating with the Sync Gateway</summary>
        [InterfaceAudience.Public]
        public virtual Authenticator GetAuthenticator()
        {
            return authenticator;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void DatabaseClosing()
        {
            SaveLastSequence();
            Stop();
            ClearDbRef();
        }

        /// <summary>
        /// If we're in the middle of saving the checkpoint and waiting for a response, by the time the
        /// response arrives _db will be nil, so there won't be any way to save the checkpoint locally.
        /// </summary>
        /// <remarks>
        /// If we're in the middle of saving the checkpoint and waiting for a response, by the time the
        /// response arrives _db will be nil, so there won't be any way to save the checkpoint locally.
        /// To avoid that, pre-emptively save the local checkpoint now.
        /// </remarks>
        /// <exclude></exclude>
        private void ClearDbRef()
        {
            if (savingCheckpoint && lastSequence != null && db != null)
            {
                db.SetLastSequence(lastSequence, RemoteCheckpointDocID(), !IsPull());
                db = null;
            }
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual string GetLastSequence()
        {
            return lastSequence;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void SetLastSequence(string lastSequenceIn)
        {
            if (lastSequenceIn != null && !lastSequenceIn.Equals(lastSequence))
            {
                Log.V(Log.TagSync, "%s: Setting lastSequence to %s from(%s)", this, lastSequenceIn
                    , lastSequence);
                lastSequence = lastSequenceIn;
                if (!lastSequenceChanged)
                {
                    lastSequenceChanged = true;
                    workExecutor.Schedule(new _Runnable_729(this), 2 * 1000, TimeUnit.Milliseconds);
                }
            }
        }

        private sealed class _Runnable_729 : Runnable
        {
            public _Runnable_729(Replication _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void Run()
            {
                this._enclosing.SaveLastSequence();
            }

            private readonly Replication _enclosing;
        }

        [InterfaceAudience.Private]
        internal virtual void AddToCompletedChangesCount(int delta)
        {
            int previousVal = this.completedChangesCount.GetAndAdd(delta);
            Log.V(Log.TagSync, "%s: Incrementing completedChangesCount count from %s by adding %d -> %d"
                , this, previousVal, delta, completedChangesCount.Get());
            NotifyChangeListeners();
        }

        [InterfaceAudience.Private]
        internal virtual void AddToChangesCount(int delta)
        {
            int previousVal = this.changesCount.GetAndAdd(delta);
            if (changesCount.Get() < 0)
            {
                Log.W(Log.TagSync, "Changes count is negative, this could indicate an error");
            }
            Log.V(Log.TagSync, "%s: Incrementing changesCount count from %s by adding %d -> %d"
                , this, previousVal, delta, changesCount.Get());
            NotifyChangeListeners();
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual string GetSessionID()
        {
            return sessionID;
        }

        [InterfaceAudience.Private]
        protected internal virtual void CheckSession()
        {
            // REVIEW : This is not in line with the iOS implementation
            if (GetAuthenticator() != null && ((AuthenticatorImpl)GetAuthenticator()).UsesCookieBasedLogin
                ())
            {
                CheckSessionAtPath("/_session");
            }
            else
            {
                FetchRemoteCheckpointDoc();
            }
        }

        [InterfaceAudience.Private]
        protected internal virtual void CheckSessionAtPath(string sessionPath)
        {
            Log.V(Log.TagSync, "%s | %s: checkSessionAtPath() calling asyncTaskStarted()", this
                , Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            SendAsyncRequest("GET", sessionPath, null, new _RemoteRequestCompletionBlock_782(
                this, sessionPath));
        }

        private sealed class _RemoteRequestCompletionBlock_782 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_782(Replication _enclosing, string sessionPath
                )
            {
                this._enclosing = _enclosing;
                this.sessionPath = sessionPath;
            }

            public void OnCompletion(object result, Exception error)
            {
                try
                {
                    if (error != null)
                    {
                        // If not at /db/_session, try CouchDB location /_session
                        if (error is HttpResponseException && ((HttpResponseException)error).GetStatusCode
                            () == 404 && Sharpen.Runtime.EqualsIgnoreCase(sessionPath, "/_session"))
                        {
                            this._enclosing.CheckSessionAtPath("_session");
                            return;
                        }
                        Log.E(Log.TagSync, this + ": Session check failed", error);
                        this._enclosing.SetError(error);
                    }
                    else
                    {
                        IDictionary<string, object> response = (IDictionary<string, object>)result;
                        IDictionary<string, object> userCtx = (IDictionary<string, object>)response.Get("userCtx"
                            );
                        string username = (string)userCtx.Get("name");
                        if (username != null && username.Length > 0)
                        {
                            Log.D(Log.TagSync, "%s Active session, logged in as %s", this, username);
                            this._enclosing.FetchRemoteCheckpointDoc();
                        }
                        else
                        {
                            Log.D(Log.TagSync, "%s No active session, going to login", this);
                            this._enclosing.Login();
                        }
                    }
                }
                finally
                {
                    Log.V(Log.TagSync, "%s | %s: checkSessionAtPath() calling asyncTaskFinished()", this
                        , Sharpen.Thread.CurrentThread());
                    this._enclosing.AsyncTaskFinished(1);
                }
            }

            private readonly Replication _enclosing;

            private readonly string sessionPath;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public abstract void BeginReplicating();

        [InterfaceAudience.Private]
        protected internal virtual void Stopped()
        {
            Log.V(Log.TagSync, "%s: STOPPED", this);
            running = false;
            NotifyChangeListeners();
            SaveLastSequence();
            batcher = null;
            if (db != null)
            {
                db.GetManager().GetContext().GetNetworkReachabilityManager().RemoveNetworkReachabilityListener
                    (this);
            }
            ClearDbRef();
        }

        // db no longer tracks me so it won't notify me when it closes; clear ref now
        [InterfaceAudience.Private]
        private void NotifyChangeListeners()
        {
            UpdateProgress();
            foreach (Replication.ChangeListener listener in changeListeners)
            {
                Replication.ChangeEvent changeEvent = new Replication.ChangeEvent(this);
                listener.Changed(changeEvent);
            }
        }

        [InterfaceAudience.Private]
        protected internal virtual void Login()
        {
            IDictionary<string, string> loginParameters = ((AuthenticatorImpl)GetAuthenticator
                ()).LoginParametersForSite(remote);
            if (loginParameters == null)
            {
                Log.D(Log.TagSync, "%s: %s has no login parameters, so skipping login", this, GetAuthenticator
                    ());
                FetchRemoteCheckpointDoc();
                return;
            }
            string loginPath = ((AuthenticatorImpl)GetAuthenticator()).LoginPathForSite(remote
                );
            Log.D(Log.TagSync, "%s: Doing login with %s at %s", this, GetAuthenticator().GetType
                (), loginPath);
            Log.V(Log.TagSync, "%s | %s: login() calling asyncTaskStarted()", this, Sharpen.Thread
                .CurrentThread());
            AsyncTaskStarted();
            SendAsyncRequest("POST", loginPath, loginParameters, new _RemoteRequestCompletionBlock_874
                (this, loginPath));
        }

        private sealed class _RemoteRequestCompletionBlock_874 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_874(Replication _enclosing, string loginPath
                )
            {
                this._enclosing = _enclosing;
                this.loginPath = loginPath;
            }

            public void OnCompletion(object result, Exception e)
            {
                try
                {
                    if (e != null)
                    {
                        Log.D(Log.TagSync, "%s: Login failed for path: %s", this, loginPath);
                        this._enclosing.SetError(e);
                    }
                    else
                    {
                        Log.V(Log.TagSync, "%s: Successfully logged in!", this);
                        this._enclosing.FetchRemoteCheckpointDoc();
                    }
                }
                finally
                {
                    Log.V(Log.TagSync, "%s | %s: login() calling asyncTaskFinished()", this, Sharpen.Thread
                        .CurrentThread());
                    this._enclosing.AsyncTaskFinished(1);
                }
            }

            private readonly Replication _enclosing;

            private readonly string loginPath;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void AsyncTaskStarted()
        {
            lock (this)
            {
                Log.V(Log.TagSync, "%s: asyncTaskStarted %d -> %d", this, this.asyncTaskCount, this
                    .asyncTaskCount + 1);
                if (asyncTaskCount++ == 0)
                {
                    Log.V(Log.TagSync, "%s: asyncTaskStarted() calling updateActive()", this);
                    UpdateActive();
                }
            }
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void AsyncTaskFinished(int numTasks)
        {
            lock (this)
            {
                Log.V(Log.TagSync, "%s: asyncTaskFinished %d -> %d", this, this.asyncTaskCount, this
                    .asyncTaskCount - numTasks);
                this.asyncTaskCount -= numTasks;
                System.Diagnostics.Debug.Assert((asyncTaskCount >= 0));
                if (asyncTaskCount == 0)
                {
                    Log.V(Log.TagSync, "%s: asyncTaskFinished() calling updateActive()", this);
                    UpdateActive();
                }
            }
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void UpdateActive()
        {
            try
            {
                int batcherCount = 0;
                if (batcher != null)
                {
                    batcherCount = batcher.Count();
                }
                else
                {
                    Log.W(Log.TagSync, "%s: batcher object is null.", this);
                }
                bool newActive = batcherCount > 0 || asyncTaskCount > 0;
                Log.D(Log.TagSync, "%s: updateActive() called.  active: %s, newActive: %s batcherCount: %d, asyncTaskCount: %d"
                    , this, active, newActive, batcherCount, asyncTaskCount);
                if (active != newActive)
                {
                    Log.D(Log.TagSync, "%s: Progress: set active = %s asyncTaskCount: %d batcherCount: %d"
                        , this, newActive, asyncTaskCount, batcherCount);
                    active = newActive;
                    NotifyChangeListeners();
                    if (!active)
                    {
                        if (!continuous)
                        {
                            Log.D(Log.TagSync, "%s since !continuous, calling stopped()", this);
                            Stopped();
                        }
                        else
                        {
                            if (error != null)
                            {
                                Log.D(Log.TagSync, "%s: Failed to xfer %d revisions, will retry in %d sec", this, 
                                    revisionsFailed, RetryDelay);
                                CancelPendingRetryIfReady();
                                ScheduleRetryIfReady();
                            }
                        }
                    }
                }
                else
                {
                    Log.D(Log.TagSync, "%s: active == newActive.", this);
                }
            }
            catch (Exception e)
            {
                Log.E(Log.TagSync, "Exception in updateActive()", e);
            }
            finally
            {
                Log.D(Log.TagSync, "%s: exit updateActive()", this);
            }
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void AddToInbox(RevisionInternal rev)
        {
            Log.V(Log.TagSync, "%s: addToInbox() called, rev: %s", this, rev);
            batcher.QueueObject(rev);
            Log.V(Log.TagSync, "%s: addToInbox() calling updateActive()", this);
            UpdateActive();
        }

        [InterfaceAudience.Private]
        protected internal virtual void ProcessInbox(RevisionList inbox)
        {
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void SendAsyncRequest(string method, string relativePath, object body
            , RemoteRequestCompletionBlock onCompletion)
        {
            try
            {
                string urlStr = BuildRelativeURLString(relativePath);
                Uri url = new Uri(urlStr);
                SendAsyncRequest(method, url, body, onCompletion);
            }
            catch (UriFormatException e)
            {
                Log.E(Log.TagSync, "Malformed URL for async request", e);
            }
        }

        [InterfaceAudience.Private]
        internal virtual string BuildRelativeURLString(string relativePath)
        {
            // the following code is a band-aid for a system problem in the codebase
            // where it is appending "relative paths" that start with a slash, eg:
            //     http://dotcom/db/ + /relpart == http://dotcom/db/relpart
            // which is not compatible with the way the java url concatonation works.
            string remoteUrlString = remote.ToExternalForm();
            if (remoteUrlString.EndsWith("/") && relativePath.StartsWith("/"))
            {
                remoteUrlString = Sharpen.Runtime.Substring(remoteUrlString, 0, remoteUrlString.Length
                     - 1);
            }
            return remoteUrlString + relativePath;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void SendAsyncRequest(string method, Uri url, object body, RemoteRequestCompletionBlock
             onCompletion)
        {
            RemoteRequest request = new RemoteRequest(workExecutor, clientFactory, method, url
                , body, GetLocalDatabase(), GetHeaders(), onCompletion);
            request.SetAuthenticator(GetAuthenticator());
            request.SetOnPreCompletion(new _RemoteRequestCompletionBlock_1023(this));
            request.SetOnPostCompletion(new _RemoteRequestCompletionBlock_1038(this, request)
                );
            if (remoteRequestExecutor.IsTerminated())
            {
                string msg = "sendAsyncRequest called, but remoteRequestExecutor has been terminated";
                throw new InvalidOperationException(msg);
            }
            Future future = remoteRequestExecutor.Submit(request);
            requests.Put(request, future);
        }

        private sealed class _RemoteRequestCompletionBlock_1023 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_1023(Replication _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void OnCompletion(object result, Exception e)
            {
                if (this._enclosing.serverType == null && result is HttpResponse)
                {
                    HttpResponse response = (HttpResponse)result;
                    Header serverHeader = response.GetFirstHeader("Server");
                    if (serverHeader != null)
                    {
                        string serverVersion = serverHeader.GetValue();
                        Log.V(Log.TagSync, "serverVersion: %s", serverVersion);
                        this._enclosing.serverType = serverVersion;
                    }
                }
            }

            private readonly Replication _enclosing;
        }

        private sealed class _RemoteRequestCompletionBlock_1038 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_1038(Replication _enclosing, RemoteRequest request
                )
            {
                this._enclosing = _enclosing;
                this.request = request;
            }

            public void OnCompletion(object result, Exception e)
            {
                Sharpen.Collections.Remove(this._enclosing.requests, request);
            }

            private readonly Replication _enclosing;

            private readonly RemoteRequest request;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void SendAsyncMultipartDownloaderRequest(string method, string relativePath
            , object body, Database db, RemoteRequestCompletionBlock onCompletion)
        {
            try
            {
                string urlStr = BuildRelativeURLString(relativePath);
                Uri url = new Uri(urlStr);
                RemoteMultipartDownloaderRequest request = new RemoteMultipartDownloaderRequest(workExecutor
                    , clientFactory, method, url, body, db, GetHeaders(), onCompletion);
                request.SetAuthenticator(GetAuthenticator());
                remoteRequestExecutor.Execute(request);
            }
            catch (UriFormatException e)
            {
                Log.E(Log.TagSync, "Malformed URL for async request", e);
            }
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void SendAsyncMultipartRequest(string method, string relativePath, 
            MultipartEntity multiPartEntity, RemoteRequestCompletionBlock onCompletion)
        {
            Uri url = null;
            try
            {
                string urlStr = BuildRelativeURLString(relativePath);
                url = new Uri(urlStr);
            }
            catch (UriFormatException e)
            {
                throw new ArgumentException(e);
            }
            RemoteMultipartRequest request = new RemoteMultipartRequest(workExecutor, clientFactory
                , method, url, multiPartEntity, GetLocalDatabase(), GetHeaders(), onCompletion);
            request.SetAuthenticator(GetAuthenticator());
            remoteRequestExecutor.Execute(request);
        }

        /// <summary>CHECKPOINT STORAGE:</summary>
        [InterfaceAudience.Private]
        internal virtual void MaybeCreateRemoteDB()
        {
        }

        // Pusher overrides this to implement the .createTarget option
        /// <summary>This is the _local document ID stored on the remote server to keep track of state.
        ///     </summary>
        /// <remarks>
        /// This is the _local document ID stored on the remote server to keep track of state.
        /// Its ID is based on the local database ID (the private one, to make the result unguessable)
        /// and the remote database's URL.
        /// </remarks>
        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual string RemoteCheckpointDocID()
        {
            if (remoteCheckpointDocID != null)
            {
                return remoteCheckpointDocID;
            }
            else
            {
                // TODO: Needs to be consistent with -hasSameSettingsAs: --
                // TODO: If a.remoteCheckpointID == b.remoteCheckpointID then [a hasSameSettingsAs: b]
                if (db == null)
                {
                    return null;
                }
                // canonicalization: make sure it produces the same checkpoint id regardless of
                // ordering of filterparams / docids
                IDictionary<string, object> filterParamsCanonical = null;
                if (GetFilterParams() != null)
                {
                    filterParamsCanonical = new SortedDictionary<string, object>(GetFilterParams());
                }
                IList<string> docIdsSorted = null;
                if (GetDocIds() != null)
                {
                    docIdsSorted = new AList<string>(GetDocIds());
                    docIdsSorted.Sort();
                }
                // use a treemap rather than a dictionary for purposes of canonicalization
                IDictionary<string, object> spec = new SortedDictionary<string, object>();
                spec.Put("localUUID", db.PrivateUUID());
                spec.Put("remoteURL", remote.ToExternalForm());
                spec.Put("push", !IsPull());
                spec.Put("continuous", IsContinuous());
                if (GetFilter() != null)
                {
                    spec.Put("filter", GetFilter());
                }
                if (filterParamsCanonical != null)
                {
                    spec.Put("filterParams", filterParamsCanonical);
                }
                if (docIdsSorted != null)
                {
                    spec.Put("docids", docIdsSorted);
                }
                byte[] inputBytes = null;
                try
                {
                    inputBytes = Manager.GetObjectMapper().WriteValueAsBytes(spec);
                }
                catch (IOException e)
                {
                    throw new RuntimeException(e);
                }
                remoteCheckpointDocID = Misc.TDHexSHA1Digest(inputBytes);
                return remoteCheckpointDocID;
            }
        }

        [InterfaceAudience.Private]
        private bool Is404(Exception e)
        {
            if (e is HttpResponseException)
            {
                return ((HttpResponseException)e).GetStatusCode() == 404;
            }
            return false;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void FetchRemoteCheckpointDoc()
        {
            lastSequenceChanged = false;
            string checkpointId = RemoteCheckpointDocID();
            string localLastSequence = db.LastSequenceWithCheckpointId(checkpointId);
            Log.V(Log.TagSync, "%s | %s: fetchRemoteCheckpointDoc() calling asyncTaskStarted()"
                , this, Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            SendAsyncRequest("GET", "/_local/" + checkpointId, null, new _RemoteRequestCompletionBlock_1202
                (this, localLastSequence));
        }

        private sealed class _RemoteRequestCompletionBlock_1202 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_1202(Replication _enclosing, string localLastSequence
                )
            {
                this._enclosing = _enclosing;
                this.localLastSequence = localLastSequence;
            }

            public void OnCompletion(object result, Exception e)
            {
                try
                {
                    if (e != null && !this._enclosing.Is404(e))
                    {
                        Log.W(Log.TagSync, "%s: error getting remote checkpoint", e, this);
                        this._enclosing.SetError(e);
                    }
                    else
                    {
                        if (e != null && this._enclosing.Is404(e))
                        {
                            Log.D(Log.TagSync, "%s: 404 error getting remote checkpoint %s, calling maybeCreateRemoteDB"
                                , this, this._enclosing.RemoteCheckpointDocID());
                            this._enclosing.MaybeCreateRemoteDB();
                        }
                        IDictionary<string, object> response = (IDictionary<string, object>)result;
                        this._enclosing.remoteCheckpoint = response;
                        string remoteLastSequence = null;
                        if (response != null)
                        {
                            remoteLastSequence = (string)response.Get("lastSequence");
                        }
                        if (remoteLastSequence != null && remoteLastSequence.Equals(localLastSequence))
                        {
                            this._enclosing.lastSequence = localLastSequence;
                            Log.D(Log.TagSync, "%s: Replicating from lastSequence=%s", this, this._enclosing.
                                lastSequence);
                        }
                        else
                        {
                            Log.D(Log.TagSync, "%s: lastSequence mismatch: I had: %s, remote had: %s", this, 
                                localLastSequence, remoteLastSequence);
                        }
                        this._enclosing.BeginReplicating();
                    }
                }
                finally
                {
                    Log.V(Log.TagSync, "%s | %s: fetchRemoteCheckpointDoc() calling asyncTaskFinished()"
                        , this, Sharpen.Thread.CurrentThread());
                    this._enclosing.AsyncTaskFinished(1);
                }
            }

            private readonly Replication _enclosing;

            private readonly string localLastSequence;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual void SaveLastSequence()
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
            Log.D(Log.TagSync, "%s: saveLastSequence() called. lastSequence: %s", this, lastSequence
                );
            IDictionary<string, object> body = new Dictionary<string, object>();
            if (remoteCheckpoint != null)
            {
                body.PutAll(remoteCheckpoint);
            }
            body.Put("lastSequence", lastSequence);
            string remoteCheckpointDocID = RemoteCheckpointDocID();
            if (remoteCheckpointDocID == null)
            {
                Log.W(Log.TagSync, "%s: remoteCheckpointDocID is null, aborting saveLastSequence()"
                    , this);
                return;
            }
            savingCheckpoint = true;
            string checkpointID = remoteCheckpointDocID;
            Log.D(Log.TagSync, "%s: put remote _local document.  checkpointID: %s", this, checkpointID
                );
            SendAsyncRequest("PUT", "/_local/" + checkpointID, body, new _RemoteRequestCompletionBlock_1274
                (this, body, checkpointID));
        }

        private sealed class _RemoteRequestCompletionBlock_1274 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_1274(Replication _enclosing, IDictionary<string
                , object> body, string checkpointID)
            {
                this._enclosing = _enclosing;
                this.body = body;
                this.checkpointID = checkpointID;
            }

            public void OnCompletion(object result, Exception e)
            {
                this._enclosing.savingCheckpoint = false;
                if (e != null)
                {
                    Log.W(Log.TagSync, "%s: Unable to save remote checkpoint", e, this);
                }
                if (this._enclosing.db == null)
                {
                    Log.W(Log.TagSync, "%s: Database is null, ignoring remote checkpoint response", this
                        );
                    return;
                }
                if (!this._enclosing.db.IsOpen())
                {
                    Log.W(Log.TagSync, "%s: Database is closed, ignoring remote checkpoint response", 
                        this);
                    return;
                }
                if (e != null)
                {
                    switch (this._enclosing.GetStatusFromError(e))
                    {
                        case Status.NotFound:
                        {
                            // Failed to save checkpoint:
                            this._enclosing.remoteCheckpoint = null;
                            // doc deleted or db reset
                            this._enclosing.overdueForSave = true;
                            // try saving again
                            break;
                        }

                        case Status.Conflict:
                        {
                            this._enclosing.RefreshRemoteCheckpointDoc();
                            break;
                        }

                        default:
                        {
                            // TODO: On 401 or 403, and this is a pull, remember that remote
                            // TODo: is read-only & don't attempt to read its checkpoint next time.
                            break;
                        }
                    }
                }
                else
                {
                    // Saved checkpoint:
                    IDictionary<string, object> response = (IDictionary<string, object>)result;
                    body.Put("_rev", response.Get("rev"));
                    this._enclosing.remoteCheckpoint = body;
                    this._enclosing.db.SetLastSequence(this._enclosing.lastSequence, checkpointID, !this
                        ._enclosing.IsPull());
                }
                if (this._enclosing.overdueForSave)
                {
                    this._enclosing.SaveLastSequence();
                }
            }

            private readonly Replication _enclosing;

            private readonly IDictionary<string, object> body;

            private readonly string checkpointID;
        }

        [InterfaceAudience.Public]
        public virtual bool GoOffline()
        {
            if (!online)
            {
                return false;
            }
            if (db == null)
            {
                return false;
            }
            db.RunAsync(new _AsyncTask_1328(this));
            return true;
        }

        private sealed class _AsyncTask_1328 : AsyncTask
        {
            public _AsyncTask_1328(Replication _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void Run(Database database)
            {
                Log.D(Log.TagSync, "%s: Going offline", this);
                this._enclosing.online = false;
                this._enclosing.StopRemoteRequests();
                this._enclosing.UpdateProgress();
                this._enclosing.NotifyChangeListeners();
            }

            private readonly Replication _enclosing;
        }

        [InterfaceAudience.Public]
        public virtual bool GoOnline()
        {
            if (online)
            {
                return false;
            }
            if (db == null)
            {
                return false;
            }
            db.RunAsync(new _AsyncTask_1349(this));
            return true;
        }

        private sealed class _AsyncTask_1349 : AsyncTask
        {
            public _AsyncTask_1349(Replication _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void Run(Database database)
            {
                Log.D(Log.TagSync, "%s: Going online", this);
                this._enclosing.online = true;
                if (this._enclosing.running)
                {
                    this._enclosing.lastSequence = null;
                    this._enclosing.SetError(null);
                }
                this._enclosing.remoteRequestExecutor = Executors.NewCachedThreadPool();
                this._enclosing.CheckSession();
                this._enclosing.NotifyChangeListeners();
            }

            private readonly Replication _enclosing;
        }

        [InterfaceAudience.Private]
        private void StopRemoteRequests()
        {
            Log.V(Log.TagSync, "%s: stopRemoteRequests() cancelling: %d requests", this, requests
                .Count);
            foreach (RemoteRequest request in requests.Keys)
            {
                Log.V(Log.TagSync, "%s: aborting request: %s underlying req: %s", this, request, 
                    request.GetRequest().GetURI());
                request.Abort();
                Log.V(Log.TagSync, "%s: aborted request", this);
            }
        }

        [InterfaceAudience.Private]
        internal virtual void UpdateProgress()
        {
            if (!IsRunning())
            {
                status = Replication.ReplicationStatus.ReplicationStopped;
            }
            else
            {
                if (!online)
                {
                    status = Replication.ReplicationStatus.ReplicationOffline;
                }
                else
                {
                    if (active)
                    {
                        status = Replication.ReplicationStatus.ReplicationActive;
                    }
                    else
                    {
                        status = Replication.ReplicationStatus.ReplicationIdle;
                    }
                }
            }
        }

        [InterfaceAudience.Private]
        protected internal virtual void SetError(Exception throwable)
        {
            // TODO
            if (throwable != error)
            {
                Log.E(Log.TagSync, "%s: Progress: set error = %s", this, throwable);
                error = throwable;
                NotifyChangeListeners();
            }
        }

        [InterfaceAudience.Private]
        protected internal virtual void RevisionFailed()
        {
            // Remember that some revisions failed to transfer, so we can later retry.
            ++revisionsFailed;
        }

        protected internal virtual RevisionInternal TransformRevision(RevisionInternal rev
            )
        {
            if (revisionBodyTransformationBlock != null)
            {
                try
                {
                    int generation = rev.GetGeneration();
                    RevisionInternal xformed = revisionBodyTransformationBlock.Invoke(rev);
                    if (xformed == null)
                    {
                        return null;
                    }
                    if (xformed != rev)
                    {
                        System.Diagnostics.Debug.Assert((xformed.GetDocId().Equals(rev.GetDocId())));
                        System.Diagnostics.Debug.Assert((xformed.GetRevId().Equals(rev.GetRevId())));
                        System.Diagnostics.Debug.Assert((xformed.GetProperties().Get("_revisions").Equals
                            (rev.GetProperties().Get("_revisions"))));
                        if (xformed.GetProperties().Get("_attachments") != null)
                        {
                            // Insert 'revpos' properties into any attachments added by the callback:
                            RevisionInternal mx = new RevisionInternal(xformed.GetProperties(), db);
                            xformed = mx;
                            mx.MutateAttachments(new _Functor_1452(generation));
                        }
                        rev = xformed;
                    }
                }
                catch (Exception e)
                {
                    Log.W(Log.TagSync, "%s: Exception transforming a revision of doc '%s", e, this, rev
                        .GetDocId());
                }
            }
            return rev;
        }

        private sealed class _Functor_1452 : CollectionUtils.Functor<IDictionary<string, 
            object>, IDictionary<string, object>>
        {
            public _Functor_1452(int generation)
            {
                this.generation = generation;
            }

            public IDictionary<string, object> Invoke(IDictionary<string, object> info)
            {
                if (info.Get("revpos") != null)
                {
                    return info;
                }
                if (info.Get("data") == null)
                {
                    throw new InvalidOperationException("Transformer added attachment without adding data"
                        );
                }
                IDictionary<string, object> nuInfo = new Dictionary<string, object>(info);
                nuInfo.Put("revpos", generation);
                return nuInfo;
            }

            private readonly int generation;
        }

        /// <summary>
        /// Called after a continuous replication has gone idle, but it failed to transfer some revisions
        /// and so wants to try again in a minute.
        /// </summary>
        /// <remarks>
        /// Called after a continuous replication has gone idle, but it failed to transfer some revisions
        /// and so wants to try again in a minute. Should be overridden by subclasses.
        /// </remarks>
        [InterfaceAudience.Private]
        protected internal virtual void Retry()
        {
            SetError(null);
        }

        [InterfaceAudience.Private]
        protected internal virtual void RetryIfReady()
        {
            if (!running)
            {
                return;
            }
            if (online)
            {
                Log.D(Log.TagSync, "%s: RETRYING, to transfer missed revisions", this);
                revisionsFailed = 0;
                CancelPendingRetryIfReady();
                Retry();
            }
            else
            {
                ScheduleRetryIfReady();
            }
        }

        [InterfaceAudience.Private]
        protected internal virtual void CancelPendingRetryIfReady()
        {
            if (retryIfReadyFuture != null && retryIfReadyFuture.IsCancelled() == false)
            {
                retryIfReadyFuture.Cancel(true);
            }
        }

        [InterfaceAudience.Private]
        protected internal virtual void ScheduleRetryIfReady()
        {
            retryIfReadyFuture = workExecutor.Schedule(new _Runnable_1508(this), RetryDelay, 
                TimeUnit.Seconds);
        }

        private sealed class _Runnable_1508 : Runnable
        {
            public _Runnable_1508(Replication _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void Run()
            {
                this._enclosing.RetryIfReady();
            }

            private readonly Replication _enclosing;
        }

        [InterfaceAudience.Private]
        private int GetStatusFromError(Exception t)
        {
            if (t is CouchbaseLiteException)
            {
                CouchbaseLiteException couchbaseLiteException = (CouchbaseLiteException)t;
                return couchbaseLiteException.GetCBLStatus().GetCode();
            }
            return Status.Unknown;
        }

        /// <summary>
        /// Variant of -fetchRemoveCheckpointDoc that's used while replication is running, to reload the
        /// checkpoint to get its current revision number, if there was an error saving it.
        /// </summary>
        /// <remarks>
        /// Variant of -fetchRemoveCheckpointDoc that's used while replication is running, to reload the
        /// checkpoint to get its current revision number, if there was an error saving it.
        /// </remarks>
        [InterfaceAudience.Private]
        private void RefreshRemoteCheckpointDoc()
        {
            Log.D(Log.TagSync, "%s: Refreshing remote checkpoint to get its _rev...", this);
            savingCheckpoint = true;
            Log.V(Log.TagSync, "%s | %s: refreshRemoteCheckpointDoc() calling asyncTaskStarted()"
                , this, Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            SendAsyncRequest("GET", "/_local/" + RemoteCheckpointDocID(), null, new _RemoteRequestCompletionBlock_1535
                (this));
        }

        private sealed class _RemoteRequestCompletionBlock_1535 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_1535(Replication _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void OnCompletion(object result, Exception e)
            {
                try
                {
                    if (this._enclosing.db == null)
                    {
                        Log.W(Log.TagSync, "%s: db == null while refreshing remote checkpoint.  aborting"
                            , this);
                        return;
                    }
                    this._enclosing.savingCheckpoint = false;
                    if (e != null && this._enclosing.GetStatusFromError(e) != Status.NotFound)
                    {
                        Log.E(Log.TagSync, "%s: Error refreshing remote checkpoint", e, this);
                    }
                    else
                    {
                        Log.D(Log.TagSync, "%s: Refreshed remote checkpoint: %s", this, result);
                        this._enclosing.remoteCheckpoint = (IDictionary<string, object>)result;
                        this._enclosing.lastSequenceChanged = true;
                        this._enclosing.SaveLastSequence();
                    }
                }
                finally
                {
                    // try saving again
                    Log.V(Log.TagSync, "%s | %s: refreshRemoteCheckpointDoc() calling asyncTaskFinished()"
                        , this, Sharpen.Thread.CurrentThread());
                    this._enclosing.AsyncTaskFinished(1);
                }
            }

            private readonly Replication _enclosing;
        }

        [InterfaceAudience.Private]
        protected internal virtual Status StatusFromBulkDocsResponseItem(IDictionary<string
            , object> item)
        {
            try
            {
                if (!item.ContainsKey("error"))
                {
                    return new Status(Status.Ok);
                }
                string errorStr = (string)item.Get("error");
                if (errorStr == null || errorStr.IsEmpty())
                {
                    return new Status(Status.Ok);
                }
                // 'status' property is nonstandard; TouchDB returns it, others don't.
                string statusString = (string)item.Get("status");
                int status = System.Convert.ToInt32(statusString);
                if (status >= 400)
                {
                    return new Status(status);
                }
                // If no 'status' present, interpret magic hardcoded CouchDB error strings:
                if (Sharpen.Runtime.EqualsIgnoreCase(errorStr, "unauthorized"))
                {
                    return new Status(Status.Unauthorized);
                }
                else
                {
                    if (Sharpen.Runtime.EqualsIgnoreCase(errorStr, "forbidden"))
                    {
                        return new Status(Status.Forbidden);
                    }
                    else
                    {
                        if (Sharpen.Runtime.EqualsIgnoreCase(errorStr, "conflict"))
                        {
                            return new Status(Status.Conflict);
                        }
                        else
                        {
                            return new Status(Status.UpstreamError);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.E(Database.Tag, "Exception getting status from " + item, e);
            }
            return new Status(Status.Ok);
        }

        [InterfaceAudience.Private]
        public virtual void NetworkReachable()
        {
            GoOnline();
        }

        [InterfaceAudience.Private]
        public virtual void NetworkUnreachable()
        {
            GoOffline();
        }

        [InterfaceAudience.Private]
        internal virtual bool ServerIsSyncGatewayVersion(string minVersion)
        {
            string prefix = "Couchbase Sync Gateway/";
            if (serverType == null)
            {
                return false;
            }
            else
            {
                if (serverType.StartsWith(prefix))
                {
                    string versionString = Sharpen.Runtime.Substring(serverType, prefix.Length);
                    return Sharpen.Runtime.CompareOrdinal(versionString, minVersion) >= 0;
                }
            }
            return false;
        }

        [InterfaceAudience.Private]
        internal virtual void SetServerType(string serverType)
        {
            this.serverType = serverType;
        }

        [InterfaceAudience.Private]
        internal virtual HttpClientFactory GetClientFactory()
        {
            return clientFactory;
        }
    }
}
