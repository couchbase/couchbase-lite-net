package com.couchbase.cblite;

import com.couchbase.cblite.internal.InterfaceAudience;
import com.couchbase.cblite.util.Log;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutionException;

/**
 * A CBLQuery subclass that automatically refreshes the result rows every time the database changes.
 * All you need to do is use add a listener to observe changes.
 */
public class CBLLiveQuery extends CBLQuery implements CBLChangeListener {

    private boolean observing;
    private boolean willUpdate;
    private CBLQueryEnumerator rows;
    private List<ChangeListener> observers = new ArrayList<ChangeListener>();
    private Throwable lastError;

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    CBLLiveQuery(CBLQuery query) {
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
        setStaleness(query.getStaleness());
    }

    /**
     * In CBLLiveQuery the rows accessor is a non-blocking property.
     * Its value will be nil until the initial query finishes.
     */
    @InterfaceAudience.Public
    public CBLQueryEnumerator getRows() throws CBLiteException {
        if (rows == null) {
            return null;
        }
        else {
            // Have to return a copy because the enumeration has to start at item #0 every time
            return new CBLQueryEnumerator(rows);
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
     * usually don't need to call this yourself, since calling rows()
     * call start for you.)
     */
    @InterfaceAudience.Public
    public void start() {
        if (!observing) {
            observing = true;
            getDatabase().addChangeListener(this);
        }
        update();
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
            updateQueryFuture.cancel(true);
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
            Log.e(CBLDatabase.TAG, "Got interrupted exception waiting for rows", e);
            throw e;
        } catch (ExecutionException e) {
            Log.e(CBLDatabase.TAG, "Got execution exception waiting for rows", e);
            throw e;
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

    void update() {
        if (getView() == null) {
            throw new IllegalStateException("Cannot start LiveQuery when view is null");
        }
        setWillUpdate(false);
        updateQueryFuture = runAsyncInternal(new QueryCompleteListener() {
            @Override
            public void queryComplete(CBLQueryEnumerator rows, Throwable error) {
                if (error != null) {
                    for (ChangeListener observer : observers) {
                        observer.change(new ChangeEvent(error));
                    }
                    lastError = error;
                } else {
                    if (rows != null && !rows.equals(rows)) {
                        setRows(rows);
                        for (ChangeListener observer : observers) {
                            observer.change(new ChangeEvent(CBLLiveQuery.this, rows));
                        }
                    }
                    lastError = null;
                }
            }
        });
    }

    public void onDatabaseChanged(CBLDatabase database, Map<String, Object> changeNotification) {
        if (!willUpdate) {
            setWillUpdate(true);
            update();
        }
    }

    public void onFailureDatabaseChanged(Throwable exception) {
        Log.e(CBLDatabase.TAG, "onFailureDatabaseChanged", exception);
    }

    private synchronized void setRows(CBLQueryEnumerator queryEnumerator) {
        rows = queryEnumerator;
    }

    private synchronized void setWillUpdate(boolean willUpdateParam) {
        willUpdate = willUpdateParam;
    }

    public static class ChangeEvent {

        private CBLLiveQuery source;
        private Throwable error;
        private CBLQueryEnumerator queryEnumerator;

        ChangeEvent() {
        }

        ChangeEvent(CBLLiveQuery source, CBLQueryEnumerator queryEnumerator) {
            this.source = source;
            this.queryEnumerator = queryEnumerator;
        }

        ChangeEvent(Throwable error) {
            this.error = error;
        }

        public CBLLiveQuery getSource() {
            return source;
        }

        public Throwable getError() {
            return error;
        }

        public CBLQueryEnumerator getRows() {
            return queryEnumerator;
        }

    }

    public static interface ChangeListener {
        public void change(ChangeEvent event);
    }


}
