package com.couchbase.lite;

import com.couchbase.lite.internal.InterfaceAudience;
import com.couchbase.lite.util.Log;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutionException;

/**
 * A Query subclass that automatically refreshes the result rows every time the database changes.
 * All you need to do is use add a listener to observe changes.
 */
public final class LiveQuery extends Query implements Database.ChangeListener {

    private boolean observing;
    private boolean willUpdate;
    private QueryEnumerator rows;
    private List<ChangeListener> observers = new ArrayList<ChangeListener>();
    private Throwable lastError;

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    /* package */ LiveQuery(Query query) {
        super(query.getDatabase(), query.getView());
        setLimit(query.getLimit());
        setSkip(query.getSkip());
        setStartKey(query.getStartKey());
        setEndKey(query.getEndKey());
        setDescending(query.isDescending());
        setPrefetch(query.shouldPrefetch());
        setKeys(query.getKeys());
        setGroupLevel(query.getGroupLevel());
        setMapOnly(query.isMapOnly());
        setStartKeyDocId(query.getStartKeyDocId());
        setEndKeyDocId(query.getEndKeyDocId());
        setIndexUpdateMode(query.getIndexUpdateMode());
    }

    /**
     * Sends the query to the server and returns an enumerator over the result rows (Synchronous).
     * Note: In a CBLLiveQuery you should add a ChangeListener and call start() instead.
     */
    @Override
    @InterfaceAudience.Public
    public QueryEnumerator run() throws CouchbaseLiteException {

        try {
            waitForRows();
        } catch (Exception e) {
            lastError = e;
            throw new CouchbaseLiteException(e, Status.INTERNAL_SERVER_ERROR);
        }

        if (rows == null) {
            return null;
        }
        else {
            // Have to return a copy because the enumeration has to start at item #0 every time
            return new QueryEnumerator(rows);
        }
    }

    /**
     * Returns the last error, if any, that occured while executing the Query, otherwise null.
     */
    @InterfaceAudience.Public
    public Throwable getLastError() {
        return lastError;
    }

    /**
     * Starts observing database changes. The .rows property will now update automatically. (You
     * usually don't need to call this yourself, since calling getRows() will start it for you
     */
    @InterfaceAudience.Public
    public void start() {
        if (!observing) {
            observing = true;
            getDatabase().addChangeListener(this);
            update();
        }
    }

    /**
     * Stops observing database changes. Calling start() or rows() will restart it.
     */
    @InterfaceAudience.Public
    public void stop() {

        if (observing) {
            observing = false;
            getDatabase().removeChangeListener(this);
        }

        if (willUpdate) {
            setWillUpdate(false);
        }

        // slight diversion from iOS version -- cancel the updateQueryFuture
        // regardless of the willUpdate value, since there can be an update in flight
        // with willUpdate set to false.  was needed to make testLiveQueryStop() unit test pass.
        if (updateQueryFuture != null) {
            boolean cancelled = updateQueryFuture.cancel(true);
            Log.d(Database.TAG, this + ": cancelled updateQueryFuture " + updateQueryFuture + ", returned: " + cancelled);
        }

    }

    /**
     * Blocks until the intial async query finishes. After this call either .rows or .error will be non-nil.
     */
    @InterfaceAudience.Public
    public void waitForRows() throws InterruptedException, ExecutionException {
        start();
        try {
            updateQueryFuture.get();
        } catch (InterruptedException e) {
            Log.e(Database.TAG, "Got interrupted exception waiting for rows", e);
            throw e;
        } catch (ExecutionException e) {
            Log.e(Database.TAG, "Got execution exception waiting for rows", e);
            throw e;
        }
    }

    /**
     * Gets the results of the Query. The value will be null until the initial Query completes.
     */
    @InterfaceAudience.Public
    public QueryEnumerator getRows() {
        start();
        if (rows == null) {
            return null;
        }
        else {
            // Have to return a copy because the enumeration has to start at item #0 every time
            return new QueryEnumerator(rows);
        }
    }

    /**
     * Add a change listener to be notified when the live query result
     * set changes.
     */
    @InterfaceAudience.Public
    public void addChangeListener(ChangeListener changeListener) {
        observers.add(changeListener);
    }

    /**
     * Remove previously added change listener
     */
    @InterfaceAudience.Public
    public void removeChangeListener(ChangeListener changeListener) {
        observers.remove(changeListener);
    }

    /**
     * The type of event raised when a LiveQuery result set changes.
     */
    @InterfaceAudience.Public
    public static class ChangeEvent {

        private LiveQuery source;
        private Throwable error;
        private QueryEnumerator queryEnumerator;

        ChangeEvent() {
        }

        ChangeEvent(LiveQuery source, QueryEnumerator queryEnumerator) {
            this.source = source;
            this.queryEnumerator = queryEnumerator;
        }

        ChangeEvent(Throwable error) {
            this.error = error;
        }

        public LiveQuery getSource() {
            return source;
        }

        public Throwable getError() {
            return error;
        }

        public QueryEnumerator getRows() {
            return queryEnumerator;
        }

    }

    /**
     * A delegate that can be used to listen for LiveQuery result set changes.
     */
    @InterfaceAudience.Public
    public static interface ChangeListener {
        public void changed(ChangeEvent event);
    }

    @InterfaceAudience.Private
    /* package */ void update() {
        Log.d(Database.TAG, "update() called");

        if (getView() == null) {
            throw new IllegalStateException("Cannot start LiveQuery when view is null");
        }
        setWillUpdate(false);
        updateQueryFuture = runAsyncInternal(new QueryCompleteListener() {
            @Override
            public void completed(QueryEnumerator rowsParam, Throwable error) {
                if (error != null) {
                    for (ChangeListener observer : observers) {
                        observer.changed(new ChangeEvent(error));
                    }
                    lastError = error;
                } else {
                    if (rowsParam != null && !rowsParam.equals(rows)) {
                        setRows(rowsParam);
                        for (ChangeListener observer : observers) {
                            Log.d(Database.TAG, "update() calling back observer with rows");
                            observer.changed(new ChangeEvent(LiveQuery.this, rows));
                        }
                    }
                    lastError = null;
                }
            }
        });
    }


    /**
     * @exclude
     */
    @Override
    @InterfaceAudience.Private
    public void changed(Database.ChangeEvent event) {
        if (!willUpdate) {
            setWillUpdate(true);
            updateQueryFuture = getDatabase().runAsync(new AsyncTask() {
                @Override
                public void run(Database database) {
                    update();
                }
            });
            Log.d(Database.TAG, "changed() called, created updateQueryFuture: " + updateQueryFuture);

        }
    }

    @InterfaceAudience.Private
    private synchronized void setRows(QueryEnumerator queryEnumerator) {
        rows = queryEnumerator;
    }

    @InterfaceAudience.Private
    private synchronized void setWillUpdate(boolean willUpdateParam) {
        willUpdate = willUpdateParam;
    }


}
