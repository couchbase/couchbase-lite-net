package com.couchbase.cblite.replicator;

import com.couchbase.cblite.CBLDatabase;
import com.couchbase.cblite.CBLMisc;
import com.couchbase.cblite.DocumentChange;
import com.couchbase.cblite.internal.CBLRevisionInternal;
import com.couchbase.cblite.CBLRevisionList;
import com.couchbase.cblite.auth.CBLAuthorizer;
import com.couchbase.cblite.auth.CBLFacebookAuthorizer;
import com.couchbase.cblite.auth.CBLPersonaAuthorizer;
import com.couchbase.cblite.internal.InterfaceAudience;
import com.couchbase.cblite.support.CBLBatchProcessor;
import com.couchbase.cblite.support.CBLBatcher;
import com.couchbase.cblite.support.CBLHttpClientFactory;
import com.couchbase.cblite.support.CBLRemoteMultipartDownloaderRequest;
import com.couchbase.cblite.support.CBLRemoteMultipartRequest;
import com.couchbase.cblite.support.CBLRemoteRequest;
import com.couchbase.cblite.support.CBLRemoteRequestCompletionBlock;
import com.couchbase.cblite.support.HttpClientFactory;
import com.couchbase.cblite.util.URIUtils;
import com.couchbase.cblite.util.Log;

import org.apache.http.client.HttpResponseException;
import org.apache.http.entity.mime.MultipartEntity;

import java.net.MalformedURLException;
import java.net.URI;
import java.net.URL;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Observable;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public abstract class CBLReplicator {

    private static int lastSessionID = 0;

    protected boolean continuous;
    protected String filterName;
    protected ScheduledExecutorService workExecutor;
    protected CBLDatabase db;
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
    protected CBLBatcher<CBLRevisionInternal> batcher;
    protected int asyncTaskCount;
    private int completedChangesCount;
    private int changesCount;
    protected final HttpClientFactory clientFactory;
    private List<ChangeListener> changeListeners;

    protected Map<String, Object> filterParams;
    protected ExecutorService remoteRequestExecutor;
    protected CBLAuthorizer authorizer;

    protected static final int PROCESSOR_DELAY = 500;
    protected static final int INBOX_CAPACITY = 100;
    public static final String REPLICATOR_DATABASE_NAME = "_replicator";

    /**
     * Options for what metadata to include in document bodies
     */
    public enum ReplicationMode {
        REPLICATION_STOPPED,  /**< The replication is finished or hit a fatal error. */
        REPLICATION_OFFLINE,  /**< The remote host is currently unreachable. */
        REPLICATION_IDLE,     /**< Continuous replication is caught up and waiting for more changes.*/
        REPLICATION_ACTIVE    /**< The replication is actively transferring data. */
    }


    /**
     * Private Constructor
     */
    @InterfaceAudience.Private
    public CBLReplicator(CBLDatabase db, URL remote, boolean continuous, ScheduledExecutorService workExecutor) {
        this(db, remote, continuous, null, workExecutor);
    }

    /**
     * Private Constructor
     */
    @InterfaceAudience.Private
    public CBLReplicator(CBLDatabase db, URL remote, boolean continuous, HttpClientFactory clientFactory, ScheduledExecutorService workExecutor) {

        this.db = db;
        this.continuous = continuous;
        this.workExecutor = workExecutor;
        this.remote = remote;
        this.remoteRequestExecutor = Executors.newCachedThreadPool();
        this.changeListeners = new ArrayList<ChangeListener>();

        if (remote.getQuery() != null && !remote.getQuery().isEmpty()) {

            URI uri = URI.create(remote.toExternalForm());

            String personaAssertion = URIUtils.getQueryParameter(uri, CBLPersonaAuthorizer.QUERY_PARAMETER);
            if (personaAssertion != null && !personaAssertion.isEmpty()) {
                String email = CBLPersonaAuthorizer.registerAssertion(personaAssertion);
                CBLPersonaAuthorizer authorizer = new CBLPersonaAuthorizer(email);
                setAuthorizer(authorizer);
            }

            String facebookAccessToken = URIUtils.getQueryParameter(uri, CBLFacebookAuthorizer.QUERY_PARAMETER);
            if (facebookAccessToken != null && !facebookAccessToken.isEmpty()) {
                String email = URIUtils.getQueryParameter(uri, CBLFacebookAuthorizer.QUERY_PARAMETER_EMAIL);
                CBLFacebookAuthorizer authorizer = new CBLFacebookAuthorizer(email);
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

        batcher = new CBLBatcher<CBLRevisionInternal>(workExecutor, INBOX_CAPACITY, PROCESSOR_DELAY, new CBLBatchProcessor<CBLRevisionInternal>() {
            @Override
            public void process(List<CBLRevisionInternal> inbox) {
                Log.v(CBLDatabase.TAG, "*** " + toString() + ": BEGIN processInbox (" + inbox.size() + " sequences)");
                processInbox(new CBLRevisionList(inbox));
                Log.v(CBLDatabase.TAG, "*** " + toString() + ": END processInbox (lastSequence=" + lastSequence);
                active = false;
            }
        });

        this.clientFactory = clientFactory != null ? clientFactory : CBLHttpClientFactory.INSTANCE;

    }

    /**
     * Get the local database which is the source or target of this replication
     */
    @InterfaceAudience.Public
    public CBLDatabase getLocalDatabase() {
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
     * For a push replication, use the name under which you registered the filter with the CBLDatabase.
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
        throw new UnsupportedOperationException();
    }

    /**
     * Set the list of Sync Gateway channel names
     */
    @InterfaceAudience.Public
    public void setChannels(List<String> channels) {
        throw new UnsupportedOperationException();
    }

    /**
     * Extra HTTP headers to send in all requests to the remote server.
     * Should map strings (header names) to strings.
     */
    @InterfaceAudience.Public
    public Map<String, String> getHeaders() {
        throw new UnsupportedOperationException();
    }

    /**
     * Set Extra HTTP headers to be sent in all requests to the remote server.
     */
    @InterfaceAudience.Public
    public void setHeaders(Map<String, String> headers) {
        throw new UnsupportedOperationException();
    }

    /**
     * Gets the documents to specify as part of the replication.
     */
    @InterfaceAudience.Public
    public List<String> getDocsIds() {
        throw new UnsupportedOperationException();
    }

    /**
     * Sets the documents to specify as part of the replication.
     */
    @InterfaceAudience.Public
    public void setDocIds(List<String> docIds) {
        throw new UnsupportedOperationException();
    }

    /**
     * The replication's current state, one of {stopped, offline, idle, active}.
     */
    @InterfaceAudience.Public
    public ReplicationMode getMode() {
        throw new UnsupportedOperationException();
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
        Log.v(CBLDatabase.TAG, toString() + " STARTING ...");
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
        Log.v(CBLDatabase.TAG, toString() + " STOPPING...");
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
        throw new UnsupportedOperationException();
    }

    /**
     * Adds a change delegate that will be called whenever the Replication changes.
     */
    @InterfaceAudience.Public
    public void addChangeListener(ChangeListener changeListener) {
        changeListeners.add(changeListener);
    }

    /**
     * Removes the specified delegate as a listener for the Replication change event.
     */
    @InterfaceAudience.Public
    public void removeChangeListener(ChangeListener changeListener) {
        changeListeners.remove(changeListener);
    }

    public void setAuthorizer(CBLAuthorizer authorizer) {
        this.authorizer = authorizer;
    }

    public CBLAuthorizer getAuthorizer() {
        return authorizer;
    }

    public void databaseClosing() {
        saveLastSequence();
        stop();
        db = null;
    }

    public String toString() {
        String maskedRemoteWithoutCredentials = (remote != null ? remote.toExternalForm() : "");
        maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.replaceAll("://.*:.*@", "://---:---@");
        String name = getClass().getSimpleName() + "[" + maskedRemoteWithoutCredentials + "]";
        return name;
    }

    public String getLastSequence() {
        return lastSequence;
    }

    public void setLastSequence(String lastSequenceIn) {
        if (lastSequenceIn != null && !lastSequenceIn.equals(lastSequence)) {
            Log.v(CBLDatabase.TAG, toString() + ": Setting lastSequence to " + lastSequenceIn + " from( " + lastSequence + ")");
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

    void setCompletedChangesCount(int processed) {
        this.completedChangesCount = processed;
        notifyChangeListeners();
    }

    void setChangesCount(int total) {
        this.changesCount = total;
        notifyChangeListeners();
    }

    public String getSessionID() {
        return sessionID;
    }

    protected void checkSession() {
        if (getAuthorizer() != null && getAuthorizer().usesCookieBasedLogin()) {
            checkSessionAtPath("/_session");
        } else {
            fetchRemoteCheckpointDoc();
        }
    }

    protected void checkSessionAtPath(final String sessionPath) {

        asyncTaskStarted();
        sendAsyncRequest("GET", sessionPath, null, new CBLRemoteRequestCompletionBlock() {

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
                        Log.d(CBLDatabase.TAG, String.format("%s Active session, logged in as %s", this, username));
                        fetchRemoteCheckpointDoc();
                    } else {
                        Log.d(CBLDatabase.TAG, String.format("%s No active session, going to login", this));
                        login();
                    }

                }
                asyncTaskFinished(1);
            }

        });
    }

    public abstract void beginReplicating();


    public void stopped() {
        Log.v(CBLDatabase.TAG, toString() + " STOPPED");
        running = false;
        this.completedChangesCount = this.changesCount = 0;

        saveLastSequence();
        notifyChangeListeners();

        batcher = null;
        db = null;
    }

    private void notifyChangeListeners() {
        for (ChangeListener listener : changeListeners) {
            ChangeEvent changeEvent = new ChangeEvent(this);
            listener.changed(changeEvent);
        }
    }

    protected void login() {
        Map<String, String> loginParameters = getAuthorizer().loginParametersForSite(remote);
        if (loginParameters == null) {
            Log.d(CBLDatabase.TAG, String.format("%s: %s has no login parameters, so skipping login", this, getAuthorizer()));
            fetchRemoteCheckpointDoc();
            return;
        }

        final String loginPath = getAuthorizer().loginPathForSite(remote);

        Log.d(CBLDatabase.TAG, String.format("%s: Doing login with %s at %s", this, getAuthorizer().getClass(), loginPath));
        asyncTaskStarted();
        sendAsyncRequest("POST", loginPath, loginParameters, new CBLRemoteRequestCompletionBlock() {

            @Override
            public void onCompletion(Object result, Throwable e) {
                if (e != null) {
                    Log.d(CBLDatabase.TAG, String.format("%s: Login failed for path: %s", this, loginPath));
                    error = e;
                }
                else {
                    Log.d(CBLDatabase.TAG, String.format("%s: Successfully logged in!", this));
                    fetchRemoteCheckpointDoc();
                }
                asyncTaskFinished(1);
            }

        });

    }

    public synchronized void asyncTaskStarted() {
        ++asyncTaskCount;
    }

    public synchronized void asyncTaskFinished(int numTasks) {
        this.asyncTaskCount -= numTasks;
        if (asyncTaskCount == 0) {
            if (!continuous) {
                stopped();
            }
        }
    }

    public void addToInbox(CBLRevisionInternal rev) {
        if (batcher.count() == 0) {
            active = true;
        }
        batcher.queueObject(rev);
    }

    public void processInbox(CBLRevisionList inbox) {

    }

    public void sendAsyncRequest(String method, String relativePath, Object body, CBLRemoteRequestCompletionBlock onCompletion) {
        try {
            String urlStr = buildRelativeURLString(relativePath);
            URL url = new URL(urlStr);
            sendAsyncRequest(method, url, body, onCompletion);
        } catch (MalformedURLException e) {
            Log.e(CBLDatabase.TAG, "Malformed URL for async request", e);
        }
    }

    String buildRelativeURLString(String relativePath) {

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

    public void sendAsyncRequest(String method, URL url, Object body, CBLRemoteRequestCompletionBlock onCompletion) {
        CBLRemoteRequest request = new CBLRemoteRequest(workExecutor, clientFactory, method, url, body, onCompletion);
        remoteRequestExecutor.execute(request);
    }

    public void sendAsyncMultipartDownloaderRequest(String method, String relativePath, Object body, CBLDatabase db, CBLRemoteRequestCompletionBlock onCompletion) {
        try {

            String urlStr = buildRelativeURLString(relativePath);
            URL url = new URL(urlStr);

            CBLRemoteMultipartDownloaderRequest request = new CBLRemoteMultipartDownloaderRequest(workExecutor, clientFactory, method, url, body, db, onCompletion);
            remoteRequestExecutor.execute(request);
        } catch (MalformedURLException e) {
            Log.e(CBLDatabase.TAG, "Malformed URL for async request", e);
        }
    }

    public void sendAsyncMultipartRequest(String method, String relativePath, MultipartEntity multiPartEntity, CBLRemoteRequestCompletionBlock onCompletion) {
        URL url = null;
        try {
            String urlStr = buildRelativeURLString(relativePath);
            url = new URL(urlStr);
        } catch (MalformedURLException e) {
            throw new IllegalArgumentException(e);
        }
        CBLRemoteMultipartRequest request = new CBLRemoteMultipartRequest(workExecutor, clientFactory, method, url, multiPartEntity, onCompletion);
        remoteRequestExecutor.execute(request);
    }

    /**
     * CHECKPOINT STORAGE: *
     */

     void maybeCreateRemoteDB() {
        // CBLPusher overrides this to implement the .createTarget option
    }

    /**
     * This is the _local document ID stored on the remote server to keep track of state.
     * Its ID is based on the local database ID (the private one, to make the result unguessable)
     * and the remote database's URL.
     */
    public String remoteCheckpointDocID() {
        if (db == null) {
            return null;
        }
        String input = db.privateUUID() + "\n" + remote.toExternalForm() + "\n" + (!isPull() ? "1" : "0");
        return CBLMisc.TDHexSHA1Digest(input.getBytes());
    }

    private boolean is404(Throwable e) {
        if (e instanceof HttpResponseException) {
            return ((HttpResponseException) e).getStatusCode() == 404;
        }
        return false;
    }

    public void fetchRemoteCheckpointDoc() {
        lastSequenceChanged = false;
        final String localLastSequence = db.lastSequenceWithRemoteURL(remote, !isPull());
        if (localLastSequence == null) {
            maybeCreateRemoteDB();
            beginReplicating();
            return;
        }

        asyncTaskStarted();
        sendAsyncRequest("GET", "/_local/" + remoteCheckpointDocID(), null, new CBLRemoteRequestCompletionBlock() {

            @Override
            public void onCompletion(Object result, Throwable e) {
                if (e != null && !is404(e)) {
                    Log.d(CBLDatabase.TAG, this + " error getting remote checkpoint: " + e);
                    error = e;
                } else {
                    if (e != null && is404(e)) {
                        Log.d(CBLDatabase.TAG, this + " 404 error getting remote checkpoint " + remoteCheckpointDocID() + ", calling maybeCreateRemoteDB");
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
                        Log.v(CBLDatabase.TAG, this + ": Replicating from lastSequence=" + lastSequence);
                    } else {
                        Log.v(CBLDatabase.TAG, this + ": lastSequence mismatch: I had " + localLastSequence + ", remote had " + remoteLastSequence);
                    }
                    beginReplicating();
                }
                asyncTaskFinished(1);
            }

        });
    }

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

        Log.v(CBLDatabase.TAG, this + " checkpointing sequence=" + lastSequence);
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
        sendAsyncRequest("PUT", "/_local/" + remoteCheckpointDocID, body, new CBLRemoteRequestCompletionBlock() {

            @Override
            public void onCompletion(Object result, Throwable e) {
                savingCheckpoint = false;
                if (e != null) {
                    Log.v(CBLDatabase.TAG, this + ": Unable to save remote checkpoint", e);
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

    /**
     * The type of event raised by a Replication when any of the following
     * properties change: mode, running, error, completed, total.
     */
    @InterfaceAudience.Public
    public static class ChangeEvent {

        private CBLReplicator source;

        public ChangeEvent(CBLReplicator source) {
            this.source = source;
        }

        public CBLReplicator getSource() {
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


}
