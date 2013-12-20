using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite {

    public partial class Replication {

    #region Enums
    
    public enum ReplicationStatus {
        Stopped,
        Offline,
        Idle,
        Active
    }
                        
    #endregion

    #region Constructors

        public Replication(Database db, Uri remote, bool continuous, ScheduledExecutorService workExecutor) 
            : this(db, remote, continuous, null, workExecutor) { }

        /// <summary>Private Constructor</summary>
        public Replication(Database db, Uri remote, bool continuous, HttpClientFactory clientFactory, ScheduledExecutorService workExecutor)
        {
//            LocalDatabase = db;
//            Continuous = continuous;
//            this.workExecutor = workExecutor;
//            RemoteUrl = remote;
//            RemoteRequestExecutor = Executors.NewCachedThreadPool();
//
//            if (remote.GetQuery() != null && !remote.GetQuery().IsEmpty())
//            {
//                URI uri = URI.Create(remote.ToExternalForm());
//                string personaAssertion = URIUtils.GetQueryParameter(uri, PersonaAuthorizer.QueryParameter
//                );
//                if (personaAssertion != null && !personaAssertion.IsEmpty())
//                {
//                    string email = PersonaAuthorizer.RegisterAssertion(personaAssertion);
//                    PersonaAuthorizer authorizer = new PersonaAuthorizer(email);
//                    SetAuthorizer(authorizer);
//                }
//                string facebookAccessToken = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QueryParameter
//                );
//                if (facebookAccessToken != null && !facebookAccessToken.IsEmpty())
//                {
//                    string email = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QueryParameterEmail
//                    );
//                    FacebookAuthorizer authorizer = new FacebookAuthorizer(email);
//                    Uri remoteWithQueryRemoved = null;
//                    try
//                    {
//                        remoteWithQueryRemoved = new Uri(remote.Scheme, remote.GetHost(), remote.Port, remote
//                            .AbsolutePath);
//                    }
//                    catch (UriFormatException e)
//                    {
//                        throw new ArgumentException(e);
//                    }
//                    FacebookAuthorizer.RegisterAccessToken(facebookAccessToken, email, remoteWithQueryRemoved
//                        .ToExternalForm());
//                    SetAuthorizer(authorizer);
//                }
//                // we need to remove the query from the URL, since it will cause problems when
//                // communicating with sync gw / couchdb
//                try
//                {
//                    this.remote = new Uri(remote.Scheme, remote.GetHost(), remote.Port, remote.AbsolutePath
//                    );
//                }
//                catch (UriFormatException e)
//                {
//                    throw new ArgumentException(e);
//                }
//            }
//            batcher = new Batcher<RevisionInternal>(workExecutor, InboxCapacity, ProcessorDelay
//                , new _BatchProcessor_137(this));
//            this.clientFactory = clientFactory != null ? clientFactory : CouchbaseLiteHttpClientFactory
//                .Instance;
        }

    #endregion

    #region Non-public Members

        protected internal String  lastSequence;
        protected internal Boolean lastSequenceChanged;
        protected internal Boolean savingCheckpoint;
        protected internal Boolean overdueForSave;
        protected internal IDictionary<String, Object> remoteCheckpoint;


        internal void DatabaseClosing()
        {
            SaveLastSequence();
            Stop();
            LocalDatabase = null;
        }

        internal void SaveLastSequence()
        {
            throw new NotImplementedException();
//            if (!lastSequenceChanged)
//            {
//                return;
//            }
//            if (savingCheckpoint)
//            {
//                // If a save is already in progress, don't do anything. (The completion block will trigger
//                // another save after the first one finishes.)
//                overdueForSave = true;
//                return;
//            }
//
//            lastSequenceChanged = false;
//            overdueForSave = false;
//
//            Log.V(Database.Tag, this + " checkpointing sequence=" + lastSequence);
//
//            var body = new Dictionary<String, Object>();
//            if (remoteCheckpoint != null)
//            {
//                body.PutAll(remoteCheckpoint);
//            }
//            body.Put("lastSequence", lastSequence);
//            var remoteCheckpointDocID = RemoteCheckpointDocID();
//            if (String.IsNullOrEmpty(remoteCheckpointDocID))
//            {
//                return;
//            }
//            savingCheckpoint = true;
//            SendAsyncRequest("PUT", "/_local/" + remoteCheckpointDocID, body, new _RemoteRequestCompletionBlock_717
//                (this, body));
//            // TODO: If error is 401 or 403, and this is a pull, remember that remote is read-only and don't attempt to read its checkpoint next time.
//            LocalDatabase.SetLastSequence(lastSequence, remote, !IsPull());
        }

//        internal void SendAsyncRequest(string method, string relativePath, object body, RemoteRequestCompletionBlock onCompletion)
//        {
//            try
//            {
//                string urlStr = BuildRelativeURLString(relativePath);
//                Uri url = new Uri(urlStr);
//                SendAsyncRequest(method, url, body, onCompletion);
//            }
//            catch (UriFormatException e)
//            {
//                Log.E(Database.Tag, "Malformed URL for async request", e);
//            }
//        }
//
//        internal String BuildRelativeURLString(String relativePath)
//        {
//            // the following code is a band-aid for a system problem in the codebase
//            // where it is appending "relative paths" that start with a slash, eg:
//            //     http://dotcom/db/ + /relpart == http://dotcom/db/relpart
//            // which is not compatible with the way the java url concatonation works.
//            string remoteUrlString = Remote.ToExternalForm();
//            if (remoteUrlString.EndsWith("/") && relativePath.StartsWith("/"))
//            {
//                remoteUrlString = Sharpen.Runtime.Substring(remoteUrlString, 0, remoteUrlString.Length
//                    - 1);
//            }
//            return remoteUrlString + relativePath;
//        }
//
//        public virtual void SendAsyncRequest(string method, Uri url, object body, RemoteRequestCompletionBlock
//            onCompletion)
//        {
//            RemoteRequest request = new RemoteRequest(workExecutor, clientFactory, method, url
//                , body, onCompletion);
//            remoteRequestExecutor.Execute(request);
//        }


        // Pusher overrides this to implement the .createTarget option
        /// <summary>This is the _local document ID stored on the remote server to keep track of state.
        ///     </summary>
        /// <remarks>
        /// This is the _local document ID stored on the remote server to keep track of state.
        /// Its ID is based on the local database ID (the private one, to make the result unguessable)
        /// and the remote database's URL.
        /// </remarks>
//        internal String RemoteCheckpointDocID()
//        {
//            if (LocalDatabase == null)
//            {
//                return null;
//            }
//            string input = LocalDatabase.PrivateUUID() + "\n" + remote.ToExternalForm() + "\n" + (!IsPull
//                () ? "1" : "0");
//            return Misc.TDHexSHA1Digest(Sharpen.Runtime.GetBytesForString(input));
//        }


    #endregion
    
    #region Instance Members
        
        public Database LocalDatabase { get; private set; }

        public Uri RemoteUrl { get { throw new NotImplementedException(); } }

        public Boolean IsPull { get { throw new NotImplementedException(); } }

        public Boolean CreateTarget { get; set; }

        public Boolean Continuous { get; set; }

        public String Filter { get; set; }

        public Dictionary<String, String> FilterParams { get; set; }

        public IEnumerable<String> Channels { get; set; }

        public IEnumerable<String> DocIds { get; set; }

        public Dictionary<String, String> Headers { get; set; }

        public ReplicationStatus Status { get { throw new NotImplementedException(); } }

        public Boolean IsRunning { get { throw new NotImplementedException(); } }

        public Exception LastError { get { throw new NotImplementedException(); } }

        public int CompletedChangesCount { get { throw new NotImplementedException(); } }

        public int ChangesCount { get { throw new NotImplementedException(); } }

        //Methods
        public void Start() { throw new NotImplementedException(); }

        public void Stop() { throw new NotImplementedException(); }

        public void Retart() { throw new NotImplementedException(); }

        public event EventHandler<ReplicationChangeEventArgs> Change;

    #endregion
    
    #region EventArgs Subclasses
        public class ReplicationChangeEventArgs : EventArgs {

            //Properties
            public Replication Source { get { throw new NotImplementedException(); } }

        }

    #endregion
    
    }

}

