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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using Couchbase.Lite.Replicator;
using Stateless;
using System.Collections.Concurrent;
using System.Text;

#if !NET_3_5
using StringEx = System.String;
using System.Net;
#else
using System.Net.Couchbase;
#endif

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
    [Serializable]
    public enum ReplicationStatus {
        /// <summary>
        /// The <see cref="Couchbase.Lite.Replication"/> is finished or hit a fatal error.
        /// </summary>
        Stopped,
        /// <summary>
        /// The remote host is currently unreachable.
        /// </summary>
        Offline,
        /// <summary>
        /// The continuous <see cref="Couchbase.Lite.Replication"/> is caught up and
        /// waiting for more changes.
        /// </summary>
        Idle,
        /// <summary>
        /// The <see cref="Couchbase.Lite.Replication"/> is actively transferring data.
        /// </summary>
        Active
    }

    #endregion

    /// <summary>
    /// A class for holding replication options
    /// </summary>
    [DictionaryContract(OptionalKeys=new object[] { 
        ReplicationOptionsDictionary.REMOTE_UUID_KEY, typeof(string) 
    })]
    public sealed class ReplicationOptionsDictionary : ContractedDictionary
    {
        /// <summary>
        /// This key stores an ID for a remote endpoint whose identifier
        /// is likely to change (i.e. found via Bonjour)
        /// </summary>
        public const string REMOTE_UUID_KEY = "remoteUUID";
    }

    /// <summary>
    /// A Couchbase Lite pull or push <see cref="Couchbase.Lite.Replication"/>
    /// between a local and a remote <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public abstract class Replication
    {

        #region Constants

        /// <summary>
        /// The protocol version to use when syncing with Sync Gateway.
        /// This value is also included in all HTTP requests as the
        /// User-Agent version.
        /// </summary>
        public const string SYNC_PROTOCOL_VERSION = "1.2";

        internal const string CHANNELS_QUERY_PARAM = "channels";
        internal const string BY_CHANNEL_FILTER_NAME = "sync_gateway/bychannel";
        internal const string REPLICATOR_DATABASE_NAME = "_replicator";
        internal const int INBOX_CAPACITY = 100;
        private const int PROCESSOR_DELAY = 500; //Milliseconds
        private const int RETRY_DELAY = 60; // Seconds
        private const int SAVE_LAST_SEQUENCE_DELAY = 2; //Seconds
        private const string TAG = "Replication";
        private const string LOCAL_CHECKPOINT_LOCAL_UUID_KEY = "localUUID";

        #endregion

        #region Variables

        /// <summary>
        /// Adds or Removed a <see cref="Couchbase.Lite.Database"/> change delegate
        /// that will be called whenever the <see cref="Couchbase.Lite.Replication"/>
        /// changes.
        /// </summary>
        public event EventHandler<ReplicationChangeEventArgs> Changed 
        {
            add { _changed = (EventHandler<ReplicationChangeEventArgs>)Delegate.Combine(_changed, value); }
            remove { _changed = (EventHandler<ReplicationChangeEventArgs>)Delegate.Remove(_changed, value); }
        }
        private EventHandler<ReplicationChangeEventArgs> _changed;

        /// <summary>
        /// The state machine the holds and controls the state of the replicator
        /// </summary>
        protected readonly StateMachine<ReplicationState, ReplicationTrigger> _stateMachine;

        /// <summary>
        /// The task factory on which work is executed
        /// </summary>
        protected readonly TaskFactory WorkExecutor;

        private IHttpClientFactory _clientFactory;

        /// <summary>
        /// The list of currently active HTTP requests
        /// </summary>
        [Obsolete("This field will no longer store active requests, use _requests")]
        protected ICollection<HttpClient> requests;

        /// <summary>
        /// The list of currently active HTTP messages
        /// </summary>
        protected readonly ConcurrentDictionary<HttpRequestMessage, Task> _requests;

        /// <summary>
        /// Whether or not the LastSequence property has changed
        /// </summary>
        protected bool lastSequenceChanged;

        /// <summary>
        /// The ID of the replication session
        /// </summary>
        protected internal string sessionID;

        private bool _savingCheckpoint;
        private IDictionary<string, object> _remoteCheckpoint;
        private bool _continuous;
        private int _revisionsFailed;
        private static int _lastSessionID;
        private string _remoteCheckpointDocID;
        private CancellationTokenSource _retryIfReadyTokenSource;
        private Task _retryIfReadyTask;
        private readonly Queue<ReplicationChangeEventArgs> _eventQueue = new Queue<ReplicationChangeEventArgs>();
        private HashSet<string> _pendingDocumentIDs;
        private TaskFactory _eventContext; // Keep a separate reference since the localDB will be nulled on certain types of stop
        private Guid _replicatorID = Guid.NewGuid();
        private CookieStore _cookieStore;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the transformation function used on the properties of the documents
        /// being replicated
        /// </summary>
        public PropertyTransformationDelegate TransformationFunction { get; set; }

        /// <summary>
        /// Gets the local <see cref="Couchbase.Lite.Database"/> being replicated to/from.
        /// </summary>
        public Database LocalDatabase { get; private set; }

        /// <summary>
        /// Gets the remote URL being replicated to/from.
        /// </summary>
        public Uri RemoteUrl { get; private set; }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Replication"/> pulls from,
        /// as opposed to pushes to, the target.
        /// </summary>
        public abstract bool IsPull { get; }

        /// <summary>
        /// Gets or sets whether the target <see cref="Couchbase.Lite.Database"/> should be created
        /// if it doesn't already exist. This only has an effect if the target supports it.
        /// </summary>
        public abstract bool CreateTarget { get; set; }

        /// <summary>
        /// Gets or sets whether the <see cref="Couchbase.Lite.Replication"/> operates continuously,
        /// replicating changes as the source <see cref="Couchbase.Lite.Database"/> is modified.
        /// </summary>
        public bool Continuous
        {
            get { return _continuous; }
            set { if (!IsRunning) _continuous = value; }
        }

        /// <summary>
        /// Gets or sets the name of an optional filter function to run on the source
        /// <see cref="Couchbase.Lite.Database"/>. Only documents for which the function
        /// returns true are replicated.
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// Gets or sets the parameters to pass to the filter function.
        /// </summary>
        /// <value>The parameters to pass to the filter function.</value>
        public IDictionary<string, object> FilterParams { get; set; }

        /// <summary>
        /// Gets or sets the list of Sync Gateway channel names to filter by for pull <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        /// <remarks>
        /// Gets or sets the list of Sync Gateway channel names to filter by for pull <see cref="Couchbase.Lite.Replication"/>.
        /// A null value means no filtering, and all available channels will be replicated.
        /// Only valid for pull replications whose source database is on a Couchbase Sync Gateway server.
        /// This is a convenience property that just sets the values of filter and filterParams.
        /// </remarks>
        public IEnumerable<string> Channels {
            get
            {
                if (FilterParams == null || FilterParams.Count == 0)
                {
                    return new List<string>();
                }

                var p = FilterParams.ContainsKey(CHANNELS_QUERY_PARAM)
                    ? (string)FilterParams[CHANNELS_QUERY_PARAM]
                    : null;
                if (!IsPull || Filter == null || !Filter.Equals(BY_CHANNEL_FILTER_NAME) || StringEx.IsNullOrWhiteSpace(p))
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
                        Log.W(TAG, "filterChannels can only be set in pull replications");
                        return;
                    }

                    Filter = BY_CHANNEL_FILTER_NAME;
                    var filterParams = new Dictionary<string, object>();
                    filterParams.Put(CHANNELS_QUERY_PARAM, String.Join(",", value.ToStringArray()));
                    FilterParams = filterParams;
                }
                else if (Filter != null && Filter.Equals(BY_CHANNEL_FILTER_NAME))
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
        public abstract IEnumerable<string> DocIds { get; set; }

        /// <summary>
        /// Gets or sets the extra HTTP headers to send in <see cref="Couchbase.Lite.Replication"/>
        /// requests to the remote <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <value>
        /// the extra HTTP headers to send in <see cref="Couchbase.Lite.Replication"/> requests
        /// to the remote <see cref="Couchbase.Lite.Database"/>.
        /// </value>
        public abstract IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Replication"/>'s current status.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Replication"/>'s current status.</value>
        public ReplicationStatus Status 
        {
            get {
                if (_stateMachine == null) {
                    return ReplicationStatus.Stopped;
                } else if (_stateMachine.IsInState(ReplicationState.Offline)) {
                    return ReplicationStatus.Offline;
                } else if (_stateMachine.IsInState(ReplicationState.Idle)) {
                    return ReplicationStatus.Idle;
                } else if (_stateMachine.IsInState(ReplicationState.Stopped) || _stateMachine.IsInState(ReplicationState.Initial)) {
                    return ReplicationStatus.Stopped;
                } else {
                    return ReplicationStatus.Active;
                }
            }
        }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Replication"/> is running.
        /// Continuous <see cref="Couchbase.Lite.Replication"/>s never actually stop,
        /// instead they go idle waiting for new data to appear.
        /// </summary>
        /// <value>
        /// <c>true</c> if <see cref="Couchbase.Lite.Replication"/> is running; otherwise, <c>false</c>.
        /// </value>
        public bool IsRunning 
        { 
            get {
                return _stateMachine != null && 
                    !_stateMachine.IsInState(ReplicationState.Stopped) &&
                    !_stateMachine.IsInState(ReplicationState.Initial);
            }
        }

        /// <summary>
        /// Gets the last error, if any, that occurred since the <see cref="Couchbase.Lite.Replication"/> was started.
        /// </summary>
        public Exception LastError
        { 
            get { return _lastError; }
            set
            {
                if (value != _lastError) {
                    var newException = value == null ? null : value.Flatten().FirstOrDefault();
                    Log.E(TAG, " Progress: set error = {0}", (object)newException);
                    _lastError = newException;
                    NotifyChangeListeners();
                }
            }
        }
        private Exception _lastError;

        /// <summary>
        /// If the <see cref="Couchbase.Lite.Replication"/> is active, gets the number of completed changes that have been processed, otherwise 0.
        /// </summary>
        /// <value>The completed changes count.</value>
        public int CompletedChangesCount 
        {
            get { return _completedChangesCount; }
        }
        private int _completedChangesCount;

        /// <summary>
        /// If the <see cref="Couchbase.Lite.Replication"/> is active, gets the number of changes to be processed, otherwise 0.
        /// </summary>
        /// <value>The changes count.</value>
        public int ChangesCount 
        {
            get { return _changesCount; }
        }
        private int _changesCount;

        /// <summary>
        /// Gets or sets the authenticator.
        /// </summary>
        /// <value>The authenticator.</value>
        public IAuthenticator Authenticator { get; set; }

        /// <summary>
        /// Gets the active task info for thie replication
        /// </summary>
        public IDictionary<string, object> ActiveTaskInfo
        {
            get {
                // For schema, see http://wiki.apache.org/couchdb/HttpGetActiveTasks
                var source = RemoteUrl.AbsoluteUri;
                var target = LocalDatabase.Name;
                if (!IsPull) {
                    var temp = source;
                    source = target;
                    target = temp;
                }

                string status;
                int? progress = null;
                if (!IsRunning) {
                    status = "Stopped";
                } else if (Status == ReplicationStatus.Offline) {
                    status = "Offline"; //non-standard
                } else if (Status != ReplicationStatus.Active) {
                    status = "Idle"; //non-standard
                } else {
                    var processed = _completedChangesCount;
                    var total = _changesCount;
                    status = String.Format("Processed {0} / {1} changes", processed, total);
                    progress = total > 0 ? (int?)Math.Round(100 * (processed / (double)total)) : null;
                }

                List<object> error = new List<object>();
                var errorObj = LastError;
                if (errorObj != null) {
                    error.Add(errorObj.Message);
                }

                /*IList<HttpClient> remoteRequests;
                lock (requests) {
                    remoteRequests = new List<HttpClient>(requests);
                }*/

                //TODO: Active requests needs refactor

                return new NonNullDictionary<string, object> {
                    { "type", "Replication" },
                    { "task", sessionID },
                    { "source", source },
                    { "target", target },
                    { "continuous", Continuous ? (bool?)true : null },
                    { "status", status },
                    { "progress", progress },
                    { "error", error }
                };
            }
        }

        /// <summary>
        /// Gets or sets custom options on this replication
        /// </summary>
        public ReplicationOptionsDictionary Options { get; set; }
       
        /// <summary>
        /// Returns whether or not the replication may be stopped at 
        /// the point of time in question
        /// </summary>
        protected virtual bool IsSafeToStop
        {
            get { return true; }
        }

        /// <summary>
        /// Gets or sets the last sequence that this replication processed from its source database
        /// </summary>
        protected internal string LastSequence
        {
            get { return _lastSequence; }
            set {
                if (value != null && !value.Equals(_lastSequence)) {
                    Log.V(TAG, "Setting LastSequence to " + value + " from( " + _lastSequence + ")");
                    _lastSequence = value;

                    if (!lastSequenceChanged) {
                        lastSequenceChanged = true;
                    }
                }
            }
        }
        private string _lastSequence = "0";

        /// <summary>
        /// Gets or sets the client factory used to create HttpClient objects 
        /// for connected to remote databases
        /// </summary>
        internal IHttpClientFactory ClientFactory {
            get { return _clientFactory; }
            set { 
                if (value != null) {
                    _clientFactory = value;
                } else {
                    Manager manager = null;
                    if (LocalDatabase != null) {
                        manager = LocalDatabase.Manager;
                    }

                    IHttpClientFactory managerClientFactory = null;
                    if (manager != null) {
                        managerClientFactory = manager.DefaultHttpClientFactory;
                    }

                    if (managerClientFactory != null) {
                        _clientFactory = managerClientFactory;
                    }
                    else {
                        var id = LocalDatabase == null ? null : RemoteCheckpointDocID(LocalDatabase.PrivateUUID());
                        _cookieStore = new CookieStore(LocalDatabase, id);
                        _clientFactory = new CouchbaseLiteHttpClientFactory();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the cancellation token source to cancel this replication's operation
        /// </summary>
        protected CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Gets or sets the headers that should be used when making HTTP requests
        /// </summary>
        protected internal IDictionary<String, Object> RequestHeaders { get; set; }

        /// <summary>
        /// The container for storing cookies specific to this replication
        /// </summary>
        protected internal CookieStore CookieContainer
        {
            get { return _cookieStore; }
        }

        internal RemoteServerVersion ServerType { get; set; }
        internal Batcher<RevisionInternal> Batcher { get; set; }
        internal Func<RevisionInternal, RevisionInternal> RevisionBodyTransformationFunction { get; private set; }


        #endregion

        #region Constructors

        /// <summary>
        /// Convenience constructor
        /// </summary>
        /// <param name="db">The local database to replicate to/from</param>
        /// <param name="remote">The remote Uri to sync with</param>
        /// <param name="continuous">If set to <c>true</c> continuous.</param>
        /// <param name="workExecutor">The TaskFactory to execute work on</param>
        protected Replication(Database db, Uri remote, bool continuous, TaskFactory workExecutor)
            : this(db, remote, continuous, null, workExecutor) { }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="db">The local database to replicate to/from</param>
        /// <param name="remote">The remote Uri to sync with</param>
        /// <param name="continuous">If set to <c>true</c> continuous.</param>
        /// <param name="clientFactory">The client factory for instantiating the HttpClient used to create web requests</param>
        /// <param name="workExecutor">The TaskFactory to execute work on</param>
        internal Replication(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory, TaskFactory workExecutor)
        {
            LocalDatabase = db;
            _eventContext = LocalDatabase.Manager.CapturedContext;
            Continuous = continuous;
            // NOTE: Consider running a separate scheduler for all http requests.
            WorkExecutor = workExecutor;
            CancellationTokenSource = new CancellationTokenSource();
            RemoteUrl = remote;
            Options = new ReplicationOptionsDictionary();
            RequestHeaders = new Dictionary<String, Object>();
            _requests = new ConcurrentDictionary<HttpRequestMessage, Task>();

            // FIXME: Refactor to visitor pattern.
            if (RemoteUrl.GetQuery() != null && !StringEx.IsNullOrWhiteSpace(RemoteUrl.GetQuery()))
            {
                var uri = new Uri(remote.ToString());
                var personaAssertion = URIUtils.GetQueryParameter(uri, PersonaAuthorizer.QueryParameter);

                if (personaAssertion != null && !StringEx.IsNullOrWhiteSpace(personaAssertion))
                {
                    var email = PersonaAuthorizer.RegisterAssertion(personaAssertion);
                    var authorizer = new PersonaAuthorizer(email);
                    Authenticator = authorizer;
                }

                var facebookAccessToken = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QUERY_PARAMETER);

                if (facebookAccessToken != null && !StringEx.IsNullOrWhiteSpace(facebookAccessToken))
                {
                    var email = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QUERY_PARAMETER_EMAIL);
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

                    FacebookAuthorizer.RegisterAccessToken(facebookAccessToken, email, remoteWithQueryRemoved);

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

            Batcher = new Batcher<RevisionInternal>(workExecutor, INBOX_CAPACITY, PROCESSOR_DELAY, inbox =>
            {
                try {
                    Log.V(TAG, "*** BEGIN ProcessInbox for {0} ({1} sequences)", _replicatorID, inbox.Count);
                    if(Continuous) {
                        FireTrigger(ReplicationTrigger.Resume);
                    }

                    ProcessInbox (new RevisionList(inbox));

                    Log.V(TAG, "*** END ProcessInbox for {0} (lastSequence={1})", _replicatorID, LastSequence);
                } catch(Exception e) {
                    Log.E(TAG, "ProcessInbox failed: ", e);
                    throw new RuntimeException(e);
                }
            });

            ClientFactory = clientFactory;

            _stateMachine = new StateMachine<ReplicationState, ReplicationTrigger>(ReplicationState.Initial);
            InitializeStateMachine();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public void Start()
        {
            FireTrigger(ReplicationTrigger.Start);
        }

        /// <summary>
        /// Stops the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public virtual void Stop()
        {
            FireTrigger(ReplicationTrigger.StopGraceful);
        }

        /// <summary>
        /// Gets a collection of document IDs that have been scheduled for replication
        /// but not yet completed.
        /// </summary>
        /// <returns>The pending document IDs.</returns>
        public ICollection<string> GetPendingDocumentIDs()
        {
            if (IsPull || (_stateMachine.State > ReplicationState.Initial && _pendingDocumentIDs != null)) {
                return _pendingDocumentIDs;
            }

            var lastSequence = LastSequence;
            if (lastSequence == null || lastSequence == "0") {
                var checkpointID = RemoteCheckpointDocID(LocalDatabase.PrivateUUID());
                lastSequence = LocalDatabase.LastSequenceWithCheckpointId(checkpointID);
                if (lastSequence == null) {
                    var doc = LocalDatabase.GetLocalCheckpointDoc();
                    var importedUUID = doc == null ? null : doc.GetCast<string>(LOCAL_CHECKPOINT_LOCAL_UUID_KEY);
                    if (importedUUID != null) {
                        checkpointID = RemoteCheckpointDocID(importedUUID);
                        lastSequence = LocalDatabase.LastSequenceWithCheckpointId(checkpointID);
                    }
                }
            }

            if (!LocalDatabase.IsOpen) {
                Log.D(TAG, "LocalDatabase is not open, so ruling Replication as stopped.  Returning empty pending ID set");
                return new HashSet<string>();
            }

            var revs = LocalDatabase.UnpushedRevisionsSince(lastSequence, LocalDatabase.GetFilter(Filter), FilterParams);
            if (revs != null) {
                _pendingDocumentIDs = new HashSet<string>(revs.GetAllDocIds());
                return _pendingDocumentIDs;
            }

            Log.W(TAG, "Error getting unpushed revisions");
            return new HashSet<string>();
        }

        /// <summary>
        /// Checks if the specified document is pending replication
        /// </summary>
        /// <returns><c>true</c> if this document is pending, otherwise, <c>false</c>.</returns>
        /// <param name="doc">The document to check.</param>
        public bool IsDocumentPending(Document doc)
        {
            return doc != null && GetPendingDocumentIDs().Contains(doc.Id);
        }

        /// <summary>
        /// Restarts the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public void Restart()
        {
            Changed += WaitForStopped;
            Stop();
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
            
            _cookieStore.Add(cookie);
        }

        /// <summary>
        /// Deletes a cookie specified by name
        /// </summary>
        /// <param name="name">The name of the cookie</param>
        public void DeleteCookie(String name)
        {
            _cookieStore.Delete(RemoteUrl, name);
        }
            
        #endregion

        #region Protected Methods

        /// <summary>
        /// Creates the database object on the remote endpoint, if necessary
        /// </summary>
        protected internal virtual void MaybeCreateRemoteDB() { }

        /// <summary>
        /// Checks the remote endpoint against the given version to see if it is at 
        /// that version or above
        /// </summary>
        /// <returns><c>true</c>, if the server is at or above the minimum version, <c>false</c> otherwise.</returns>
        /// <param name="minVersion">Minimum version.</param>
        protected internal bool CheckServerCompatVersion(string minVersion)
        {
            if (ServerType == null) {
                return false;
            }

            return ServerType.IsSyncGateway && string.Compare(ServerType.Version, minVersion, StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Increments the count of failed revisions for the replication
        /// </summary>
        protected void RevisionFailed()
        {
            _revisionsFailed++;
        }

        /// <summary>
        /// Gets the status from a response from _bulk_docs and translates it into
        /// a Status object
        /// </summary>
        /// <returns>The status of the request</returns>
        /// <param name="item">The response received</param>
        protected Status StatusFromBulkDocsResponseItem(IDictionary<string, object> item)
        {
            try {
                if (!item.ContainsKey("error")) {
                    return new Status(StatusCode.Ok);
                }

                var errorStr = item.Get("error") as string;
                if (StringEx.IsNullOrWhiteSpace(errorStr)) {
                    return new Status(StatusCode.Ok);
                }

                // 'status' property is nonstandard; TouchDB returns it, others don't.
                var status = item.GetCast<int>("status");
                if (status >= 400) {
                    return new Status((StatusCode)status);
                }

                // If no 'status' present, interpret magic hardcoded CouchDB error strings:
                if (errorStr.Equals("unauthorized", StringComparison.InvariantCultureIgnoreCase)) {
                    return new Status(StatusCode.Unauthorized);
                } else {
                    if (errorStr.Equals("forbidden", StringComparison.InvariantCultureIgnoreCase)) {
                        return new Status(StatusCode.Forbidden);
                    } else {
                        if (errorStr.Equals("conflict", StringComparison.InvariantCultureIgnoreCase)) {
                            return new Status(StatusCode.Conflict);
                        } else {
                            return new Status(StatusCode.UpStreamError);
                        }
                    }
                }
            } catch (Exception e) {
                Log.E(Database.TAG, "Exception getting status from " + item, e);
            }

            return new Status(StatusCode.Ok);
        }

        /// <summary>
        /// Called after a continuous replication has gone idle, but it failed to transfer some revisions
        /// and so wants to try again in a minute.
        /// </summary>
        /// <remarks>
        /// Called after a continuous replication has gone idle, but it failed to transfer some revisions
        /// and so wants to try again in a minute. Should be overridden by subclasses.
        /// </remarks>
        protected virtual void Retry()
        {
            LastError = null;
            CheckSession();
        }

        /// <summary>
        /// Attempts to retry a previously failed replication, if possible
        /// </summary>
        protected virtual void RetryIfReady()
        {
            if (!IsRunning) {
                return;
            }

            if (_stateMachine.IsInState(ReplicationState.Idle)) {
                Log.D(TAG, "RETRYING, to transfer missed revisions...");
                _revisionsFailed = 0;
                CancelPendingRetryIfReady();
                Retry();
            }
            else {
                ScheduleRetryIfReady();
            }
        }

        /// <summary>
        /// Fires the specified trigger for the state machine
        /// </summary>
        /// <param name="trigger">The trigger to fire.</param>
        protected void FireTrigger(ReplicationTrigger trigger)
        {
            Log.D(TAG, "Preparing to fire {0}", trigger);
            var stackTrace = Environment.StackTrace;

            WorkExecutor.StartNew(() =>
            {
                try {
                    _stateMachine.Fire(trigger);
                } catch(Exception e) {
                    Log.E(TAG, "State machine error", e);
                    throw;
                }
            });
        }

        /// <summary>
        /// Cancels the next scheduled retry attempt
        /// </summary>
        protected virtual void CancelPendingRetryIfReady()
        {
            if (_retryIfReadyTokenSource != null) {
                _retryIfReadyTokenSource.Cancel();
            }
        }

        /// <summary>
        /// Schedules a call to retry if ready, using RetryDelay
        /// </summary>
        protected virtual void ScheduleRetryIfReady()
        {
            _retryIfReadyTokenSource = new CancellationTokenSource();
            var token = _retryIfReadyTokenSource.Token;
            Task.Delay(TimeSpan.FromSeconds(RETRY_DELAY)).ContinueWith(task =>
            {
                if (!token.IsCancellationRequested) {
                    RetryIfReady();
                }
            }, token);
        }

        /// <summary>
        /// Starts the replicator when it transitions into a running state
        /// </summary>
        protected virtual void StartInternal()
        {
            Log.I(TAG, "Attempting to start {0} ({1})", IsPull ? "puller" : "pusher", _replicatorID);
            if (!LocalDatabase.IsOpen) {
                Log.W(TAG, "Not starting because db.isOpen() returned false.");
                FireTrigger(ReplicationTrigger.StopImmediate);
                return;
            }

            if(!LocalDatabase.AddReplication(this) || !LocalDatabase.AddActiveReplication(this)) {
#if DEBUG
                var existing = LocalDatabase.AllReplicators.FirstOrDefault(x => x.RemoteCheckpointDocID() == RemoteCheckpointDocID());
                if(existing != null) {
                    Log.W(TAG, "Not starting because identical {0} already exists ({1})", IsPull ? "puller" : "pusher", existing._replicatorID);
                } else {
                    Log.E(TAG, "Not starting {0} for unknown reasons", IsPull ? "puller" : "pusher");
                }
#else
                Log.W(TAG, "Not starting becuse identical {0} already exists", IsPull ? "puller" : "pusher");
#endif
                FireTrigger(ReplicationTrigger.StopImmediate);
                return;
            }

            if (!LocalDatabase.Manager.NetworkReachabilityManager.CanReach(RemoteUrl.AbsoluteUri)) {
                LastError = LocalDatabase.Manager.NetworkReachabilityManager.LastError;
                FireTrigger(ReplicationTrigger.GoOffline);
                CheckOnlineLoop();
                return;
            }

            SetupRevisionBodyTransformationFunction();

            sessionID = string.Format("repl{0:000}", Interlocked.Increment(ref _lastSessionID));
            Log.I(TAG, "Beginning replication process...");
            LastSequence = null;

            CheckSession();

            var reachabilityManager = LocalDatabase.Manager.NetworkReachabilityManager;
            reachabilityManager.StatusChanged += NetworkStatusChanged;
        }

        /// <summary>
        /// Performs login logic for the remote endpoint
        /// </summary>
        protected virtual void Login()
        {
            var loginParameters = Authenticator.LoginParametersForSite(RemoteUrl);
            if (loginParameters == null)
            {
                Log.D(TAG, String.Format("{0}: {1} has no login parameters, so skipping login", this, Authenticator));
                FetchRemoteCheckpointDoc();
                return;
            }

            var loginPath = Authenticator.LoginPathForSite(RemoteUrl);
            Log.D(TAG, string.Format("{0}: Doing login with {1} at {2}", this, Authenticator.GetType(), loginPath));

            SendAsyncRequest(HttpMethod.Post, loginPath, loginParameters, (result, e) => {
                if (e != null)
                {
                    Log.D(TAG, "Login failed for path: {0}", loginPath);
                    Log.W(TAG, "Remote endpoint login failed!");
                    LastError = e;

                    // TODO: double check this behavior against iOS implementation, especially
                    // TODO: with regards to behavior of a continuous replication.
                    // Note: was added in order that unit test testReplicatorErrorStatus() finished and passed.
                    // (before adding this, the replication would just end up in limbo and never finish)
                    FireTrigger(ReplicationTrigger.StopGraceful);
                }
                else
                {
                    Log.D(TAG, string.Format("Successfully logged in!"));
                    FetchRemoteCheckpointDoc ();
                }
            });
        }

        /// <summary>
        /// Sets the last replication error that occurred
        /// </summary>
        /// <param name="error">The last replication error that occurred</param>
        [Obsolete("Set the LastError property directly instead")]
        protected void SetLastError(Exception error) {
            LastError = error;
        }

        /// <summary>
        /// Takes the action necessary to transition the replicator
        /// into an offline state
        /// </summary>
        protected virtual void PerformGoOffline()
        {

        }

        /// <summary>
        /// Takes the action necessary to transition the replicator
        /// into an online state
        /// </summary>
        protected virtual void PerformGoOnline()
        {

        }

        /// <summary>
        /// Safely increments the completed changes count
        /// </summary>
        protected void SafeIncrementCompletedChangesCount()
        {
            SafeAddToCompletedChangesCount(1);
        }

        /// <summary>
        /// Safely adds the specified value to the completed changes count
        /// </summary>
        /// <param name="value">The amount to add</param>
        protected void SafeAddToCompletedChangesCount(int value)
        {
            if (value == 0) 
            {
                return;
            }

            Log.V(TAG, "    >>>Updating completedChangesCount from {0} by {1} for {2}", _completedChangesCount, value, _replicatorID);
            var newCount = Interlocked.Add(ref _completedChangesCount, value);
            Log.V(TAG, "    <<<Updated completedChanges count to {0} for {1}", _completedChangesCount, _replicatorID);
            NotifyChangeListeners();
            if (newCount == _changesCount && IsSafeToStop) {
                if(Continuous) {
                    FireTrigger(ReplicationTrigger.WaitingForChanges);
                } else {
                    FireTrigger(ReplicationTrigger.StopGraceful);
                }
            }
        }

        /// <summary>
        /// Safely adds the specified value to the changes count
        /// </summary>
        /// <param name="value">The amount to add</param>
        protected void SafeAddToChangesCount(int value)
        {
            if (value == 0) 
            {
                return;
            }

            Log.V(TAG, "    >>>Updating changesCount from {0} by {1} for {2}", _changesCount, value, _replicatorID);
            Interlocked.Add(ref _changesCount, value);
            Log.V(TAG, "    <<<Updated changesCount to {0} for {1}", _changesCount, _replicatorID);
            if(Continuous) {
                FireTrigger(ReplicationTrigger.Resume);
            }

            NotifyChangeListeners();
        }

        /// <summary>
        /// Shuts down the replication, waiting for any in progress / scheduled
        /// actions to finish
        /// </summary>
        protected virtual void StopGraceful()
        {
            Log.D(TAG, "{0}: Stop Graceful...", _replicatorID);

            _continuous = false;
            if (Batcher != null)  {
                Batcher.Clear();
            }

            CancelPendingRetryIfReady();
            FireTrigger(ReplicationTrigger.StopImmediate);
        }

        #endregion

        #region Internal Methods

        internal abstract void BeginReplicating();

        internal abstract void ProcessInbox(RevisionList inbox);

        internal bool HasSameSettingsAs(Replication other)
        {
            return LocalDatabase == other.LocalDatabase &&
            IsPull == other.IsPull &&
            RemoteCheckpointDocID("").Equals(other.RemoteCheckpointDocID(""));
        }

        internal virtual void Stopping()
        {
            Log.V(TAG, "    {0} Stopping", _replicatorID);
            if(!LocalDatabase.IsOpen) {
                return; // This logic has already been handled by DatabaseClosing()
            }

            lastSequenceChanged = true; // force save the sequence
            SaveLastSequence (() => 
            {
                var reachabilityManager = LocalDatabase.Manager.NetworkReachabilityManager;
                if (reachabilityManager != null) {
                    reachabilityManager.StatusChanged -= NetworkStatusChanged;
                }

                LocalDatabase.ForgetReplication(this);
            });
        }

        internal HttpRequestMessage SendAsyncRequest(HttpMethod method, string relativePath, object body, RemoteRequestCompletionBlock completionHandler, CancellationTokenSource requestTokenSource = null)
        {
            try {
                var urlStr = BuildRelativeURLString(relativePath);
                var url = new Uri(urlStr);
                return SendAsyncRequest(method, url, body, completionHandler, requestTokenSource);
            } catch (UriFormatException e) {
                Log.E(TAG, "Malformed URL for async request", e);
                throw;
            } catch (Exception e) {
                Log.E(TAG, "Unhandled exception", e);
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

        internal HttpRequestMessage SendAsyncRequest(HttpMethod method, Uri url, Object body, RemoteRequestCompletionBlock completionHandler, CancellationTokenSource requestTokenSource = null)
        {
            var message = new HttpRequestMessage(method, url);
            var mapper = Manager.GetObjectMapper();
            message.Headers.Add("Accept", new[] { "multipart/related", "application/json" });


            var client = _clientFactory.GetHttpClient(_cookieStore, true);
            var challengeResponseAuth = Authenticator as IChallengeResponseAuthenticator;
            if (challengeResponseAuth != null) {
                var authHandler = _clientFactory.Handler as DefaultAuthHandler;
                if (authHandler != null) {
                    authHandler.Authenticator = challengeResponseAuth;
                }

                challengeResponseAuth.PrepareWithRequest(message);
            }

            var authHeader = AuthUtils.GetAuthenticationHeaderValue(Authenticator, message.RequestUri);
            if (authHeader != null) {
                client.DefaultRequestHeaders.Authorization = authHeader;
            }

            var bytes = default(byte[]);
            if (body != null) {
                bytes = mapper.WriteValueAsBytes(body).ToArray();
                var byteContent = new ByteArrayContent(bytes);
                message.Content = byteContent;
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            var token = requestTokenSource != null 
                ? requestTokenSource.Token
                : CancellationTokenSource.Token;

            Log.D(TAG, "{0} - Sending async {1} request to: {2}", _replicatorID, method, url);
            var t = client.SendAsync(message, token).ContinueWith(response =>
            {
                try {
                    HttpResponseMessage result = null;
                    Exception error = null;
                    if(!response.IsFaulted && !response.IsCanceled) {
                        result = response.Result;
                        UpdateServerType(result);
                    } else if(response.IsFaulted) {
                        error = response.Exception.InnerException;
                        Log.E(TAG, "Http Message failed to send: {0}", message);
                        Log.E(TAG, "Http exception", response.Exception.InnerException);
                        if(bytes != null) {
                            try {
                                Log.E(TAG, "\tFailed content: {0}", Encoding.UTF8.GetString(bytes));
                            } catch(ObjectDisposedException) {}
                        }
                    }

                    if(completionHandler != null) {
                        object fullBody = null;

                        try {
                            if(response.Status != TaskStatus.RanToCompletion) {
                                Log.D(TAG, "SendAsyncRequest did not run to completion.");
                            }

                            if(response.IsCanceled) {
                                error = new Exception("SendAsyncRequest Task has been canceled.");
                            } else {
                                error = error is AggregateException
                                    ? response.Exception.Flatten()
                                    : response.Exception;
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
                            Log.E(TAG, "SendAsyncRequest has an error occurred.", e);
                        }

                        completionHandler(fullBody, error);
                    }

                    if(result != null) {
                        result.Dispose();
                    }
                } finally {
                    Task dummy;
                    _requests.TryRemove(message, out dummy);
                    client.Dispose();
                }
            }, token);

            _requests.AddOrUpdate(message, k => t, (k, v) => t);
            return message;
        }

        internal void SendAsyncMultipartDownloaderRequest(HttpMethod method, string relativePath, object body, Database db, RemoteRequestCompletionBlock onCompletion)
        {
            try {
                var urlStr = BuildRelativeURLString(relativePath);
                var url = new Uri(urlStr);

                var message = new HttpRequestMessage(method, url);
                message.Headers.Add("Accept", "*/*");
                AddRequestHeaders(message);

                var client = _clientFactory.GetHttpClient(_cookieStore, true);
                var challengeResponseAuth = Authenticator as IChallengeResponseAuthenticator;
                if (challengeResponseAuth != null) {
                    var authHandler = _clientFactory.Handler as DefaultAuthHandler;
                    if (authHandler != null) {
                        authHandler.Authenticator = challengeResponseAuth;
                    }

                    challengeResponseAuth.PrepareWithRequest(message);
                }

                var authHeader = AuthUtils.GetAuthenticationHeaderValue(Authenticator, message.RequestUri);
                if (authHeader != null) {
                    client.DefaultRequestHeaders.Authorization = authHeader;
                }

                var request = client.SendAsync(message, CancellationTokenSource.Token).ContinueWith(new Action<Task<HttpResponseMessage>>(responseMessage =>
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
                        //CouchbaseLiteHttpClientFactory.Instance.AddCookies(clientFactory.HttpHandler.CookieContainer.GetCookies(url));

                        var status = response.StatusCode;
                        if ((Int32)status.GetStatusCode() >= 300) {
                            Log.E(TAG, "Got error {0}", status.GetStatusCode());
                            Log.E(TAG, "Request was for: " + message);
                            Log.E(TAG, "Status reason: " + response.ReasonPhrase);
                            error = new HttpResponseException(status);
                            if(onCompletion != null) {
                                onCompletion(null, error);
                            }
                        } else {
                            var entity = response.Content;
                            var contentTypeHeader = response.Content.Headers.ContentType;
                            Stream inputStream = null;
                            if (contentTypeHeader != null && contentTypeHeader.ToString().Contains("multipart/related")) {
                                try {
                                    var reader = new MultipartDocumentReader(LocalDatabase);
                                    var contentType = contentTypeHeader.ToString();
                                    reader.SetContentType(contentType);

                                    var inputStreamTask = entity.ReadAsStreamAsync();
                                    inputStream = inputStreamTask.Result;

                                    const int bufLen = 1024;
                                    var buffer = new byte[bufLen];

                                    int numBytesRead = 0;
                                    while ((numBytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0) {
                                        if (numBytesRead != bufLen) {
                                            var bufferToAppend = new Couchbase.Lite.Util.ArraySegment<Byte>(buffer, 0, numBytesRead);
                                            reader.AppendData(bufferToAppend);
                                        } else {
                                            reader.AppendData(buffer);
                                        }
                                    }

                                    reader.Finish();
                                    fullBody = reader.GetDocumentProperties();

                                    if (onCompletion != null) {
                                        onCompletion(fullBody, error);
                                    }
                                } catch (Exception ex) {
                                    Log.E(TAG, "SendAsyncMultipartDownloaderRequest has an error occurred.", ex);
                                } finally {
                                    try {
                                        inputStream.Close();
                                    } catch (Exception) { }
                                }
                            } else {
                                if (entity != null) {
                                    try {
                                        var readTask = entity.ReadAsStreamAsync();
                                        //readTask.Wait(); // TODO: This should be scaled based on content length.
                                        inputStream = readTask.Result;
                                        fullBody = Manager.GetObjectMapper().ReadValue<Object>(inputStream);
                                        if (onCompletion != null)
                                            onCompletion(fullBody, error);
                                    } catch (Exception ex) {
                                        Log.E(TAG, "SendAsyncMultipartDownloaderRequest has an error occurred.", ex);
                                    } finally {
                                        try {
                                            inputStream.Close();
                                        } catch (Exception) { }
                                    }
                                }
                            }
                        }
                    } catch (System.Net.ProtocolViolationException e) {
                        Log.E(TAG, "client protocol exception", e);
                        error = e;
                    } catch (IOException e) {
                        Log.E(TAG, "IO Exception", e);
                        error = e;
                    } finally {
                        Task dummy;
                        client.Dispose();
                        _requests.TryRemove(message, out dummy);
                        responseMessage.Result.Dispose();
                    }
                }), WorkExecutor.Scheduler);
                _requests.TryAdd(message, request);
            } catch (UriFormatException e) {
                Log.E(TAG, "Malformed URL for async request", e);
            }
        }

        internal void SendAsyncMultipartRequest(HttpMethod method, String relativePath, MultipartContent multiPartEntity, RemoteRequestCompletionBlock completionHandler)
        {
            Uri url = null;
            try {
                var urlStr = BuildRelativeURLString(relativePath);
                url = new Uri(urlStr);
            } catch (UriFormatException e) {
                throw new ArgumentException("Invalid URI format.", e);
            }

            var message = new HttpRequestMessage(method, url);
            message.Content = multiPartEntity;
            message.Headers.Add("Accept", "*/*");

            var client = _clientFactory.GetHttpClient(_cookieStore, true);
            var challengeResponseAuth = Authenticator as IChallengeResponseAuthenticator;
            if(challengeResponseAuth != null) {
                var authHandler = _clientFactory.Handler as DefaultAuthHandler;
                if(authHandler != null) {
                    authHandler.Authenticator = challengeResponseAuth;
                }
                challengeResponseAuth.PrepareWithRequest(message);
            }

            var authHeader = AuthUtils.GetAuthenticationHeaderValue(Authenticator, message.RequestUri);
            if (authHeader != null) {
                client.DefaultRequestHeaders.Authorization = authHeader;
            }

            var t = client.SendAsync(message, CancellationTokenSource.Token).ContinueWith(response=> 
            {
                multiPartEntity.Dispose();
                if (response.Status != TaskStatus.RanToCompletion)
                {
                    LastError = response.Exception;
                    Log.E(TAG, "SendAsyncRequest did not run to completion.");
                    client.Dispose();
                    return Task.FromResult((Stream)null);
                }
                if ((Int32)response.Result.StatusCode > 300) {
                    LastError = new HttpResponseException(response.Result.StatusCode);
                    Log.E(TAG, "Server returned HTTP Error", LastError);
                    client.Dispose();
                    return Task.FromResult((Stream)null);
                }
                return response.Result.Content.ReadAsStreamAsync();
            }, CancellationTokenSource.Token).ContinueWith(response=> 
            {
                try {
                    var hasEmptyResult = response.Result == null || response.Result.Result == null || response.Result.Result.Length == 0;
                    if (response.Status != TaskStatus.RanToCompletion) {
                        Log.E (TAG, "SendAsyncRequest did not run to completion.");
                    } else if (hasEmptyResult) {
                        Log.E (TAG, "Server returned an empty response.");
                    }

                    if (completionHandler != null) {
                        object fullBody = null;
                        if (!hasEmptyResult) {
                            var mapper = Manager.GetObjectMapper();
                            fullBody = mapper.ReadValue<Object> (response.Result.Result);
                        }

                        completionHandler (fullBody, response.Exception ?? LastError);
                    }
                } finally {
                    Task dummy;
                    _requests.TryRemove(message, out dummy);
                    client.Dispose();
                }
            }, CancellationTokenSource.Token);
            _requests.TryAdd(message, t);
        }

        // Pusher overrides this to implement the .createTarget option
        /// <summary>This is the _local document ID stored on the remote server to keep track of state.
        ///     </summary>
        /// <remarks>
        /// This is the _local document ID stored on the remote server to keep track of state.
        /// Its ID is based on the local database ID (the private one, to make the result unguessable)
        /// and the remote database's URL.
        /// </remarks>
        internal string RemoteCheckpointDocID(string localUUID)
        {
            if (!LocalDatabase.IsOpen) {
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
            spec.Put("localUUID", localUUID);
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

            string remoteUUID;
            var hasValue = Options.TryGetValue<string>(ReplicationOptionsDictionary.REMOTE_UUID_KEY, out remoteUUID);
            if (hasValue) {
                spec["remoteURL"] = remoteUUID;
            } else {
                spec["remoteURL"] = RemoteUrl.AbsoluteUri;
            }

            IEnumerable<byte> inputBytes = null;
            try {
                inputBytes = Manager.GetObjectMapper().WriteValueAsBytes(spec);
            } catch (IOException e) {
                throw new RuntimeException(e);
            }

            return Misc.HexSHA1Digest(inputBytes);
        }

        internal string RemoteCheckpointDocID()
        {
            if (_remoteCheckpointDocID == null) {
                _remoteCheckpointDocID = RemoteCheckpointDocID(LocalDatabase.PrivateUUID());
            }

            return _remoteCheckpointDocID;
        }

        internal RevisionInternal TransformRevision(RevisionInternal rev)
        {
            if (RevisionBodyTransformationFunction != null) {
                try {
                    var generation = rev.GetGeneration();
                    var xformed = RevisionBodyTransformationFunction(rev);
                    if (xformed == null) {
                        return null;
                    }

                    if (xformed != rev) {
                        Debug.Assert((xformed.GetDocId().Equals(rev.GetDocId())));
                        Debug.Assert((xformed.GetRevId().Equals(rev.GetRevId())));
                        Debug.Assert((xformed.GetProperties().Get("_revisions").Equals(rev.GetProperties().Get("_revisions"))));

                        if (xformed.GetProperties().ContainsKey("_attachments")) {
                            // Insert 'revpos' properties into any attachments added by the callback:
                            var mx = new RevisionInternal(xformed.GetProperties());
                            xformed = mx;
                            mx.MutateAttachments((name, info) =>
                            {
                                if (info.Get("revpos") != null) {
                                    return info;
                                }

                                if (info.Get("data") == null) {
                                    throw new InvalidOperationException("Transformer added attachment without adding data");
                                }

                                var newInfo = new Dictionary<string, object>(info);
                                newInfo["revpos"] = generation;
                                return newInfo;
                            });
                        }
                    }
                } catch (Exception e) {
                    Log.W(TAG, String.Format("Exception transforming a revision of doc '{0}'", rev.GetDocId()), e);
                }
            }

            return rev;
        }

        // This method will be used by Router & Reachability Manager
        internal void GoOffline()
        {
            FireTrigger(ReplicationTrigger.GoOffline);
        }

        // This method will be used by Router & Reachability Manager
        internal void GoOnline()
        {
            FireTrigger(ReplicationTrigger.GoOnline);
        }

        internal void StopRemoteRequests()
        {
            var cts = CancellationTokenSource;
            CancellationTokenSource = new CancellationTokenSource();
            if (!cts.IsCancellationRequested) {
                cts.Cancel();
            }
        }

        internal void DatabaseClosing(CountdownEvent evt)
        {
            Log.I(TAG, "Database closed while replication running, shutting down");
            lastSequenceChanged = true; // force save the sequence
            SaveLastSequence(() => {
                var reachabilityManager = LocalDatabase.Manager.NetworkReachabilityManager;
                if (reachabilityManager != null) {
                    reachabilityManager.StatusChanged -= NetworkStatusChanged;
                }

                Stop();
                evt.Signal();
            });
        }

        internal void AddToInbox(RevisionInternal rev)
        {
            Debug.Assert(IsRunning);
            Batcher.QueueObject(rev);
        }

        internal void CheckSession()
        {
            if (Authenticator != null && Authenticator.UsesCookieBasedLogin) {
                CheckSessionAtPath("/_session");
            }
            else {
                FetchRemoteCheckpointDoc();
            }
        }

        #endregion

        #region Private Methods

        private void CheckOnlineLoop()
        {
            // Check at intervals to see if connection has been restored (in case
            // the offline status is the result of the *server* being offline)
            Task.Delay(TimeSpan.FromSeconds(RETRY_DELAY)).ContinueWith(t =>
            {
                if(_stateMachine.State != ReplicationState.Offline) {
                    return;
                }

                FireTrigger(ReplicationTrigger.GoOnline);
                CheckOnlineLoop();
            });
        }

        private void WaitForStopped (object sender, ReplicationChangeEventArgs e)
        {
            if (e.Status != ReplicationStatus.Stopped) {
                return;
            }
                
            Changed -= WaitForStopped;
            Start();
        }

        private void FetchRemoteCheckpointDoc()
        {
            lastSequenceChanged = false;
            var checkpointId = RemoteCheckpointDocID();
            var localLastSequence = LocalDatabase.LastSequenceWithCheckpointId(checkpointId);

            if (localLastSequence == null && GetLastSequenceFromLocalCheckpoint() == null) {
                Log.I(TAG, "No local checkpoint, not getting remote one");
                MaybeCreateRemoteDB();
                BeginReplicating();
                return;
            }

            SendAsyncRequest(HttpMethod.Get, "/_local/" + checkpointId, null, (response, e) => 
            {
                try {
                    if (e != null && !Is404 (e)) {
                        Log.W(TAG, String.Format("Error getting remote checkpoint for {0}: ", _replicatorID), e);
                        LastError = e;
                        FireTrigger(ReplicationTrigger.StopGraceful);
                    } else {
                        if (e != null && Is404 (e)) {
                            Log.D(TAG, "404 error getting remote checkpoint " + RemoteCheckpointDocID() + ", calling maybeCreateRemoteDB");
                            MaybeCreateRemoteDB();
                        }

                        IDictionary<string, object> result = null;

                        if (response != null) {
                            result = response.AsDictionary<string, object>();
                            _remoteCheckpoint = result;
                        }
                        _remoteCheckpoint = result;
                        string remoteLastSequence = null;

                        if (result != null) {
                            remoteLastSequence = (string)result.Get("lastSequence");
                        }

                        if (remoteLastSequence != null && remoteLastSequence.Equals (localLastSequence)) {
                            LastSequence = localLastSequence;
                            if(LastSequence == null) {
                                // Try to get the last sequence from the local checkpoint document
                                // created only when importing a database. This allows the
                                // replicator to continue replicating from the current local checkpoint
                                // of the imported database after importing.
                                _lastSequence = GetLastSequenceFromLocalCheckpoint();
                            }

                            Log.V(TAG, "{0} replicating from lastSequence={1}", _replicatorID, LastSequence);
                        } else {
                            Log.V(TAG, "{0} lastSequence mismatch: I had {1}, remote had {2}", _replicatorID, localLastSequence, remoteLastSequence);
                        }

                        BeginReplicating ();
                    }
                } catch (Exception ex) {
                    Log.E(TAG, String.Format("Error analyzing _local response for {0}", _replicatorID), ex);
                }
            });
        }

        // If the local database has been copied from one pre-packaged in the app, this method returns
        // a pre-existing checkpointed sequence to start from. This allows first-time replication to be
        // fast and avoid starting over from sequence zero.
        private string GetLastSequenceFromLocalCheckpoint()
        {
            lastSequenceChanged = false;
            var db = LocalDatabase;
            var doc = db.GetLocalCheckpointDoc();
            if (doc != null) {
                var localUUID = doc.GetCast<string>(LOCAL_CHECKPOINT_LOCAL_UUID_KEY);
                if (localUUID != null) {
                    var checkpointID = RemoteCheckpointDocID(localUUID);
                    return db.LastSequenceWithCheckpointId(checkpointID);
                }
            }

            return null;
        }

        private static bool Is404(Exception e)
        {
            if (e is Couchbase.Lite.HttpResponseException) {
                return ((HttpResponseException)e).StatusCode == System.Net.HttpStatusCode.NotFound;
            }

            return (e is HttpResponseException) && ((HttpResponseException)e).StatusCode == System.Net.HttpStatusCode.NotFound;
        }

        private void SaveLastSequence(SaveLastSequenceCompletionBlock completionHandler)
        {
            if (!lastSequenceChanged) {
                if (completionHandler != null) {
                    completionHandler();
                }
                return;
            }

            if (_savingCheckpoint) {
                // If a save is already in progress, don't do anything. (The completion block will trigger
                // another save after the first one finishes.)
                Task.Delay(500).ContinueWith(t => SaveLastSequence(completionHandler));
                return;
            }

            lastSequenceChanged = false;

            Log.D(TAG, "SaveLastSequence() called for {0}. lastSequence: {1}", _replicatorID, LastSequence);

            var body = new Dictionary<String, Object>();
            if (_remoteCheckpoint != null) {
                body.PutAll(_remoteCheckpoint);
            }

            body["lastSequence"] = LastSequence;
            var remoteCheckpointDocID = RemoteCheckpointDocID();
            if (String.IsNullOrEmpty(remoteCheckpointDocID)) {
                Log.W(TAG, "remoteCheckpointDocID is null for {0}, aborting SaveLastSequence()", _replicatorID);
                return;
            }

            _savingCheckpoint = true;
            var message = SendAsyncRequest(HttpMethod.Put, "/_local/" + remoteCheckpointDocID, body, (result, e) => 
            {
                _savingCheckpoint = false;
                if (e != null) {
                    Log.W(TAG, String.Format("Unable to save remote checkpoint for {0}", _replicatorID), e);
                    switch (GetStatusFromError(e)) {
                        case StatusCode.NotFound:
                            _remoteCheckpoint = null;
                            break;
                        case StatusCode.Conflict:
                            RefreshRemoteCheckpointDoc();
                            break;
                        default:
                            // TODO: On 401 or 403, and this is a pull, remember that remote
                            // TODO: is read-only & don't attempt to read its checkpoint next time.
                            break;
                    }
                } else {
                    Log.D(TAG, "Save checkpoint response for {0}: {1}", _replicatorID, result.ToString());
                    var response = result.AsDictionary<string, object>();
                    body.Put ("_rev", response.Get ("rev"));
                    _remoteCheckpoint = body;
                    var localDb = LocalDatabase;
                    if(localDb.Storage == null) {
                        Log.W(TAG, "Database is null or closed, ignoring remote checkpoint response");
                        if(completionHandler != null) {
                            completionHandler();
                        }
                        return;
                    }

                    localDb.SetLastSequence(LastSequence, remoteCheckpointDocID);
                }

                if (completionHandler != null) {
                    completionHandler ();
                }
            });

            // This request should not be canceled when the replication is told to stop:
            Task dummy;
            _requests.TryRemove(message, out dummy);
        }

        private void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach (string requestHeaderKey in RequestHeaders.Keys) {
                request.Headers.Add(requestHeaderKey, RequestHeaders.Get(requestHeaderKey).ToString());
            }
        }

        private void UpdateServerType(HttpResponseMessage response)
        {
            var server = response.Headers.Server;
            if (server != null && server.Any()) {
                var serverString = String.Join(" ", server.Select(pi => pi.Product).Where(pi => pi != null).ToStringArray());
                ServerType = new RemoteServerVersion(serverString);
                Log.V(TAG, "Server Version: " + ServerType);
            }
        }

        private StatusCode GetStatusFromError(Exception e)
        {
            var couchbaseLiteException = e as CouchbaseLiteException;
            if (couchbaseLiteException != null) {
                return couchbaseLiteException.CBLStatus.Code;
            }

            var httpException = e as HttpResponseException;
            if(httpException != null) {
                return (StatusCode)httpException.StatusCode;
            }


            return StatusCode.Unknown;
        }

        private void RefreshRemoteCheckpointDoc()
        {
            Log.D(TAG, "Refreshing remote checkpoint to get its _rev...");
            _savingCheckpoint = true;

            SendAsyncRequest(HttpMethod.Get, "/_local/" + RemoteCheckpointDocID(), null, (result, e) =>
            {
                if (!LocalDatabase.IsOpen) {
                    Log.W(TAG, "Db closed while refreshing remote checkpoint.  Aborting");
                    return;
                }

                _savingCheckpoint = false;

                if (e != null && GetStatusFromError(e) != StatusCode.NotFound) {
                    Log.E(TAG, "Error refreshing remote checkpoint", e);
                } else {
                    Log.D(TAG, "Refreshed remote checkpoint: " + result);
                    _remoteCheckpoint = result.AsDictionary<string, object>();
                    lastSequenceChanged = true;
                    SaveLastSequence(null);
                }         
            });
        }

        private void SetupRevisionBodyTransformationFunction()
        {
            var xformer = TransformationFunction;
            if (xformer != null)
            {
                RevisionBodyTransformationFunction = (rev) =>
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

                        var nuRev = new RevisionInternal (rev.GetProperties ());
                        nuRev.SetProperties (xformedProperties);
                        return nuRev;
                    }
                    return rev;
                };
            }
        }

        private void CheckSessionAtPath(string sessionPath)
        {
            SendAsyncRequest(HttpMethod.Get, sessionPath, null, (result, e) => {
                if (e != null)
                {
                    if (e is WebException && ((WebException)e).Status == System.Net.WebExceptionStatus.ProtocolError && ((HttpWebResponse)((WebException)e).Response).StatusCode == System.Net.HttpStatusCode.NotFound
                        && sessionPath.Equals("/_session", StringComparison.InvariantCultureIgnoreCase)) {
                        CheckSessionAtPath ("_session");
                        return;
                    }
                    Log.W(TAG, "Session check failed", e);
                    LastError = e;
                }
                else
                {
                    var response = result.AsDictionary<string, object>();
                    var userCtx = response.Get("userCtx").AsDictionary<string, object>();
                    var username = (string)userCtx.Get ("name");

                    if (!string.IsNullOrEmpty (username)) {
                        Log.D (TAG, string.Format ("{0} Active session, logged in as {1}", this, username));
                        FetchRemoteCheckpointDoc ();
                    } else {
                        Log.D (TAG, string.Format ("{0} No active session, going to login", this));
                        Login ();
                    }
                }
            });
        }

        private void NetworkStatusChanged(object sender, NetworkReachabilityChangeEventArgs e)
        {
            if (e.Status == NetworkReachabilityStatus.Reachable) {
                GoOnline();
            }
            else {
                GoOffline();
            }
        }
  
        private void InitializeStateMachine()
        {
            // hierarchy
            _stateMachine.Configure(ReplicationState.Idle).SubstateOf(ReplicationState.Running);
            _stateMachine.Configure(ReplicationState.Offline).SubstateOf(ReplicationState.Running);

            // permitted transitions
            _stateMachine.Configure(ReplicationState.Initial).Permit(ReplicationTrigger.Start, ReplicationState.Running);
            _stateMachine.Configure(ReplicationState.Idle).Permit(ReplicationTrigger.Resume, ReplicationState.Running);
            _stateMachine.Configure(ReplicationState.Running).Permit(ReplicationTrigger.WaitingForChanges, ReplicationState.Idle);
            _stateMachine.Configure(ReplicationState.Running).Permit(ReplicationTrigger.StopImmediate, ReplicationState.Stopped);
            _stateMachine.Configure(ReplicationState.Running).Permit(ReplicationTrigger.StopGraceful, ReplicationState.Stopping);
            _stateMachine.Configure(ReplicationState.Running).Permit(ReplicationTrigger.GoOffline, ReplicationState.Offline);
            _stateMachine.Configure(ReplicationState.Offline).PermitIf(ReplicationTrigger.GoOnline, ReplicationState.Running, 
                () => LocalDatabase.Manager.NetworkReachabilityManager.CanReach(RemoteUrl.AbsoluteUri));
            
            _stateMachine.Configure(ReplicationState.Stopping).Permit(ReplicationTrigger.StopImmediate, ReplicationState.Stopped);
            _stateMachine.Configure(ReplicationState.Stopped).Permit(ReplicationTrigger.Start, ReplicationState.Running);

            // ignored transitions
            _stateMachine.Configure(ReplicationState.Running).Ignore(ReplicationTrigger.Start);
            _stateMachine.Configure(ReplicationState.Initial).Ignore(ReplicationTrigger.StopGraceful);
            _stateMachine.Configure(ReplicationState.Stopping).Ignore(ReplicationTrigger.StopGraceful);
            _stateMachine.Configure(ReplicationState.Stopped).Ignore(ReplicationTrigger.StopGraceful);
            _stateMachine.Configure(ReplicationState.Stopped).Ignore(ReplicationTrigger.StopImmediate);
            _stateMachine.Configure(ReplicationState.Stopping).Ignore(ReplicationTrigger.WaitingForChanges);
            _stateMachine.Configure(ReplicationState.Stopped).Ignore(ReplicationTrigger.WaitingForChanges);
            _stateMachine.Configure(ReplicationState.Offline).Ignore(ReplicationTrigger.WaitingForChanges);
            _stateMachine.Configure(ReplicationState.Initial).Ignore(ReplicationTrigger.GoOffline);
            _stateMachine.Configure(ReplicationState.Stopping).Ignore(ReplicationTrigger.GoOffline);
            _stateMachine.Configure(ReplicationState.Stopped).Ignore(ReplicationTrigger.GoOffline);
            _stateMachine.Configure(ReplicationState.Offline).Ignore(ReplicationTrigger.GoOffline);
            _stateMachine.Configure(ReplicationState.Initial).Ignore(ReplicationTrigger.GoOnline);
            _stateMachine.Configure(ReplicationState.Running).Ignore(ReplicationTrigger.GoOnline);
            _stateMachine.Configure(ReplicationState.Stopping).Ignore(ReplicationTrigger.GoOnline);
            _stateMachine.Configure(ReplicationState.Stopped).Ignore(ReplicationTrigger.GoOnline);
            _stateMachine.Configure(ReplicationState.Idle).Ignore(ReplicationTrigger.GoOnline);
            _stateMachine.Configure(ReplicationState.Offline).Ignore(ReplicationTrigger.Resume);
            _stateMachine.Configure(ReplicationState.Initial).Ignore(ReplicationTrigger.Resume);
            _stateMachine.Configure(ReplicationState.Running).Ignore(ReplicationTrigger.Resume);
            _stateMachine.Configure(ReplicationState.Stopping).Ignore(ReplicationTrigger.Resume);
            _stateMachine.Configure(ReplicationState.Stopped).Ignore(ReplicationTrigger.Resume);

            // actions
            _stateMachine.Configure(ReplicationState.Running).OnEntry(transition =>
            {
                Log.V(TAG, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                StartInternal();
                NotifyChangeListenersStateTransition(transition);
            });

            _stateMachine.Configure(ReplicationState.Running).OnExit(transition =>
               Log.V(TAG, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID));

            _stateMachine.Configure(ReplicationState.Idle).OnEntry(transition =>
            {
                Log.V(TAG, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                if(transition.Source == transition.Destination) {
                    return;
                }

                if(_revisionsFailed > 0) {
                    ScheduleRetryIfReady();
                }

                NotifyChangeListenersStateTransition(transition);
            });

            _stateMachine.Configure(ReplicationState.Offline).OnEntry(transition =>
            {
                Log.V(TAG, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                PerformGoOffline();
                NotifyChangeListenersStateTransition(transition);
            });

            _stateMachine.Configure(ReplicationState.Offline).OnExit(transition =>
            {
                Log.V(TAG, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                PerformGoOnline();
                NotifyChangeListenersStateTransition(transition);
            });

            _stateMachine.Configure(ReplicationState.Stopping).OnEntry(transition =>
            {
                Log.V(TAG, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                if(transition.Source == transition.Destination) {
                    Log.I(TAG, "Concurrency issue with ReplicationState.Stopping");
                    return;
                }

                NotifyChangeListenersStateTransition(transition);
                StopGraceful();
            });

            _stateMachine.Configure(ReplicationState.Stopped).OnEntry(transition =>
            {
                Log.V(TAG, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                Stopping();

                if(transition.Source == transition.Destination) {
                    Log.I(TAG, "Concurrency issue with ReplicationState.Stopped");
                    return;
                }

                NotifyChangeListenersStateTransition(transition);
            });
        }

        private void NotifyChangeListenersStateTransition(StateMachine<ReplicationState, ReplicationTrigger>.Transition transition)
        {
            var stateTransition = new ReplicationStateTransition(transition);
            NotifyChangeListeners(stateTransition);
        }

        private void NotifyChangeListeners(ReplicationStateTransition transition = null) 
        {
            Log.V(TAG, "NotifyChangeListeners ({0}/{1}, state={2} (batch={3}, net={4}))",
                CompletedChangesCount, ChangesCount,
                _stateMachine.State, Batcher == null ? 0 : Batcher.Count(), _requests.Count);

            _pendingDocumentIDs = null;
            var evt = _changed;
            if (evt == null) {
                return;
            }

            var args = new ReplicationChangeEventArgs(this, transition);

            // Ensure callback runs on captured context, which should be the UI thread.
            var stackTrace = Environment.StackTrace;

            lock(_eventQueue) {
                _eventQueue.Enqueue(args);
            }

            if (_eventContext != null) {
                _eventContext.StartNew(() =>
                {
                    lock (_eventQueue) { 
                        while (_eventQueue.Count > 0) {
                            evt(this, _eventQueue.Dequeue());
                        }
                    }
                });
            }
        }

        #endregion
    }

    #region EventArgs Subclasses

    ///
    /// <see cref="Couchbase.Lite.Replication"/> Change Event Arguments.
    ///
    public class ReplicationChangeEventArgs : EventArgs
    {
        private readonly Replication _source;
        private readonly ReplicationStateTransition _transition;
        private readonly int _changesCount;
        private readonly int _completedChangesCount;
        private readonly ReplicationStatus _status;
        private readonly Exception _lastError;

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Replication"/> that raised the event.  Do not
        /// rely on this variable for the current state of the replicator as it may have changed
        /// between the time the args were created and the time that the event was raised.
        /// Instead use the various other properties.
        /// </summary>
        public Replication Source 
        {
            get { return _source; }
        }

        /// <summary>
        /// Gets the number of changes scheduled for the replication at the
        /// time the event was created.
        /// </summary>
        public int ChangesCount
        {
            get { return _changesCount; }
        }

        /// <summary>
        /// Gets the number of changes completed by the replication at the
        /// time the event was created.
        /// </summary>
        public int CompletedChangesCount
        {
            get { return _completedChangesCount; }
        }

        /// <summary>
        /// Gets the status of the replication at the time the event was created
        /// </summary>
        public ReplicationStatus Status
        {
            get { return _status; }
        }

        /// <summary>
        /// Gets the transition
        /// </summary>
        /// <value>The replication state transition.</value>
        public ReplicationStateTransition ReplicationStateTransition 
        {
            get { return _transition; }
        }

        /// <summary>
        /// Gets the most recent error that occured at the time of this change
        /// </summary>
        /// <value>The last error.</value>
        public Exception LastError
        {
            get { return _lastError; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Couchbase.Lite.ReplicationChangeEventArgs"/> class.
        /// </summary>
        /// <param name="sender">The <see cref="Couchbase.Lite.Replication"/> that raised the event.</param>
        /// <param name="transition">The transition that caused the state in the replication, if applicable</param>
        public ReplicationChangeEventArgs (Replication sender, ReplicationStateTransition transition)
        {
            _source = sender;
            _transition = transition;
            _changesCount = sender.ChangesCount;
            _completedChangesCount = sender.CompletedChangesCount;
            _status = sender.Status;
            _lastError = sender.LastError;
        }
    }

    #endregion

    #region Delegates

    /// <summary>
    /// The signature of a method that transforms a set of properties
    /// </summary>
    public delegate IDictionary<string, object> PropertyTransformationDelegate(IDictionary<string, object> propertyBag);

    internal delegate void SaveLastSequenceCompletionBlock();

    #endregion

}
