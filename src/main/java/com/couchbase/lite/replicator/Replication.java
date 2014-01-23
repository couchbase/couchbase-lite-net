package com.couchbase.lite.replicator;

import com.couchbase.lite.Database;
import com.couchbase.lite.Manager;
import com.couchbase.lite.Misc;
import com.couchbase.lite.RevisionList;
import com.couchbase.lite.auth.Authorizer;
import com.couchbase.lite.auth.FacebookAuthorizer;
import com.couchbase.lite.auth.PersonaAuthorizer;
import com.couchbase.lite.internal.RevisionInternal;
import com.couchbase.lite.internal.InterfaceAudience;
import com.couchbase.lite.support.BatchProcessor;
import com.couchbase.lite.support.Batcher;
import com.couchbase.lite.support.CouchbaseLiteHttpClientFactory;
import com.couchbase.lite.support.RemoteMultipartDownloaderRequest;
import com.couchbase.lite.support.RemoteMultipartRequest;
import com.couchbase.lite.support.RemoteRequest;
import com.couchbase.lite.support.RemoteRequestCompletionBlock;
import com.couchbase.lite.support.HttpClientFactory;
import com.couchbase.lite.util.TextUtils;
import com.couchbase.lite.util.URIUtils;
import com.couchbase.lite.util.Log;

import org.apache.http.client.HttpResponseException;
import org.apache.http.entity.mime.MultipartEntity;

import java.net.MalformedURLException;
import java.net.URI;
import java.net.URL;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public abstract class Replication {

    private static int lastSessionID = 0;

    protected boolean continuous;
    protected String filterName;
    protected ScheduledExecutorService workExecutor;
    protected Database db;
    protected URL remote;
    protected String lastSequence;
    protected boolean lastSequenceChanged;
    protected Map<String, Object> remoteCheckpoint;
    protected boolean savingCheckpoint;
    protected boolean overdueForSave;
    protected boolean running;
    protected boolean active;
    protected Throwable error;
    protected String sessionID;
    protected Batcher<RevisionInternal> batcher;
    protected int asyncTaskCount;
    private int completedChangesCount;
    private int changesCount;
    protected boolean online;
    protected HttpClientFactory clientFactory;
    private List<ChangeListener> changeListeners;
    protected List<String> documentIDs;

    protected Map<String, Object> filterParams;
    protected ExecutorService remoteRequestExecutor;
    protected Authorizer authorizer;
    private ReplicationStatus status = ReplicationStatus.REPLICATION_STOPPED;
    protected Map<String, Object> requestHeaders;

    protected static final int PROCESSOR_DELAY = 500;
    protected static final int INBOX_CAPACITY = 100;

    /**
     * @exclude
     */
    public static final String BY_CHANNEL_FILTER_NAME = "sync_gateway/bychannel";

    /**
     * @exclude
     */
    public static final String CHANNELS_QUERY_PARAM = "channels";

    /**
     * @exclude
     */
    public static final String REPLICATOR_DATABASE_NAME = "_replicator";

    /**
     * Options for what metadata to include in document bodies
     */
    public enum ReplicationStatus {
        REPLICATION_STOPPED,  /**< The replication is finished or hit a fatal error. */
        REPLICATION_OFFLINE,  /**< The remote host is currently unreachable. */
        REPLICATION_IDLE,     /**< Continuous replication is caught up and waiting for more changes.*/
        REPLICATION_ACTIVE    /**< The replication is actively transferring data. */
    }


    /**
     * Private Constructor
     * @exclude
     */
    @InterfaceAudience.Private
    /* package */ Replication(Database db, URL remote, boolean continuous, ScheduledExecutorService workExecutor) {
        this(db, remote, continuous, null, workExecutor);
    }

    /**
     * Private Constructor
     * @exclude
     */
    @InterfaceAudience.Private
    /* package */ Replication(Database db, URL remote, boolean continuous, HttpClientFactory clientFactory, ScheduledExecutorService workExecutor) {

        this.db = db;
        this.continuous = continuous;
        this.workExecutor = workExecutor;
        this.remote = remote;
        this.remoteRequestExecutor = Executors.newCachedThreadPool();
        this.changeListeners = new ArrayList<ChangeListener>();
        this.online = true;
        this.requestHeaders = new HashMap<String, Object>();

        if (remote.getQuery() != null && !remote.getQuery().isEmpty()) {

            URI uri = URI.create(remote.toExternalForm());

            String personaAssertion = URIUtils.getQueryParameter(uri, PersonaAuthorizer.QUERY_PARAMETER);
            if (personaAssertion != null && !personaAssertion.isEmpty()) {
                String email = PersonaAuthorizer.registerAssertion(personaAssertion);
                PersonaAuthorizer authorizer = new PersonaAuthorizer(email);
                setAuthorizer(authorizer);
            }

            String facebookAccessToken = URIUtils.getQueryParameter(uri, FacebookAuthorizer.QUERY_PARAMETER);
            if (facebookAccessToken != null && !facebookAccessToken.isEmpty()) {
                String email = URIUtils.getQueryParameter(uri, FacebookAuthorizer.QUERY_PARAMETER_EMAIL);
                FacebookAuthorizer authorizer = new FacebookAuthorizer(email);
                URL remoteWithQueryRemoved = null;
                try {
                    remoteWithQueryRemoved = new URL(remote.getProtocol(), remote.getHost(), remote.getPort(), remote.getPath());
                } catch (MalformedURLException e) {
                    throw new IllegalArgumentException(e);
                }
                authorizer.registerAccessToken(facebookAccessToken, email, remoteWithQueryRemoved.toExternalForm());
                setAuthorizer(authorizer);
            }

            // we need to remove the query from the URL, since it will cause problems when
            // communicating with sync gw / couchdb
            try {
                this.remote = new URL(remote.getProtocol(), remote.getHost(), remote.getPort(), remote.getPath());
            } catch (MalformedURLException e) {
                throw new IllegalArgumentException(e);
            }

        }

        batcher = new Batcher<RevisionInternal>(workExecutor, INBOX_CAPACITY, PROCESSOR_DELAY, new BatchProcessor<RevisionInternal>() {
            @Override
            public void process(List<RevisionInternal> inbox) {
                Log.v(Database.TAG, "*** " + toString() + ": BEGIN processInbox (" + inbox.size() + " sequences)");
                processInbox(new RevisionList(inbox));
                Log.v(Database.TAG, "*** " + toString() + ": END processInbox (lastSequence=" + lastSequence);
                active = false;
            }
        });

        setClientFactory(clientFactory);
        // this.clientFactory = clientFactory != null ? clientFactory : CouchbaseLiteHttpClientFactory.INSTANCE;

    }

    /**
     * Set the HTTP client factory if one was passed in, or use the default
     * set in the manager if available.
     * @param clientFactory
     */
    @InterfaceAudience.Private
    protected void setClientFactory(HttpClientFactory clientFactory) {
        Manager manager = null;
        if (this.db != null) {
            manager = this.db.getManager();
        }
        HttpClientFactory managerClientFactory = null;
        if (manager != null) {
            managerClientFactory = manager.getDefaultHttpClientFactory();
        }
        if (clientFactory != null) {
            this.clientFactory = clientFactory;
        } else {
            if (managerClientFactory != null) {
                this.clientFactory = managerClientFactory;
            } else {
                this.clientFactory = CouchbaseLiteHttpClientFactory.INSTANCE;
            }
        }
    }

    /**
     * Get the local database which is the source or target of this replication
     */
    @InterfaceAudience.Public
    public Database getLocalDatabase() {
        return db;
    }

    /**
     * Get the remote URL which is the source or target of this replication
     */
    @InterfaceAudience.Public
    public URL getRemoteUrl() {
        return remote;
    }

    /**
     * Is this a pull replication?  (Eg, it pulls data from Sync Gateway -> Device running CBL?)
     */
    @InterfaceAudience.Public
    public abstract boolean isPull();


    /**
     * Should the target database be created if it doesn't already exist? (Defaults to NO).
     */
    @InterfaceAudience.Public
    public abstract boolean shouldCreateTarget();

    /**
     * Set whether the target database be created if it doesn't already exist?
     */
    @InterfaceAudience.Public
    public abstract void setCreateTarget(boolean createTarget);

    /**
     * Should the replication operate continuously, copying changes as soon as the
     * source database is modified? (Defaults to NO).
     */
    @InterfaceAudience.Public
    public boolean isContinuous() {
        return continuous;
    }

    /**
     * Set whether the replication should operate continuously.
     */
    @InterfaceAudience.Public
    public void setContinuous(boolean continuous) {
        if (!isRunning()) {
            this.continuous = continuous;
        }
    }

    /**
     * Name of an optional filter function to run on the source server. Only documents for
     * which the function returns true are replicated.
     *
     * For a pull replication, the name looks like "designdocname/filtername".
     * For a push replication, use the name under which you registered the filter with the Database.
     */
    @InterfaceAudience.Public
    public String getFilter() {
        return filterName;
    }

    /**
     * Set the filter to be used by this replication
     */
    @InterfaceAudience.Public
    public void setFilter(String filterName) {
        this.filterName = filterName;
    }

    /**
     * Parameters to pass to the filter function.  Should map strings to strings.
     */
    @InterfaceAudience.Public
    public Map<String, Object> getFilterParams() {
        return filterParams;
    }

    /**
     * Set parameters to pass to the filter function.
     */
    @InterfaceAudience.Public
    public void setFilterParams(Map<String, Object> filterParams) {
        this.filterParams = filterParams;
    }

    /**
     * List of Sync Gateway channel names to filter by; a nil value means no filtering, i.e. all
     * available channels will be synced.  Only valid for pull replications whose source database
     * is on a Couchbase Sync Gateway server.  (This is a convenience that just reads or
     * changes the values of .filter and .query_params.)
     */
    @InterfaceAudience.Public
    public List<String> getChannels() {
        if (filterParams == null || filterParams.isEmpty()) {
            return new ArrayList<String>();
        }
        String params = (String) filterParams.get(CHANNELS_QUERY_PARAM);
        if (!isPull() || getFilter() == null || !getFilter().equals(BY_CHANNEL_FILTER_NAME) || params == null || params.isEmpty()) {
            return new ArrayList<String>();
        }
        String[] paramsArray = params.split(",");
        return new ArrayList<String>(Arrays.asList(paramsArray));
    }

    /**
     * Set the list of Sync Gateway channel names
     */
    @InterfaceAudience.Public
    public void setChannels(List<String> channels) {
        if (channels != null && !channels.isEmpty()) {
            if (!isPull()) {
                Log.w(Database.TAG, "filterChannels can only be set in pull replications");
                return;
            }
            setFilter(BY_CHANNEL_FILTER_NAME);
            Map<String, Object> filterParams = new HashMap<String, Object>();
            filterParams.put(CHANNELS_QUERY_PARAM, TextUtils.join(",", channels));
            setFilterParams(filterParams);
        } else if (getFilter().equals(BY_CHANNEL_FILTER_NAME)) {
            setFilter(null);
            setFilterParams(null);
        }
    }

    /**
     * Extra HTTP headers to send in all requests to the remote server.
     * Should map strings (header names) to strings.
     */
    @InterfaceAudience.Public
    public Map<String, Object> getHeaders() {
        return requestHeaders;
    }

    /**
     * Set Extra HTTP headers to be sent in all requests to the remote server.
     */
    @InterfaceAudience.Public
    public void setHeaders(Map<String, Object> requestHeadersParam) {
        if (requestHeadersParam != null && !requestHeaders.equals(requestHeadersParam)) {
            requestHeaders = requestHeadersParam;
        }
    }

    /**
     * Gets the documents to specify as part of the replication.
     */
    @InterfaceAudience.Public
    public List<String> getDocsIds() {
        return documentIDs;
    }

    /**
     * Sets the documents to specify as part of the replication.
     */
    @InterfaceAudience.Public
    public void setDocIds(List<String> docIds) {
        documentIDs = docIds;
    }

    /**
     * The replication's current state, one of {stopped, offline, idle, active}.
     */
    @InterfaceAudience.Public
    public ReplicationStatus getStatus() {
        return status;
    }

    /**
     * The number of completed changes processed, if the task is active, else 0 (observable).
     */
    @InterfaceAudience.Public
    public int getCompletedChangesCount() {
        return completedChangesCount;
    }

    /**
     * The total number of changes to be processed, if the task is active, else 0 (observable).
     */
    @InterfaceAudience.Public
    public int getChangesCount() {
        return changesCount;
    }

    /**
     * True while the replication is running, False if it's stopped.
     * Note that a continuous replication never actually stops; it only goes idle waiting for new
     * data to appear.
     */
    @InterfaceAudience.Public
    public boolean isRunning() {
        return running;
    }

    /**
     * The error status of the replication, or null if there have not been any errors since
     * it started.
     */
    @InterfaceAudience.Public
    public Throwable getLastError() {
        return error;
    }

    /**
     * Starts the replication, asynchronously.
     */
    @InterfaceAudience.Public
    public void start() {

        if (running) {
            return;
        }
        this.sessionID = String.format("repl%03d", ++lastSessionID);
        Log.v(Database.TAG, toString() + " STARTING ...");
        running = true;
        lastSequence = null;

        checkSession();

    }

    /**
     * Stops replication, asynchronously.
     */
    @InterfaceAudience.Public
    public void stop() {
        if (!running) {
            return;
        }
        Log.v(Database.TAG, toString() + " STOPPING...");
        batcher.flush();
        continuous = false;
        if (asyncTaskCount == 0) {
            stopped();
        }
    }

    /**
     * Restarts a completed or failed replication.
     */
    @InterfaceAudience.Public
    public void restart() {
        // TODO: add the "started" flag and check it here
        stop();
        start();
    }

    /**
     * Adds a change delegate that will be called whenever the Replication changes.
     */
    @InterfaceAudience.Public
    public void addChangeListener(ChangeListener changeListener) {
        changeListeners.add(changeListener);
    }

    @Override
    @InterfaceAudience.Public
    public String toString() {
        String maskedRemoteWithoutCredentials = (remote != null ? remote.toExternalForm() : "");
        maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.replaceAll("://.*:.*@", "://---:---@");
        String name = getClass().getSimpleName() + "[" + maskedRemoteWithoutCredentials + "]";
        return name;
    }

    /**
     * The type of event raised by a Replication when any of the following
     * properties change: mode, running, error, completed, total.
     */
    @InterfaceAudience.Public
    public static class ChangeEvent {

        private Replication source;

        public ChangeEvent(Replication source) {
            this.source = source;
        }

        public Replication getSource() {
            return source;
        }

    }

    /**
     * A delegate that can be used to listen for Replication changes.
     */
    @InterfaceAudience.Public
    public static interface ChangeListener {
        public void changed(ChangeEvent event);
    }

    /**
     * Removes the specified delegate as a listener for the Replication change event.
     */
    @InterfaceAudience.Public
    public void removeChangeListener(ChangeListener changeListener) {
        changeListeners.remove(changeListener);
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void setAuthorizer(Authorizer authorizer) {
        this.authorizer = authorizer;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public Authorizer getAuthorizer() {
        return authorizer;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void databaseClosing() {
        saveLastSequence();
        stop();
        db = null;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public String getLastSequence() {
        return lastSequence;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void setLastSequence(String lastSequenceIn) {
        if (lastSequenceIn != null && !lastSequenceIn.equals(lastSequence)) {
            Log.v(Database.TAG, toString() + ": Setting lastSequence to " + lastSequenceIn + " from( " + lastSequence + ")");
            lastSequence = lastSequenceIn;
            if (!lastSequenceChanged) {
                lastSequenceChanged = true;
                workExecutor.schedule(new Runnable() {

                    @Override
                    public void run() {
                        saveLastSequence();
                    }
                }, 2 * 1000, TimeUnit.MILLISECONDS);
            }
        }
    }


    @InterfaceAudience.Private
    /* package */ void setCompletedChangesCount(int processed) {
        this.completedChangesCount = processed;
        notifyChangeListeners();
    }

    @InterfaceAudience.Private
    /* package */ void setChangesCount(int total) {
        this.changesCount = total;
        notifyChangeListeners();
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public String getSessionID() {
        return sessionID;
    }

    @InterfaceAudience.Private
    protected void checkSession() {
        if (getAuthorizer() != null && getAuthorizer().usesCookieBasedLogin()) {
            checkSessionAtPath("/_session");
        } else {
            fetchRemoteCheckpointDoc();
        }
    }

    @InterfaceAudience.Private
    protected void checkSessionAtPath(final String sessionPath) {

        asyncTaskStarted();
        sendAsyncRequest("GET", sessionPath, null, new RemoteRequestCompletionBlock() {

            @Override
            public void onCompletion(Object result, Throwable e) {


                if (e instanceof HttpResponseException &&
                        ((HttpResponseException) e).getStatusCode() == 404 &&
                        sessionPath.equalsIgnoreCase("/_session")) {

                    checkSessionAtPath("_session");
                    return;
                } else {
                    Map<String, Object> response = (Map<String, Object>) result;
                    Map<String, Object> userCtx = (Map<String, Object>) response.get("userCtx");
                    String username = (String) userCtx.get("name");
                    if (username != null && username.length() > 0) {
                        Log.d(Database.TAG, String.format("%s Active session, logged in as %s", this, username));
                        fetchRemoteCheckpointDoc();
                    } else {
                        Log.d(Database.TAG, String.format("%s No active session, going to login", this));
                        login();
                    }

                }
                asyncTaskFinished(1);
            }

        });
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public abstract void beginReplicating();

    @InterfaceAudience.Private
    protected void stopped() {
        Log.v(Database.TAG, toString() + " STOPPED");
        running = false;
        this.completedChangesCount = this.changesCount = 0;

        saveLastSequence();
        notifyChangeListeners();

        batcher = null;
        db = null;
    }

    @InterfaceAudience.Private
    private void notifyChangeListeners() {
        updateProgress();
        for (ChangeListener listener : changeListeners) {
            ChangeEvent changeEvent = new ChangeEvent(this);
            listener.changed(changeEvent);
        }
    }

    @InterfaceAudience.Private
    protected void login() {
        Map<String, String> loginParameters = getAuthorizer().loginParametersForSite(remote);
        if (loginParameters == null) {
            Log.d(Database.TAG, String.format("%s: %s has no login parameters, so skipping login", this, getAuthorizer()));
            fetchRemoteCheckpointDoc();
            return;
        }

        final String loginPath = getAuthorizer().loginPathForSite(remote);

        Log.d(Database.TAG, String.format("%s: Doing login with %s at %s", this, getAuthorizer().getClass(), loginPath));
        asyncTaskStarted();
        sendAsyncRequest("POST", loginPath, loginParameters, new RemoteRequestCompletionBlock() {

            @Override
            public void onCompletion(Object result, Throwable e) {
                if (e != null) {
                    Log.d(Database.TAG, String.format("%s: Login failed for path: %s", this, loginPath));
                    error = e;
                }
                else {
                    Log.d(Database.TAG, String.format("%s: Successfully logged in!", this));
                    fetchRemoteCheckpointDoc();
                }
                asyncTaskFinished(1);
            }

        });

    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public synchronized void asyncTaskStarted() {
        ++asyncTaskCount;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public synchronized void asyncTaskFinished(int numTasks) {
        this.asyncTaskCount -= numTasks;
        assert(asyncTaskCount >= 0);
        if (asyncTaskCount == 0) {
            if (!continuous) {
                stopped();
            }
        }
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void addToInbox(RevisionInternal rev) {
        if (batcher.count() == 0) {
            active = true;
        }
        batcher.queueObject(rev);
    }

    @InterfaceAudience.Private
    protected void processInbox(RevisionList inbox) {

    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void sendAsyncRequest(String method, String relativePath, Object body, RemoteRequestCompletionBlock onCompletion) {
        try {
            String urlStr = buildRelativeURLString(relativePath);
            URL url = new URL(urlStr);
            sendAsyncRequest(method, url, body, onCompletion);
        } catch (MalformedURLException e) {
            Log.e(Database.TAG, "Malformed URL for async request", e);
        }
    }

    @InterfaceAudience.Private
    /* package */ String buildRelativeURLString(String relativePath) {

        // the following code is a band-aid for a system problem in the codebase
        // where it is appending "relative paths" that start with a slash, eg:
        //     http://dotcom/db/ + /relpart == http://dotcom/db/relpart
        // which is not compatible with the way the java url concatonation works.

        String remoteUrlString = remote.toExternalForm();
        if (remoteUrlString.endsWith("/") && relativePath.startsWith("/")) {
            remoteUrlString = remoteUrlString.substring(0, remoteUrlString.length() - 1);
        }
        return remoteUrlString + relativePath;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void sendAsyncRequest(String method, URL url, Object body, RemoteRequestCompletionBlock onCompletion) {
        RemoteRequest request = new RemoteRequest(workExecutor, clientFactory, method, url, body, getHeaders(), onCompletion);
        remoteRequestExecutor.execute(request);
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void sendAsyncMultipartDownloaderRequest(String method, String relativePath, Object body, Database db, RemoteRequestCompletionBlock onCompletion) {
        try {

            String urlStr = buildRelativeURLString(relativePath);
            URL url = new URL(urlStr);

            RemoteMultipartDownloaderRequest request = new RemoteMultipartDownloaderRequest(
                    workExecutor,
                    clientFactory,
                    method,
                    url,
                    body,
                    db,
                    getHeaders(),
                    onCompletion);
            remoteRequestExecutor.execute(request);
        } catch (MalformedURLException e) {
            Log.e(Database.TAG, "Malformed URL for async request", e);
        }
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void sendAsyncMultipartRequest(String method, String relativePath, MultipartEntity multiPartEntity, RemoteRequestCompletionBlock onCompletion) {
        URL url = null;
        try {
            String urlStr = buildRelativeURLString(relativePath);
            url = new URL(urlStr);
        } catch (MalformedURLException e) {
            throw new IllegalArgumentException(e);
        }
        RemoteMultipartRequest request = new RemoteMultipartRequest(
                workExecutor,
                clientFactory,
                method,
                url,
                multiPartEntity,
                getHeaders(),
                onCompletion);
        remoteRequestExecutor.execute(request);
    }

    /**
     * CHECKPOINT STORAGE: *
     */

    @InterfaceAudience.Private
    /* package */ void maybeCreateRemoteDB() {
        // Pusher overrides this to implement the .createTarget option
    }

    /**
     * This is the _local document ID stored on the remote server to keep track of state.
     * Its ID is based on the local database ID (the private one, to make the result unguessable)
     * and the remote database's URL.
     * @exclude
     */
    @InterfaceAudience.Private
    public String remoteCheckpointDocID() {
        if (db == null) {
            return null;
        }
        String input = db.privateUUID() + "\n" + remote.toExternalForm() + "\n" + (!isPull() ? "1" : "0");
        return Misc.TDHexSHA1Digest(input.getBytes());
    }

    @InterfaceAudience.Private
    private boolean is404(Throwable e) {
        if (e instanceof HttpResponseException) {
            return ((HttpResponseException) e).getStatusCode() == 404;
        }
        return false;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void fetchRemoteCheckpointDoc() {
        lastSequenceChanged = false;
        final String localLastSequence = db.lastSequenceWithRemoteURL(remote, !isPull());
        if (localLastSequence == null) {
            maybeCreateRemoteDB();
            beginReplicating();
            return;
        }

        asyncTaskStarted();
        sendAsyncRequest("GET", "/_local/" + remoteCheckpointDocID(), null, new RemoteRequestCompletionBlock() {

            @Override
            public void onCompletion(Object result, Throwable e) {
                if (e != null && !is404(e)) {
                    Log.d(Database.TAG, this + " error getting remote checkpoint: " + e);
                    error = e;
                } else {
                    if (e != null && is404(e)) {
                        Log.d(Database.TAG, this + " 404 error getting remote checkpoint " + remoteCheckpointDocID() + ", calling maybeCreateRemoteDB");
                        maybeCreateRemoteDB();
                    }
                    Map<String, Object> response = (Map<String, Object>) result;
                    remoteCheckpoint = response;
                    String remoteLastSequence = null;
                    if (response != null) {
                        remoteLastSequence = (String) response.get("lastSequence");
                    }
                    if (remoteLastSequence != null && remoteLastSequence.equals(localLastSequence)) {
                        lastSequence = localLastSequence;
                        Log.v(Database.TAG, this + ": Replicating from lastSequence=" + lastSequence);
                    } else {
                        Log.v(Database.TAG, this + ": lastSequence mismatch: I had " + localLastSequence + ", remote had " + remoteLastSequence);
                    }
                    beginReplicating();
                }
                asyncTaskFinished(1);
            }

        });
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void saveLastSequence() {
        if (!lastSequenceChanged) {
            return;
        }
        if (savingCheckpoint) {
            // If a save is already in progress, don't do anything. (The completion block will trigger
            // another save after the first one finishes.)
            overdueForSave = true;
            return;
        }

        lastSequenceChanged = false;
        overdueForSave = false;

        Log.v(Database.TAG, this + " checkpointing sequence=" + lastSequence);
        final Map<String, Object> body = new HashMap<String, Object>();
        if (remoteCheckpoint != null) {
            body.putAll(remoteCheckpoint);
        }
        body.put("lastSequence", lastSequence);

        String remoteCheckpointDocID = remoteCheckpointDocID();
        if (remoteCheckpointDocID == null) {
            return;
        }
        savingCheckpoint = true;
        sendAsyncRequest("PUT", "/_local/" + remoteCheckpointDocID, body, new RemoteRequestCompletionBlock() {

            @Override
            public void onCompletion(Object result, Throwable e) {
                savingCheckpoint = false;
                if (e != null) {
                    Log.v(Database.TAG, this + ": Unable to save remote checkpoint", e);
                    // TODO: If error is 401 or 403, and this is a pull, remember that remote is read-only and don't attempt to read its checkpoint next time.
                } else {
                    Map<String, Object> response = (Map<String, Object>) result;
                    body.put("_rev", response.get("rev"));
                    remoteCheckpoint = body;
                }
                if (overdueForSave) {
                    saveLastSequence();
                }
            }

        });
        db.setLastSequence(lastSequence, remote, !isPull());
    }

    @InterfaceAudience.Private
    /* package */ boolean goOffline() {
        if (!online) {
            return false;
        }
        online = false;
        // TODO: [self stopRemoteRequests]; - remoteRequestExecutor.shutdown(); or remoteRequestExecutor.shutdownNow();
        updateProgress();
        return true;
    }

    @InterfaceAudience.Private
    /* package */ void updateProgress() {
        if (!isRunning()) {
            status = ReplicationStatus.REPLICATION_STOPPED;
        } else if (!online) {
            status = ReplicationStatus.REPLICATION_OFFLINE;
        } else {
            if (active) {
                status = ReplicationStatus.REPLICATION_ACTIVE;
            } else {
                status = ReplicationStatus.REPLICATION_IDLE;
            }
        }
    }


}
