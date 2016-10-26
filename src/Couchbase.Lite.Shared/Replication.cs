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
using Couchbase.Lite.Replicator;
using Stateless;
using System.Collections.Concurrent;
using System.Text;
using Couchbase.Lite.Store;

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

#pragma warning disable 618

    /// <summary>
    /// Contains the keys for a replication options dictionary
    /// </summary>
    [Obsolete("Use ReplicationOptions instead")]
    public struct ReplicationOptionsDictionaryKeys
    {
        /// <summary>
        /// If specified, this will be used in place of the remote URL for calculating
        /// the remote checkpoint in the replication process.  Useful if the remote URL
        /// changes frequently (e.g. P2P discovery scenario)
        /// </summary>
        public static readonly string RemoteUUID = ReplicationOptionsDictionary.RemoteUUIDKey;
    }

    /// <summary>
    /// A class for holding replication options
    /// </summary>
    [DictionaryContract(OptionalKeys=new object[] { 
        ReplicationOptionsDictionary.RemoteUUIDKey, typeof(string)
    })]
    [Obsolete("This class is deprecated in favor of ReplicationOptions")]
    public sealed class ReplicationOptionsDictionary : ContractedDictionary
    {
        /// <summary>
        /// This key stores an ID for a remote endpoint whose identifier
        /// is likely to change (i.e. found via Bonjour)
        /// </summary>
        public const string REMOTE_UUID_KEY = "remoteUUID";

        internal const string RemoteUUIDKey = "remoteUUID";
        internal const string PollIntervalKey = "poll";
        internal const string PurgePushedKey = "purgePushed";
        internal const string AllNewKey = "allNew";
    }

#pragma warning restore 618

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
        [Obsolete("Use SyncProtocolVersion")]
        public const string SYNC_PROTOCOL_VERSION = "1.3";

        /// <summary>
        /// The protocol version to use when syncing with Sync Gateway.
        /// This value is also included in all HTTP requests as the
        /// User-Agent version.
        /// </summary>
        public static readonly string SyncProtocolVersion = "1.3";

        internal const string CHANNELS_QUERY_PARAM = "channels";
        internal const string BY_CHANNEL_FILTER_NAME = "sync_gateway/bychannel";
        internal const string REPLICATOR_DATABASE_NAME = "_replicator";
        internal const int INBOX_CAPACITY = 100;
        private static readonly TimeSpan ProcessorDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan SaveLastSequenceDelay = TimeSpan.FromSeconds(5);
        private const string Tag = nameof(Replication);
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

        /// <summary>
        /// The list of currently active HTTP requests
        /// </summary>
        [Obsolete("This field will no longer store active requests, use _requests")]
        protected ICollection<HttpClient> requests;

        /// <summary>
        /// The list of currently active HTTP messages
        /// </summary>
        [Obsolete("Refactored out, and will be removed")]
        protected ConcurrentDictionary<HttpRequestMessage, Task> _requests
        {
            get { return _remoteSession?._requests; }
        }

        /// <summary>
        /// Whether or not the LastSequence property has changed
        /// </summary>
        protected bool lastSequenceChanged;

        /// <summary>
        /// The ID of the replication session
        /// </summary>
        protected internal string sessionID;

        private bool _savingCheckpoint;
        private bool _restartEnabled;
        private IDictionary<string, object> _remoteCheckpoint;
        private bool _continuous;
        private int _revisionsFailed;
        private static int _lastSessionID;
        private string _remoteCheckpointDocID;
        private CancellationTokenSource _retryIfReadyTokenSource;
        
        private readonly Queue<ReplicationChangeEventArgs> _eventQueue = new Queue<ReplicationChangeEventArgs>();
        private HashSet<string> _pendingDocumentIDs;
        private long _pendingDocumentIDsSequence;
        private long _lastSequencePushed;
        private TaskFactory _eventContext; // Keep a separate reference since the localDB will be nulled on certain types of stop
        private Guid _replicatorID = Guid.NewGuid();
        private DateTime _startTime;
        internal RemoteSession _remoteSession;

        #endregion

        #region Properties

        /// <summary>
        /// If applicable, will store the username of the logged in user once
        /// they are authenticated
        /// </summary>
        public string Username { get; private set; }

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
                        Log.To.Sync.W(Tag, "filterChannels can only be set in pull replications, not setting...");
                        return;
                    }

                    Filter = BY_CHANNEL_FILTER_NAME;
                    var filterParams = new Dictionary<string, object>();
                    filterParams[CHANNELS_QUERY_PARAM] = String.Join(",", value.ToStringArray());
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
                    Log.To.Sync.I(Tag, "Error set during replication (application may continue)", value);
                    _lastError = value;
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
        public IAuthenticator Authenticator
        {
            get { return _remoteSession.Authenticator; }
            set { _remoteSession.Authenticator = value as IAuthorizer; }
        }

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

        #pragma warning disable 618
        /// <summary>
        /// Gets or sets custom options on this replication
        /// </summary>
        [Obsolete("Replaced by ReplicationOptions")]
        public ReplicationOptionsDictionary Options { get; set; }
        #pragma warning restore 618

        /// <summary>
        /// Gets or sets the replication options.
        /// </summary>
        public ReplicationOptions ReplicationOptions { get; set; } = new ReplicationOptions();
       
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
                if (value == null) {
                    value = "0";
                }

                if (!value.Equals(_lastSequence)) {
                    Log.To.Sync.V(Tag, "{0} setting LastSequence to {1} (from {2})", this, value, _lastSequence);
                    _lastSequence = value;

                    if (!lastSequenceChanged) {
                        lastSequenceChanged = true;
                        Task.Delay(SaveLastSequenceDelay).ContinueWith(t => SaveLastSequence(null));
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
            get { return _remoteSession.ClientFactory; }
            set {
                _remoteSession.SetupHttpClientFactory(value, LocalDatabase);
            }
        }

        /// <summary>
        /// Gets or sets the cancellation token source to cancel this replication's operation
        /// </summary>
        protected CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();

        /// <summary>
        /// Gets or sets the headers that should be used when making HTTP requests
        /// </summary>
        [Obsolete("Use Headers")]
        protected internal IDictionary<string, object> RequestHeaders
        {
            get {
                return _remoteSession.RequestHeaders;
            }
            set { 
                _remoteSession.RequestHeaders = value ?? new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// The container for storing cookies specific to this replication
        /// </summary>
        protected internal CookieStore CookieContainer
        {
            get { return _remoteSession.CookieStore; }
        }

        /// <summary>
        /// Gets the unique ID for this replication
        /// </summary>
        protected Guid ReplicatorID
        {
            get { return _replicatorID; }
        }

        internal RemoteServerVersion ServerType 
        {
            get {
                return _remoteSession?.ServerType;
            } set {
                if (_remoteSession != null) {
                    _remoteSession.ServerType = value;
                }
            }
        }

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
            sessionID = $"repl{ Interlocked.Increment(ref _lastSessionID):000}";
            var opts = new RemoteSessionContructorOptions {
                BaseUrl = remote,
                WorkExecutor = workExecutor,
                Id = _replicatorID,
                CancellationTokenSource = CancellationTokenSource
            };
            _remoteSession = new RemoteSession(opts);
            Username = remote.UserInfo;

            LocalDatabase = db;
            _eventContext = LocalDatabase.Manager.CapturedContext;
            Continuous = continuous;
            // NOTE: Consider running a separate scheduler for all http requests.
            WorkExecutor = workExecutor;
            RemoteUrl = remote;
#pragma warning disable 618
            Options = new ReplicationOptionsDictionary();
#pragma warning restore 618
            ReplicationOptions = new ReplicationOptions();

            if (RemoteUrl.Query != null && !StringEx.IsNullOrWhiteSpace(RemoteUrl.Query)) {
                Authenticator = AuthenticatorFactory.CreateFromUri(remote);

                // we need to remove the query from the URL, since it will cause problems when
                // communicating with sync gw / couchdb
                try {
                    RemoteUrl = new UriBuilder(remote.Scheme, remote.Host, remote.Port, remote.AbsolutePath).Uri;
                } catch (UriFormatException e) {
                    throw Misc.CreateExceptionAndLog(Log.To.Sync, e, Tag,
                        "Invalid URI format for remote endpoint");
                }
            }

            Batcher = new Batcher<RevisionInternal>(workExecutor, INBOX_CAPACITY, ProcessorDelay, inbox =>
            {
                try {
                    Log.To.Sync.V(Tag, "*** {0} BEGIN ProcessInbox ({1} sequences)", this, inbox.Count);
                    if(Continuous) {
                        FireTrigger(ReplicationTrigger.Resume);
                    }

                    ProcessInbox (new RevisionList(inbox));

                    Log.To.Sync.V(Tag, "*** {0} END ProcessInbox (lastSequence={1})", this, LastSequence);
                } catch(Exception e) {
                    throw Misc.CreateExceptionAndLog(Log.To.Sync, e, Tag, 
                        "{0} ProcessInbox failed", this);
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
            Log.To.Sync.V(Tag, "Start() called, firing Start...");
            FireTrigger(ReplicationTrigger.Start);
        }

        /// <summary>
        /// Stops the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public virtual void Stop()
        {
            Log.To.Sync.V(Tag, "Stop() called, firing StopGraceful...");
            FireTrigger(ReplicationTrigger.StopGraceful);
        }

        /// <summary>
        /// Gets a collection of document IDs that have been scheduled for replication
        /// but not yet completed.
        /// </summary>
        /// <returns>The pending document IDs.</returns>
        public ICollection<string> GetPendingDocumentIDs()
        {
            if(IsPull) {
                return null;
            }

            if(_pendingDocumentIDs != null) {
                if(_pendingDocumentIDsSequence == LocalDatabase.GetLastSequenceNumber()) {
                    return _pendingDocumentIDs; // Still valid
                }

                _pendingDocumentIDs = null;
            }

            var lastSequence = GetLastSequencePushed();
            if(lastSequence < 0) {
                return null;
            }

            var newPendingDocIDsSequence = LocalDatabase.GetLastSequenceNumber();
            var revs = LocalDatabase.UnpushedRevisionsSince(lastSequence.ToString(), LocalDatabase.GetFilter(Filter), FilterParams);

            _pendingDocumentIDsSequence = newPendingDocIDsSequence;
            _pendingDocumentIDs = new HashSet<string>(revs.Select(x => x.DocID));
            return _pendingDocumentIDs;
        }

        /// <summary>
        /// Checks if the specified document is pending replication
        /// </summary>
        /// <returns><c>true</c> if this document is pending, otherwise, <c>false</c>.</returns>
        /// <param name="doc">The document to check.</param>
        public bool IsDocumentPending(Document doc)
        {
            var lastSeq = GetLastSequencePushed();
            if(lastSeq < 0) {
                return false; // Error
            }

            var rev = doc.CurrentRevision;
            var seq = rev.Sequence;
            if(seq <= lastSeq) {
                return false;
            }

            if(Filter != null) {
                // Use _pendingDocumentIDs as a shortcut, if it's valid
                if(_pendingDocumentIDs != null && _pendingDocumentIDsSequence == LocalDatabase.GetLastSequenceNumber()) {
                    return _pendingDocumentIDs.Contains(doc.Id);
                }

                // Else run the filter on the doc
                var filter = LocalDatabase.GetFilter(Filter);
                if(filter != null && !filter(rev, FilterParams)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Restarts the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public void Restart()
        {
            if(_restartEnabled) {
                Log.To.Sync.I(Tag, "Restart already scheduled, returning early!");
                return;
            }

            _restartEnabled = true;
            Changed += WaitForStopped;
            if(Status == ReplicationStatus.Stopped) {
                Log.To.Sync.W(Tag, "Restart() called on already stopped replication, " +
                "simply calling Start()");
                Changed -= WaitForStopped;
                Start();
                return;
            } else if(_stateMachine.IsInState(ReplicationState.Stopping)) {
                Log.To.Sync.I(Tag, "Replication already stopping, scheduling an immediate restart");
                return; // Already stopping
            }

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
            if(expirationDate.Kind == DateTimeKind.Unspecified) {
                Log.To.Sync.W(Tag, "Cookie being constructed with DateTimeKind.Unspecified for expiration date.  " +
                    "This has been known to cause issues with expiration dates being incorrect.");
            }

            var cookie = new Cookie(name, value)
            {
                Expires = expirationDate,
                Secure = secure,
                HttpOnly = httpOnly,
                Domain = RemoteUrl.Host
            };

            cookie.Path = !string.IsNullOrEmpty(path) 
                ? path 
                : RemoteUrl.PathAndQuery;
            
            _remoteSession.CookieStore.Add(cookie);
        }

        /// <summary>
        /// Deletes a cookie specified by name
        /// </summary>
        /// <param name="name">The name of the cookie</param>
        public void DeleteCookie(String name)
        {
            _remoteSession.CookieStore.Delete(RemoteUrl, name);
        }

        /// <summary>
        /// Deletes any persistent credentials (passwords, auth tokens...) associated with this 
        /// replication's Authenticator. Also removes session cookies from the cookie store. 
        /// </summary>
        /// <returns><c>true</c> on success, <c>false</c> otherwise</returns>
        public bool ClearAuthenticationStores()
        {
            if(Authenticator != null && !((IAuthorizer)Authenticator).RemoveStoredCredentials()) {
                return false;
            }

            DeleteAllCookies();
            return true;
        }

        #endregion

        #region Protected Methods

        internal void DeleteAllCookies()
        {
            _remoteSession.CookieStore.Delete(RemoteUrl);
        }

        internal long GetLastSequencePushed()
        {
            if(IsPull) {
                return -1L;
            }

            if(_lastSequencePushed <= 0L) {
                // If running replicator hasn't updated yet, fetch the checkpointed last sequence:
                var lastSequence = LocalDatabase.LastSequenceWithCheckpointId(RemoteCheckpointDocID(LocalDatabase.PrivateUUID()));
                _lastSequencePushed = lastSequence == null ? 0L : Int64.Parse(lastSequence);
            }

            return _lastSequencePushed;
        }

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
                Log.To.Sync.E(Tag, String.Format("Exception getting status from {0}, continuing...", item), e);
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
            Login();
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
                Log.To.Sync.I(Tag, "RETRYING, to transfer missed revisions...");
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
            Log.To.Sync.V(Tag, "Preparing to fire {0}", trigger);
            WorkExecutor.StartNew(() =>
            {
                try {
                    _stateMachine.Fire(trigger);
                } catch(Exception e) {
                    Log.To.Sync.E(Tag, "State machine error", e);
                    throw;
                }
            });
        }

        /// <summary>
        /// Cancels the next scheduled retry attempt
        /// </summary>
        protected virtual void CancelPendingRetryIfReady()
        {
            var source = _retryIfReadyTokenSource;
            if (source != null) {
                source.Cancel();
            }
        }

        /// <summary>
        /// Schedules a call to retry if ready, using RetryDelay
        /// </summary>
        protected virtual void ScheduleRetryIfReady()
        {
            CancelPendingRetryIfReady();
            var source = Interlocked.Exchange(ref _retryIfReadyTokenSource, CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenSource.Token));
            if (source != null) {
                source.Dispose();
            }

            var token = _retryIfReadyTokenSource.Token;
            Task.Delay(ReplicationOptions.ReplicationRetryDelay).ContinueWith(task =>
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
            Log.To.Sync.I(Tag, "Attempting to start {0} ({1})", IsPull ? "puller" : "pusher", _replicatorID);
            _remoteSession = RemoteSession.Clone (_remoteSession, CancellationTokenSource);
            _remoteSession.Setup (ReplicationOptions);

            if (!LocalDatabase.IsOpen) {
                Log.To.Sync.W(Tag, "Not starting because local database is not open.");
                FireTrigger(ReplicationTrigger.StopImmediate);
                return;
            }

            var authorizer = Authenticator as IAuthorizer;
            if (authorizer != null) {
                authorizer.RemoteUrl = RemoteUrl;
                authorizer.LocalUUID = LocalDatabase.PublicUUID ();
            }

            var reachabilityManager = LocalDatabase.Manager.NetworkReachabilityManager;
            reachabilityManager.StatusChanged += NetworkStatusChanged;

            if (!LocalDatabase.Manager.NetworkReachabilityManager.CanReach(RemoteUrl.AbsoluteUri, ReplicationOptions.RequestTimeout)) {
                Log.To.Sync.I(Tag, "Remote endpoint is not reachable, going offline...");
                LastError = LocalDatabase.Manager.NetworkReachabilityManager.LastError;
                FireTrigger(ReplicationTrigger.GoOffline);
                CheckOnlineLoop();
            }

            LocalDatabase.AddReplication(this);
            if(!LocalDatabase.AddActiveReplication(this)) {
#if DEBUG
                var activeReplicators = default(IList<Replication>);
                if(!LocalDatabase.ActiveReplicators.AcquireTemp(out activeReplicators)) {
                    Log.To.Sync.E(Tag, "Active replication list unavailable!");
                } else {
                    var existing = activeReplicators.FirstOrDefault(x => x.RemoteCheckpointDocID() == RemoteCheckpointDocID());
                    if(existing != null) {
                        Log.To.Sync.W(Tag, "Not starting because identical {0} already exists ({1})", IsPull ? "puller" : "pusher", existing._replicatorID);
                    } else {
                        Log.To.Sync.E(Tag, "Not starting {0} for unknown reasons", IsPull ? "puller" : "pusher");
                    }
                }
#else
                Log.To.Sync.W(Tag, "Not starting becuse identical {0} already exists", IsPull ? "puller" : "pusher");
#endif
                FireTrigger(ReplicationTrigger.StopImmediate);
                return;
            }

            _retryIfReadyTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenSource.Token);
            if (ReplicationOptions.Reset) {
                LocalDatabase.SetLastSequence(null, RemoteCheckpointDocID());
            }

            _startTime = DateTime.UtcNow;
            SetupRevisionBodyTransformationFunction();

            Log.To.Sync.I(Tag, "Beginning replication process...");
            LastSequence = null;
            Login();
        }

        /// <summary>
        /// Performs login logic for the remote endpoint
        /// </summary>
        protected virtual void Login()
        {
            if(Authenticator != null) {
                var login = new RemoteLogin(RemoteUrl, LocalDatabase.PublicUUID(), _remoteSession);
                login.AttemptLogin().ContinueWith(t =>
                {
                    if(t.Exception != null) {
                        LastError = Misc.Flatten(t.Exception)?.FirstOrDefault();
                        FireTrigger(ReplicationTrigger.StopImmediate);
                    } else {
                        Log.To.Sync.I(Tag, "{0} successfully logged in!", this);
                        FetchRemoteCheckpointDoc();
                    }
                });
            } else {
                FetchRemoteCheckpointDoc();
            }
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
            Log.To.Sync.I(Tag, "{0} going offline", this);
            CheckOnlineLoop();
        }

        /// <summary>
        /// Takes the action necessary to transition the replicator
        /// into an online state
        /// </summary>
        protected virtual void PerformGoOnline()
        {
            Log.To.Sync.I(Tag, "{0} going online", this);
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
            if (value == 0) {
                return;
            }

            var newCount = Interlocked.Add(ref _completedChangesCount, value);
            Log.To.Sync.I(Tag, "{0} progress {1} / {2}", this, newCount, _changesCount);
            NotifyChangeListeners();
            if (newCount == _changesCount && IsSafeToStop) {
                if(Continuous) {
                    FireTrigger(ReplicationTrigger.WaitingForChanges);
                } else {
                    Log.To.Sync.V(Tag, "Non-continuous replication caught up, firing StopGraceful...");
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
            if (value == 0) {
                return;
            }

            var newCount = Interlocked.Add(ref _changesCount, value);
            Log.To.Sync.I(Tag, "{0} progress {1} / {2}", this, _completedChangesCount, newCount);
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
            Log.To.Sync.I(Tag, "{0}: Stop Graceful...", _replicatorID);

            if (Batcher != null)  {
                Batcher.Clear();
            }

            var master = CancellationTokenSource;
            CancellationTokenSource = new CancellationTokenSource();
            master.Cancel();
            master.Dispose();
            FireTrigger(ReplicationTrigger.StopImmediate);
        }

        #endregion

        #region Internal Methods

        internal abstract void BeginReplicating();

        internal abstract void ProcessInbox(RevisionList inbox);

        // TODO: Evaluate usefulness
        internal bool HasSameSettingsAs(Replication other)
        {
            return LocalDatabase == other.LocalDatabase &&
            IsPull == other.IsPull &&
            RemoteCheckpointDocID("").Equals(other.RemoteCheckpointDocID(""));
        }

        internal virtual void Stopping()
        {
            Log.To.Sync.I(Tag, "{0} Stopping", this);
            var remoteSession = _remoteSession;
            if(!LocalDatabase.IsOpen || remoteSession.Disposed) {
                // This logic has already been handled by DatabaseClosing(), or
                // this replication never started in the first place (client still null)
                return;
            }

            LocalDatabase.ForgetReplication(this);
            lastSequenceChanged = true; // force save the sequence
            SaveLastSequence(() =>
           {
               var reachabilityManager = LocalDatabase.Manager.NetworkReachabilityManager;
               if(reachabilityManager != null) {
                   reachabilityManager.StatusChanged -= NetworkStatusChanged;
               }

               remoteSession.Dispose();
               Log.To.Sync.I(Tag, "{0} stopped.  Elapsed time {1} sec", this, (DateTime.UtcNow - _startTime).TotalSeconds.ToString("F3"));
           });
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
            spec["localUUID"] = localUUID;
            spec["push"] = !IsPull;
            spec["continuous"] = Continuous;

            if (Filter != null) {
                spec["filter"] = Filter;
            }
            if (filterParamsCanonical != null) {
                spec["filterParams"] = filterParamsCanonical;
            }
            if (docIdsSorted != null) {
                spec["docids"] = docIdsSorted;
            }

            string remoteUUID;
            if (ReplicationOptions.RemoteUUID != null) {
                spec["remoteURL"] = ReplicationOptions.RemoteUUID;
            } else {
#pragma warning disable 618
                var hasValue = Options.TryGetValue<string>(ReplicationOptionsDictionary.REMOTE_UUID_KEY, out remoteUUID);
#pragma warning restore 618
                if (hasValue) {
                    Log.To.Sync.W(Tag, "ReplicationOptionsDictionary support is deprecated, switch to ReplicationOptions");
                    spec["remoteURL"] = remoteUUID;
                } else {
                    spec["remoteURL"] = ReplicationOptions.RemoteUUID ?? RemoteUrl.AbsoluteUri;
                }
            }

            IEnumerable<byte> inputBytes = null;
            try {
                inputBytes = Manager.GetObjectMapper().WriteValueAsBytes(spec);
            } catch(CouchbaseLiteException) {
                Log.To.Sync.E(Tag, "Failed to serialize remote checkpoint doc ID, rethrowing...");
                throw;
            } catch (Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Sync, e, Tag, "Error serializing remote checkpoint doc ID");
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
                    var generation = rev.Generation;
                    var xformed = RevisionBodyTransformationFunction(rev);
                    if (xformed == null) {
                        return null;
                    }

                    if (xformed != rev) {
                        Debug.Assert((xformed.DocID.Equals(rev.DocID)));
                        Debug.Assert((xformed.RevID.Equals(rev.RevID)));
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
                                    throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.InternalServerError, Tag,
                                        "Transformer added attachment without adding data");
                                }

                                var newInfo = new Dictionary<string, object>(info);
                                newInfo["revpos"] = generation;
                                return newInfo;
                            });
                        }
                    }
                } catch (Exception e) {
                    Log.To.Sync.W(Tag, String.Format("Exception transforming a revision of doc '{0}', aborting...", 
                        new SecureLogString(rev.DocID, LogMessageSensitivity.PotentiallyInsecure)), e);
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

        internal void DatabaseClosing(CountdownEvent evt)
        {
            Log.To.Sync.I(Tag, "Database closed while replication running, shutting down");
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

        internal bool AddToInbox(RevisionInternal rev)
        {
            Debug.Assert(IsRunning);
            return Batcher.QueueObject(rev);
        }

        #endregion

        #region Private Methods

        private void WaitForStopped (object sender, ReplicationChangeEventArgs e)
        {
            if (e.Status != ReplicationStatus.Stopped) {
                return;
            }
                
            Changed -= WaitForStopped;
            _restartEnabled = false;
            Start();
        }

        private void FetchRemoteCheckpointDoc()
        {
            lastSequenceChanged = false;
            var checkpointId = RemoteCheckpointDocID();
            var localLastSequence = LocalDatabase.LastSequenceWithCheckpointId(checkpointId);

            if (localLastSequence == null && GetLastSequenceFromLocalCheckpoint() == null) {
                Log.To.Sync.I(Tag, "{0} no local checkpoint, not getting remote one", this);
                MaybeCreateRemoteDB();
                BeginReplicating();
                return;
            }

            Log.To.SyncPerf.I(Tag, "{0} getting remote checkpoint", this);
            _remoteSession.SendAsyncRequest(HttpMethod.Get, "/_local/" + checkpointId, null, (response, e) => 
            {
                try {
                    Log.To.SyncPerf.I(Tag, "{0} got remote checkpoint", this);
                    if (e != null && !Is404 (e)) {
                        Log.To.Sync.I(Tag, "{0} error getting remote checkpoint", this);
                        LastError = e;
                        Log.To.Sync.V(Tag, "Couldn't get remote checkpoint, so changing state...");
                        if(Continuous) {
                            FireTrigger(ReplicationTrigger.WaitingForChanges);
                            ScheduleRetryIfReady();
                        } else {
                            FireTrigger(ReplicationTrigger.StopGraceful);
                        }
                    } else {
                        if (e != null && Is404 (e)) {
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

                            Log.To.Sync.I(Tag, "{0} replicating from lastSequence={1}", this, LastSequence);
                        } else {
                            Log.To.Sync.I(Tag, "{0} lastSequence mismatch: I had {1}, remote had {2} (response = {3}, " +
                                "resetting lastSequence to 0",
                                this, localLastSequence, remoteLastSequence, response);
                            _lastSequence = "0";
                        }

                        BeginReplicating ();
                    }
                } catch (Exception ex) {
                    Log.To.Sync.E(Tag, String.Format("{0} error analyzing _local response", this), ex);
                }
            });
        }

        private void CheckOnlineLoop()
        {
            // Check at intervals to see if connection has been restored (in case
            // the offline status is the result of the *server* being offline)
            Task.Delay(ReplicationOptions.ReplicationRetryDelay).ContinueWith(t =>
            {
                if(_stateMachine.State != ReplicationState.Offline) {
                    return;
                }

                FireTrigger(ReplicationTrigger.GoOnline);
                CheckOnlineLoop();
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
            var lastSequence = LastSequence;
            Log.To.Sync.I(Tag, "{0} checkpointing sequence={1}", this, lastSequence);
            var body = new Dictionary<String, Object>();
            if (_remoteCheckpoint != null) {
                foreach (var pair in _remoteCheckpoint) {
                    body[pair.Key] = pair.Value;
                }
            }

            body["lastSequence"] = lastSequence;
            var remoteCheckpointDocID = RemoteCheckpointDocID();
            if (String.IsNullOrEmpty(remoteCheckpointDocID)) {
                Log.To.Sync.W(Tag, "remoteCheckpointDocID is null for {0}, aborting SaveLastSequence", this);
                if (completionHandler != null) { completionHandler(); }
                return;
            }

            _savingCheckpoint = true;
            var message = _remoteSession.SendAsyncRequest(HttpMethod.Put, "/_local/" + remoteCheckpointDocID, body, (result, e) => 
            {
                _savingCheckpoint = false;
                if (e != null) {
                    switch (GetStatusFromError(e)) {
                        case StatusCode.NotFound:
                            Log.To.Sync.I(Tag, "Got 404 from _local, ignoring...");
                            _remoteCheckpoint = null;
                            break;
                        case StatusCode.Conflict:
                            Log.To.Sync.I(Tag, "Got 409 from _local, retrying...");
                            RefreshRemoteCheckpointDoc();
                            break;
                        default:
                            Log.To.Sync.W(Tag, String.Format("Unable to save remote checkpoint for {0}", _replicatorID), e);
                            // TODO: On 401 or 403, and this is a pull, remember that remote
                            // TODO: is read-only & don't attempt to read its checkpoint next time.
                            break;
                    }
                } else {
                    var response = result.AsDictionary<string, object>();
                    var rev = response.GetCast<string>("rev");
                    if(rev != null) {
                        body.SetRevID(rev);
                    }

                    _remoteCheckpoint = body;
                    var localDb = LocalDatabase;
                    if(localDb.Storage == null) {
                        Log.To.Sync.I(Tag, "Database is null or closed, ignoring remote checkpoint response");
                        if(completionHandler != null) {
                            completionHandler();
                        }
                        return;
                    }

                    localDb.SetLastSequence(LastSequence, remoteCheckpointDocID);
                    Log.To.Sync.I(Tag, "{0} saved remote checkpoint '{1}' (_rev={2})", this, lastSequence, rev);
                }

                if (completionHandler != null) {
                    completionHandler ();
                }
            }, true);

            // This request should not be canceled when the replication is told to stop:
            if(message != null) {
                Task dummy;
                _remoteSession?._requests.TryRemove(message, out dummy);
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

            var webException = e as WebException;
            if (webException != null)
            {
                return (StatusCode)(webException.Response as HttpWebResponse).StatusCode;
            }

            return StatusCode.Unknown;
        }

        private void RefreshRemoteCheckpointDoc()
        {
            Log.To.Sync.I(Tag, "{0} refreshing remote checkpoint to get its _rev...", this);
            _savingCheckpoint = true;

            _remoteSession.SendAsyncRequest(HttpMethod.Get, "/_local/" + RemoteCheckpointDocID(), null, (result, e) =>
            {
                if (!LocalDatabase.IsOpen) {
                    Log.To.Sync.I(Tag, "{0} DB closed while refreshing remote checkpoint.  Aborting.", this);
                    return;
                }

                _savingCheckpoint = false;

                if (e != null && GetStatusFromError(e) != StatusCode.NotFound) {
                    Log.To.Sync.I(Tag, String.Format("{0} error refreshing remote checkpoint", this), e);
                } else {
                    Log.To.Sync.I(Tag, "{0} refreshed remote checkpoint: {1}", this, new LogJsonString(result));
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
                        Debug.Assert (xformedProperties.CblID().Equals(properties.CblID()));
                        Debug.Assert (xformedProperties.CblRev().Equals(properties.CblRev()));

                        var nuRev = new RevisionInternal (rev.GetProperties ());
                        nuRev.SetProperties (xformedProperties);
                        return nuRev;
                    }
                    return rev;
                };
            }
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
                () => LocalDatabase.Manager.NetworkReachabilityManager.CanReach(RemoteUrl.AbsoluteUri, ReplicationOptions.RequestTimeout));
            
            _stateMachine.Configure(ReplicationState.Stopping).Permit(ReplicationTrigger.StopImmediate, ReplicationState.Stopped);
            _stateMachine.Configure(ReplicationState.Stopped).Permit(ReplicationTrigger.Start, ReplicationState.Running);

            // ignored transitions
            _stateMachine.Configure(ReplicationState.Running).Ignore(ReplicationTrigger.Start);
            _stateMachine.Configure(ReplicationState.Stopping).Ignore(ReplicationTrigger.Start);
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
                Log.To.Sync.V(Tag, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                StartInternal();
                NotifyChangeListenersStateTransition(transition);
            });

            _stateMachine.Configure(ReplicationState.Running).OnExit(transition =>
               Log.To.Sync.V(Tag, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID));

            _stateMachine.Configure(ReplicationState.Idle).OnEntry(transition =>
            {
                Log.To.Sync.V(Tag, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                if(transition.Source == transition.Destination) {
                    return;
                }

                if(_revisionsFailed > 0) {
                    ScheduleRetryIfReady();
                }

                SaveLastSequence(null);

                NotifyChangeListenersStateTransition(transition);
            });

            _stateMachine.Configure(ReplicationState.Offline).OnEntry(transition =>
            {
                Log.To.Sync.V(Tag, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                PerformGoOffline();
                NotifyChangeListenersStateTransition(transition);
            });

            _stateMachine.Configure(ReplicationState.Offline).OnExit(transition =>
            {
                Log.To.Sync.V(Tag, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                PerformGoOnline();
                NotifyChangeListenersStateTransition(transition);
            });

            _stateMachine.Configure(ReplicationState.Stopping).OnEntry(transition =>
            {
                Log.To.Sync.V(Tag, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                if(transition.Source == transition.Destination) {
                    Log.To.Sync.W(Tag, "Concurrency issue with ReplicationState.Stopping, ignoring state change...");
                    return;
                }

                NotifyChangeListenersStateTransition(transition);
                StopGraceful();
            });

            _stateMachine.Configure(ReplicationState.Stopped).OnEntry(transition =>
            {
                Log.To.Sync.V(Tag, "{0} => {1} ({2})", transition.Source, transition.Destination, _replicatorID);
                Stopping();

                if(transition.Source == transition.Destination) {
                    Log.To.Sync.W(Tag, "Concurrency issue with ReplicationState.Stopped, ignoring state change...");
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
            Log.To.Sync.I(Tag, "NotifyChangeListeners ({0}/{1}, state={2} (batch={3}, net={4}))",
                CompletedChangesCount, ChangesCount,
                _stateMachine.State, Batcher == null ? 0 : Batcher.Count(), _remoteSession.RequestCount);

            var lastSequencePushed = (IsPull || LastSequence == null) ? -1L : Int64.Parse(LastSequence);
            if(_lastSequencePushed != lastSequencePushed) {
                _lastSequencePushed = lastSequencePushed;
                _pendingDocumentIDs = null;
            }

            Username = (Authenticator as IAuthorizer)?.Username;
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

            Log.To.TaskScheduling.V(Tag, "Scheduling Changed callback...");
            if (_eventContext != null) {
                _eventContext.StartNew(() =>
                {
                    lock (_eventQueue) { 
                        if(_eventQueue.Count > 0) {
                            Log.To.TaskScheduling.V(Tag, "Firing {0} queued callback(s)", _eventQueue.Count);
                        } else {
                            Log.To.TaskScheduling.V(Tag, "No callback scheduled, not firing");
                        }

                        while (_eventQueue.Count > 0) {
                            try {
                                evt (this, _eventQueue.Dequeue ());
                            } catch (Exception e) {
                                Log.To.Sync.E (Tag, "Exception in Changed callback, " +
                                               "this will cause instability unless corrected!", e);
                            }
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
        private static readonly string Tag = typeof(ReplicationChangeEventArgs).Name;

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Replication"/> that raised the event.  Do not
        /// rely on this variable for the current state of the replicator as it may have changed
        /// between the time the args were created and the time that the event was raised.
        /// Instead use the various other properties.
        /// </summary>
        public Replication Source { get; }

        /// <summary>
        /// Gets the number of changes scheduled for the replication at the
        /// time the event was created.
        /// </summary>
        public int ChangesCount { get; }

        /// <summary>
        /// Gets the number of changes completed by the replication at the
        /// time the event was created.
        /// </summary>
        public int CompletedChangesCount { get; }

        /// <summary>
        /// Gets the status of the replication at the time the event was created
        /// </summary>
        public ReplicationStatus Status { get; }

        /// <summary>
        /// Gets the transition
        /// </summary>
        /// <value>The replication state transition.</value>
        public ReplicationStateTransition ReplicationStateTransition { get; }

        /// <summary>
        /// Gets the most recent error that occured at the time of this change
        /// </summary>
        /// <value>The last error.</value>
        public Exception LastError { get; }

        /// <summary>
        /// The current username assigned to the replication
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Couchbase.Lite.ReplicationChangeEventArgs"/> class.
        /// </summary>
        /// <param name="sender">The <see cref="Couchbase.Lite.Replication"/> that raised the event.</param>
        /// <param name="transition">The transition that caused the state in the replication, if applicable</param>
        public ReplicationChangeEventArgs (Replication sender, ReplicationStateTransition transition)
        {
            if (sender == null) {
                Log.To.Sync.E(Tag, "sender null in ctor, throwing...");
                throw new ArgumentNullException("sender");
            }

            Source = sender;
            ReplicationStateTransition = transition;
            ChangesCount = sender.ChangesCount;
            CompletedChangesCount = sender.CompletedChangesCount;
            Status = sender.Status;
            LastError = sender.LastError;
            Username = sender.Username;

            if (Status == ReplicationStatus.Offline && transition != null && transition.Destination == ReplicationState.Running) {
                Status = ReplicationStatus.Active;
            }

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
