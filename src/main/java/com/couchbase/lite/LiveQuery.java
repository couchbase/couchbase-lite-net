package com.couchbase.lite;

import com.couchbase.lite.internal.InterfaceAudience;
import com.couchbase.lite.util.Log;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CancellationException;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.Future;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * A Query subclass that automatically refreshes the result rows every time the database changes.
 * All you need to do is use add a listener to observe changes.
 */
public final class LiveQuery extends Query implements Database.ChangeListener {

    private boolean observing;
    private QueryEnumerator rows;
    private List<ChangeListener> observers = new ArrayList<ChangeListener>();
    private Throwable lastError;
    private AtomicBoolean runningState; // true == running, false == stopped

    /**
     * If a query is running and the user calls stop() on this query, the future
     * will be used in order to cancel the query in progress.
     */
    protected Future queryFuture;

    /**
     * If the update() method is called while a query is in progress, once it is
     * finished it will be scheduled to re-run update().  This tracks the future
     * related to that scheduled task.
     */
    protected Future rerunUpdateFuture;

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    /* package */ LiveQuery(Query query) {
        super(query.getDatabase(), query.getView());
        runningState = new AtomicBoolean(false);
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
     * Note: In a LiveQuery you should consider adding a ChangeListener and calling start() instead.
     */
    @Override
    @InterfaceAudience.Public
    public QueryEnumerator run() throws CouchbaseLiteException {

        while (true) {
            try {
                waitForRows();
                break;
            } catch (Exception e) {
                if (e instanceof CancellationException) {
                    continue;
                } else {
                    lastError = e;
                    throw new CouchbaseLiteException(e, Status.INTERNAL_SERVER_ERROR);
                }
            }
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

        if (runningState.get() == true) {
            Log.d(Database.TAG, this + ": start() called, but runningState is already true.  Ignoring.");
            return;
        } else {
            Log.d(Database.TAG, this + ": start() called");
            runningState.set(true);
        }

        if (!observing) {
            observing = true;
            getDatabase().addChangeListener(this);
            Log.d(Database.TAG, this + ": start() is calling update()");
            update();
        }
    }

    /**
     * Stops observing database changes. Calling start() or rows() will restart it.
     */
    @InterfaceAudience.Public
    public void stop() {

        if (runningState.get() == false) {
            Log.d(Database.TAG, this + ": stop() called, but runningState is already false.  Ignoring.");
            return;
        } else {
            Log.d(Database.TAG, this + ": stop() called");
            runningState.set(false);
        }

        if (observing) {
            observing = false;
            getDatabase().removeChangeListener(this);
        }

        // slight diversion from iOS version -- cancel the queryFuture
        // regardless of the willUpdate value, since there can be an update in flight
        // with willUpdate set to false.  was needed to make testLiveQueryStop() unit test pass.
        if (queryFuture != null) {
            boolean cancelled = queryFuture.cancel(true);
            Log.d(Database.TAG, this + ": cancelled queryFuture " + queryFuture + ", returned: " + cancelled);
        } else {
            Log.d(Database.TAG, this + ": not cancelling queryFuture, since it is null");
        }

        if (rerunUpdateFuture != null) {
            boolean cancelled = rerunUpdateFuture.cancel(true);
            Log.d(Database.TAG, this + ": cancelled rerunUpdateFuture " + rerunUpdateFuture + ", returned: " + cancelled);
        } else {
            Log.d(Database.TAG, this + ": not cancelling rerunUpdateFuture, since it is null");
        }

    }

    /**
     * Blocks until the intial async query finishes. After this call either .rows or .error will be non-nil.
     */
    @InterfaceAudience.Public
    public void waitForRows() throws CouchbaseLiteException {
        start();

        while (true) {
            try {
                queryFuture.get();
                break;
            } catch (Exception e) {
                if (e instanceof CancellationException) {
                    continue;
                } else {
                    lastError = e;
                    throw new CouchbaseLiteException(e, Status.INTERNAL_SERVER_ERROR);
                }
            }
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
        Log.d(Database.TAG, this + ": update() called.");

        if (getView() == null) {
            throw new IllegalStateException("Cannot start LiveQuery when view is null");
        }

        if (runningState.get() == false) {
            Log.d(Database.TAG, this + ": update() called, but running state == false.  Ignoring.");
            return;
        }

        if (queryFuture != null && !queryFuture.isCancelled() && !queryFuture.isDone()) {
            // There is a already a query in flight, so leave it alone except to schedule something
            // to run update() again once it finishes.
            Log.d(Database.TAG, LiveQuery.this + ": already a query in flight, scheduling call to update() once it's done");
            if (rerunUpdateFuture != null && !rerunUpdateFuture.isCancelled() && !rerunUpdateFuture.isDone()) {
                boolean cancelResult = rerunUpdateFuture.cancel(true);
                Log.d(Database.TAG, LiveQuery.this + ": cancelled " + rerunUpdateFuture + " result: " + cancelResult);
            }
            rerunUpdateFuture = rerunUpdateAfterQueryFinishes();
            Log.d(Database.TAG, LiveQuery.this + ": created new rerunUpdateFuture: " + rerunUpdateFuture);
            return;
        }

        // No query in flight, so kick one off
        queryFuture = runAsyncInternal(new QueryCompleteListener() {
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
                            Log.d(Database.TAG, LiveQuery.this + ": update() calling back observer with rows");
                            observer.changed(new ChangeEvent(LiveQuery.this, rows));
                        }
                    }
                    lastError = null;
                }
            }
        });
        Log.d(Database.TAG, this + ": update() created queryFuture: " + queryFuture);

    }

    /**
     * kick off async task that will wait until the query finishes, and after it
     * does, it will run upate() again in case the current query in flight misses
     * some of the recently added items.
     */
    private Future rerunUpdateAfterQueryFinishes() {
        return getDatabase().getManager().runAsync(new Runnable() {
            @Override
            public void run() {

                if (runningState.get() == false) {
                    Log.d(Database.TAG, this + ": rerunUpdateAfterQueryFinishes.run() fired, but running state == false.  Ignoring.");
                    return;
                }

                if (queryFuture != null) {
                    try {
                        queryFuture.get();
                        update();
                    } catch (Exception e) {
                        if (e instanceof CancellationException) {
                            // can safely ignore these
                        } else {
                            Log.e(Database.TAG, "Got exception waiting for queryFuture to finish", e);
                        }
                    }
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
        Log.d(Database.TAG, this + ": changed() called");
        update();
    }

    @InterfaceAudience.Private
    private synchronized void setRows(QueryEnumerator queryEnumerator) {
        rows = queryEnumerator;
    }


}
