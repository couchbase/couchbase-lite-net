//
// ChangeTracker.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;
using Couchbase.Lite.Replicator;

namespace Couchbase.Lite.Internal
{
    internal enum ChangeTrackerMode
    {
        // Sends a request to the server, and immediately gets back a response
        // containing all the changes from the requested starting point, if any
        OneShot,

        // Sends a request to the server, which will remain open until there is
        // at least one change since the requested starting point
        LongPoll,
        Continuous, /* not used, here for reference */

        // Uses web socket messages to continuously deliver changes over one
        // long-lived stream
        WebSocket
    }

    // Create change trackers based on mode
    internal static class ChangeTrackerFactory
    {
        public static ChangeTracker Create(ChangeTrackerOptions options)
        {
            if (options.Mode == ChangeTrackerMode.WebSocket) {
                return new WebSocketChangeTracker(options);
            } else {
                return new SocketChangeTracker(options);
            }
        }
    }

    internal sealed class ChangeTrackerOptions : ConstructorOptions
    {
        [RequiredProperty]
        public Uri DatabaseUri { get; set; }

        public ChangeTrackerMode Mode { get; set; }

        public bool IncludeConflicts { get; set; }

        public object LastSequenceID { get; set; }

        [RequiredProperty]
        public IChangeTrackerClient Client { get; set; }

        [RequiredProperty]
        public IRetryStrategy RetryStrategy { get; set; }

        public TaskFactory WorkExecutor { get; set; }
    }

    // The base class for change tracker logic
    internal abstract class ChangeTracker
    {

        #region Constants

        private static readonly string Tag = typeof(ChangeTracker).Name;
        internal static readonly TimeSpan DefaultHeartbeat = TimeSpan.FromMinutes(5);
        private static readonly List<string> ChangeFeedModes = new List<string> {
            "normal", "longpoll", "continuous", "websocket"
        };

        #endregion

        #region Variables

        protected readonly bool _includeConflicts;
        protected bool _usePost;
        protected bool _caughtUp;
        protected TaskFactory _workExecutor;
        protected IChangeTrackerResponseLogic _responseLogic;

        internal readonly Uri DatabaseUrl;
        internal readonly ChangeTrackerBackoff Backoff;

        #endregion

        #region Properties

        public string Feed
        {
            get {
                return ChangeFeedModes[(int)Mode];
            }
        }

        public string DatabaseName 
        {
            get {
                return DatabaseUrl.Segments.LastOrDefault();
            }
        }

        public object LastSequenceId { get; protected set; }

        public virtual Uri ChangesFeedUrl
        {
            get {
                var sb = new StringBuilder(DatabaseUrl.AbsoluteUri);
                if (sb.Length == 0) {
                    return null;
                }

                if (sb[sb.Length - 1] != '/') {
                    sb.Append('/');
                }

                sb.Append(GetChangesFeedPath());
                return new Uri(sb.ToString());
            }
        }

        public bool Paused
        {
            get { return _paused; }
            set
            {
                if(value != _paused) {
                    _paused = value;
                    Log.To.ChangeTracker.I(Tag, "{0} {1}...", value ? "Pausing" : "Resuming", this);
                    if(value) {
                        _responseLogic.Pause();
                    } else {
                        _responseLogic.Resume();
                    }
                }
            }
        }
        private bool _paused;

        public bool ActiveOnly { get; set; }

        public IChangeTrackerClient Client { get; set; }

        public bool Continuous { get; set; }

        public TimeSpan PollInterval { get; set; }

        public Exception Error { get; set; }

        public IDictionary<string, string> RequestHeaders { get; private set; }

        public IAuthenticator Authenticator { get; set; }

        public ChangeTrackerMode Mode { get; set; }

        public string FilterName { get; set; }

        public IDictionary<string, object> FilterParameters { get; set; }

        public int Limit { get; set; }

        public TimeSpan Heartbeat { get; set; }

        public IList<string> DocIDs { get; set; }

        public RemoteServerVersion ServerType { get; set; }

        public bool IsRunning { get; protected set; }

        #endregion

        #region Constructors

        protected ChangeTracker(ChangeTrackerOptions options)
        {
            options.Validate();
            Backoff = new ChangeTrackerBackoff(options.RetryStrategy);
            DatabaseUrl = options.DatabaseUri;
            Client = options.Client;
            Mode = options.Mode;
            Heartbeat = DefaultHeartbeat;
            _includeConflicts = options.IncludeConflicts;
            LastSequenceId = options.LastSequenceID;
            _workExecutor = options.WorkExecutor ?? new TaskFactory(new SingleTaskThreadpoolScheduler());
            _usePost = true;
            RequestHeaders = new Dictionary<string, string>();
        }

        #endregion

        #region Public Methods

        public abstract bool Start();

        public abstract void Stop();

        #endregion

        #region Protected Methods

        protected abstract void Stopped();

 		protected void UpdateServerType(HttpResponseMessage response)
        {
            var server = response.Headers.Server;
            if (server != null && server.Any()) {
                var serverString = String.Join(" ", server.Select(pi => pi.Product).Where(pi => pi != null).ToStringArray());
                UpdateServerType(serverString);
            }
        }

        protected void UpdateServerType(string header)
        {
            ServerType = new RemoteServerVersion(header);
            Log.To.ChangeTracker.I(Tag, "{0} Server Version: {1}", this, ServerType);
        }

        protected bool ReceivedChange(IDictionary<string, object> change)
        {
            if (change == null) {
                return false;
            }

            var seq = change.Get("seq");
            if (seq == null) {
                return false;
            }

            //pass the change to the client on the thread that created this change tracker
            if (Client != null) {
                Log.To.ChangeTracker.V(Tag, "{0} posting change", this);
                Client.ChangeTrackerReceivedChange(change);
            }

            LastSequenceId = seq;
            return true;
        }

        #endregion

        #region Internal Methods

        internal string GetChangesFeedPath()
        {
            if (_usePost) {
                return "_changes";
            }

            var path = new StringBuilder();
            path.AppendFormat("_changes?feed={0}&heartbeat={1}", Feed, (long)Heartbeat.TotalSeconds);

            if (_includeConflicts) {
                path.Append("&style=all_docs");
            }
            var sequence = LastSequenceId;
            if (sequence != null) {
                // BigCouch is now using arrays as sequence IDs. These need to be sent back JSON-encoded.
                if (sequence is IList || sequence is IDictionary<string, object>) {
                    sequence = Manager.GetObjectMapper().WriteValueAsString(sequence);
                }

                path.AppendFormat("&since={0}", Uri.EscapeUriString(sequence.ToString()));
            } 

            if (ActiveOnly && !_caughtUp) {
                path.Append("&active_only=true");
            }

            if (Limit > 0) {
                path.AppendFormat("&limit={0}", Limit);
            }

            // Add filter or doc_ids:
            var filterName = FilterName;
            var filterParameters = FilterParameters;
            if (DocIDs != null) {
                filterName = "_doc_ids";
                filterParameters = new Dictionary<string, object> {
                    { "doc_ids", DocIDs }
                };
            }

            if (filterName != null) {
                path.AppendFormat("&filter={0}", Uri.EscapeUriString(filterName));
                foreach (var pair in filterParameters) {
                    var valueStr = pair.Value as string;
                    if (valueStr == null) {
                        // It's ambiguous whether non-string filter params are allowed.
                        // If we get one, encode it as JSON:
                        try {
                            valueStr = Manager.GetObjectMapper().WriteValueAsString(pair.Value);
                        } catch(Exception) {
                            Log.To.ChangeTracker.W(Tag, "Illegal filter parameter {0} = {1}",
                                new SecureLogString(pair.Key, LogMessageSensitivity.PotentiallyInsecure),
                                new SecureLogJsonString(pair.Value, LogMessageSensitivity.PotentiallyInsecure));
                            continue;
                        }
                    }

                    path.AppendFormat("&{0}={1}", Uri.EscapeUriString(pair.Key), Uri.EscapeUriString(valueStr));
                }
            }

            return path.ToString();
        }

        internal IDictionary<string, object> GetChangesFeedParams()
        {
            // The replicator always stores the last sequence as a string, but the server may treat it as
            // an integer. As a heuristic, convert it to a number if it looks like one:
            var since = LastSequenceId;
            long n;
            if (Int64.TryParse(since as string, out n)) {
                since = n;
            }

            var filterName = FilterName;
            var filterParams = FilterParameters;
            if (DocIDs != null) {
                filterName = "_doc_ids";
                filterParams = new Dictionary<string, object> {
                    { "doc_ids", DocIDs }
                };
            }

            var post = new NonNullDictionary<string, object> {
                { "feed", Feed },
                { "heartbeat", (long)Heartbeat.TotalMilliseconds },
                { "style", _includeConflicts ? (object)"all_docs" : null },
                { "active_only", (ActiveOnly && !_caughtUp) ? (object)true : null },
                { "since", since },
                { "limit", Limit > 0 ? (object)Limit : null },
                { "filter", filterName },
                { "accept_encoding", "gzip" }
            };

            if (filterName != null && filterParams != null) {
                foreach (var pair in filterParams) {
                    post.Add(pair);
                }
            }

            return post;
        }

        internal IEnumerable<byte> GetChangesFeedPostBody()
        {
            var post = GetChangesFeedParams();
            return Manager.GetObjectMapper().WriteValueAsBytes(post);
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return string.Format("{0}[{1}]", GetType().Name, DatabaseUrl.Segments.Last());
        }

        #endregion
    }
}

