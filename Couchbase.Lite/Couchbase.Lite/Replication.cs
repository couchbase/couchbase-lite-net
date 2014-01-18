using System;
using System.Collections.Generic;
using System.Net;
using Couchbase.Lite.Util;
using Sharpen;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Support;
using Couchbase.Lite.Internal;
using System.Web;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.IO;
using System.Linq;

namespace Couchbase.Lite {

    public abstract partial class Replication 
    {

    #region Constants

        internal static readonly String ReplicatorDatabaseName = "_replicator";

    #endregion

    #region Enums
    
    public enum ReplicationStatus {
        Stopped,
        Offline,
        Idle,
        Active
    }
                        
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
            Status = Replication.ReplicationStatus.Stopped;

            if (RemoteUrl.GetQuery() != null && !RemoteUrl.GetQuery().IsEmpty())
            {
                var uri = new Uri(remote.ToString());
                var personaAssertion = URIUtils.GetQueryParameter(uri, PersonaAuthorizer.QueryParameter);

                if (personaAssertion != null && !personaAssertion.IsEmpty())
                {
                    string email = PersonaAuthorizer.RegisterAssertion(personaAssertion);
                    PersonaAuthorizer authorizer = new PersonaAuthorizer(email);
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

                    active = false;
                }, CancellationTokenSource);
            this.clientFactory = clientFactory ?? CouchbaseLiteHttpClientFactory.Instance;
//            client = this.clientFactory.GetHttpClient();
        }

    #endregion

    #region Constants

        const int ProcessorDelay = 500;
        const int InboxCapacity = 100;
        const string Tag = "Replication";

    #endregion

    #region Non-public Members

        private static Int32 lastSessionID = 0;

        readonly protected TaskFactory WorkExecutor; // FIXME: Remove this.

        readonly protected IHttpClientFactory clientFactory;

        protected internal String  sessionID;
        protected internal String  LastSequence;
        protected internal Boolean lastSequenceChanged;
        protected internal Boolean savingCheckpoint;
        protected internal Boolean overdueForSave;
        protected internal IDictionary<String, Object> remoteCheckpoint;

        protected internal Boolean continuous;

        protected internal Int32 completedChangesCount;
        protected internal Int32 changesCount;
        protected internal Int32 asyncTaskCount;
        protected internal Boolean active;

        private  Authorizer Authorizer { get; set; }
        internal Batcher<RevisionInternal> Batcher { get; set; }
        private CancellationTokenSource CancellationTokenSource { get; set; }

        protected internal IDictionary<String, Object> RequestHeaders { get; set; }

        readonly object asyncTaskLocker = new object ();

        void NotifyChangeListeners ()
        {
            var evt = Changed;
            if (evt == null) return;

            var args = new ReplicationChangeEventArgs(this);
            evt(this, args);
        }

        internal void DatabaseClosing()
        {
            SaveLastSequence();
            Stop();
            LocalDatabase = null;
        }

        internal void AddToInbox(RevisionInternal rev)
        {
            active |= Batcher.Count () == 0;
            Batcher.QueueObject(rev);
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
            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Get, sessionPath, null, (result, e) => {
                if (e is HttpException && ((HttpException)e).GetHttpCode() == 404
                    && sessionPath.Equals("/_session", StringComparison.InvariantCultureIgnoreCase)) {
                    CheckSessionAtPath ("_session");
                    return;
                } else {
                    var response = (IDictionary<String, Object>)result;
                    var userCtx = (IDictionary<String, Object>)response.Get ("userCtx");
                    var username = (string)userCtx.Get ("name");

                    if (!string.IsNullOrEmpty (username)) {
                        Log.D (Database.Tag, string.Format ("{0} Active session, logged in as {1}", this, username));
                        FetchRemoteCheckpointDoc ();
                    } else {
                        Log.D (Database.Tag, string.Format ("{0} No active session, going to login", this));
                        Login ();
                    }
                }
                AsyncTaskFinished (1);
            });
        }

        protected internal virtual void Login()
        {
            var loginParameters = Authorizer.LoginParametersForSite(RemoteUrl);
            if (loginParameters == null)
            {
                Log.D(Database.Tag, String.Format("{0}: {1} has no login parameters, so skipping login", this, Authorizer));
                FetchRemoteCheckpointDoc();
                return;
            }

            var loginPath = Authorizer.LoginPathForSite(RemoteUrl);
            Log.D(Database.Tag, string.Format("{0}: Doing login with {1} at {2}", this, Authorizer.GetType(), loginPath));

            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Post, loginPath, loginParameters, (result, e) => {
                if (e != null) {
                    Log.D (Database.Tag, string.Format ("{0}: Login failed for path: {1}", this, loginPath));
                    LastError = e;
                } else {
                    Log.D (Database.Tag, string.Format ("{0}: Successfully logged in!", this));
                    FetchRemoteCheckpointDoc ();
                }
                AsyncTaskFinished (1);
            });
        }

        internal void FetchRemoteCheckpointDoc()
        {
            lastSequenceChanged = false;
            var localLastSequence = LocalDatabase.LastSequenceWithRemoteURL(RemoteUrl, !IsPull);
            if (localLastSequence == null)
            {
                MaybeCreateRemoteDB();
                BeginReplicating();
                return;
            }
            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Get, "/_local/" + RemoteCheckpointDocID(), null, (result, e) => {
                if (e != null && !Is404 (e)) {
                    Log.D (Database.Tag, this + " error getting remote checkpoint: " + e);
                    LastError = e;
                } else {
                    if (e != null && Is404 (e)) {
                        Log.D (Database.Tag, this + " 404 error getting remote checkpoint " + RemoteCheckpointDocID () + ", calling maybeCreateRemoteDB");
                        MaybeCreateRemoteDB ();
                    }

                    var response = (IDictionary<String, Object>)result;
                    remoteCheckpoint = response;
                    var remoteLastSequence = String.Empty;

                    if (response != null) {
                        remoteLastSequence = (string)response.Get ("lastSequence");
                    }
                    if (remoteLastSequence != null && remoteLastSequence.Equals (localLastSequence)) {
                        LastSequence = localLastSequence;
                        Log.V (Database.Tag, this + ": Replicating from lastSequence=" + LastSequence);
                    } else {
                        Log.V (Database.Tag, this + ": lastSequence mismatch: I had " + localLastSequence + ", remote had " + remoteLastSequence);
                    }
                    BeginReplicating ();
                }
                AsyncTaskFinished (1);
            });
        }

        private static bool Is404(Exception e)
        {
            return e is HttpException && ((HttpException)e).GetHttpCode () == 404;
        }

        internal abstract void BeginReplicating();

        /// <summary>CHECKPOINT STORAGE:</summary>
        internal virtual void MaybeCreateRemoteDB() { }

        // FIXME: No-op.
        internal virtual void ProcessInbox(RevisionList inbox) { }

        internal void AsyncTaskStarted()
        {
            lock (asyncTaskLocker)
            {
                ++asyncTaskCount;
            }
        }

        internal void AsyncTaskFinished(Int32 numTasks)
        {
            lock (asyncTaskLocker) {
                asyncTaskCount -= numTasks;
                if (asyncTaskCount == 0) {
                    if (!continuous) {
                        Stopped ();
                    }
                }
            }
        }

        internal virtual void Stopped()
        {
            Log.V(Database.Tag, ToString() + " STOPPED");

            IsRunning = false;
            completedChangesCount = changesCount = 0;

            SaveLastSequence();
            NotifyChangeListeners();

            Batcher.Close();
            Batcher = null;
            LocalDatabase = null;
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

            Log.V(Database.Tag, this + " checkpointing sequence=" + LastSequence);

            var body = new Dictionary<String, Object>();
            if (remoteCheckpoint != null)
            {
                body.PutAll(remoteCheckpoint);
            }
            body["lastSequence"] = LastSequence;
            var remoteCheckpointDocID = RemoteCheckpointDocID();
            if (String.IsNullOrEmpty(remoteCheckpointDocID))
            {
                return;
            }
            savingCheckpoint = true;
            SendAsyncRequest(HttpMethod.Put, "/_local/" + remoteCheckpointDocID, body, (result, e) => {
                savingCheckpoint = false;
                if (e != null) {
                    Log.V (Database.Tag, this + ": Unable to save remote checkpoint", e);
                } else {
                    var response = (IDictionary<String, Object>)result;
                    body.Put ("_rev", response.Get ("rev"));
                    remoteCheckpoint = body;
                }
                if (overdueForSave) {
                    SaveLastSequence ();
                }
            });
            // TODO: If error is 401 or 403, and this is a pull, remember that remote is read-only and don't attempt to read its checkpoint next time.
            LocalDatabase.SetLastSequence(LastSequence, RemoteUrl, !IsPull);
        }

        internal void SendAsyncRequest(HttpMethod method, string relativePath, object body, Action<Object, Exception> completionHandler)
        {
            try
            {
                string urlStr = BuildRelativeURLString(relativePath);
                Uri url = new Uri(urlStr);
                SendAsyncRequest(method, url.PathAndQuery, body, completionHandler);
            }
            catch (UriFormatException e)
            {
                Log.E(Database.Tag, "Malformed URL for async request", e);
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
            message.Headers.Add("Accept", "multipart/related, application/json");

            PreemptivelySetAuthCredentials(message);

            var client = clientFactory.GetHttpClient();
            client.CancelPendingRequests();
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
                        var fullBody = mapper.ReadValue<Object>(response.Result.Result);
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
                                        readTask.Wait();
                                        inputStream = readTask.Result;
                                        fullBody = Manager.GetObjectMapper().ReadValue<object>(inputStream);
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

        //Methods

        /// <summary>
        /// Starts the <see cref="Couchbase.Lite.Replication"/>.
        /// </summary>
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }
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

            Batcher.Flush();
            continuous = false;

            if (asyncTaskCount == 0)
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

