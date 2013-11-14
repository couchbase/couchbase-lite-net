package com.couchbase.cblite;

import com.couchbase.cblite.util.Log;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * A CBLQuery subclass that automatically refreshes the result rows every time the database changes.
 * All you need to do is use add a listener to observe changes.
 */
public class CBLLiveQuery extends CBLQuery implements CBLDatabaseChangedFunction {

    private boolean observing;
    private boolean willUpdate;
    private CBLQueryEnumerator rows;
    private List<CBLLiveQueryChangedFunction> observers = new ArrayList<CBLLiveQueryChangedFunction>();
    private Thread updaterThread;

    CBLLiveQuery(CBLQuery query) {
        super(query.getDatabase(), query.getView());
        setLimit(query.getLimit());
        setSkip(query.getSkip());
        setStartKey(query.getStartKey());
        setEndKey(query.getEndKey());
        setDescending(query.isDescending());
        setPrefetch(query.isPrefetch());
        setKeys(query.getKeys());
        setGroupLevel(query.getGroupLevel());
        setMapOnly(query.isMapOnly());
        setStartKeyDocId(query.getStartKeyDocId());
        setEndKeyDocId(query.getEndKeyDocId());
        setStale(query.getStale());
    }

    /**
     * Starts observing database changes. The .rows property will now update automatically. (You
     * usually don't need to call this yourself, since calling rows()
     * call start for you.)
     */
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
    public void stop() {
        if (observing) {
            observing = false;
            getDatabase().removeChangeListener(this);
        }

        if (willUpdate) {
            setWillUpdate(false);
            // TODO: how can we cancelPreviousPerformRequestsWithTarget ? as done in iOS?
        }
    }

    /**
     * In CBLLiveQuery the rows accessor is a non-blocking property.
     * Its value will be nil until the initial query finishes.
     */
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
     * Blocks until the intial async query finishes. After this call either .rows or .error will be non-nil.
     */
    public boolean waitForRows() {
        start();
        waitForUpdateThread();

        return rows != null;
    }


    public void addChangeListener(CBLLiveQueryChangedFunction liveQueryChangedFunction) {
        observers.add(liveQueryChangedFunction);
    }

    public void removeChangeListener(CBLLiveQueryChangedFunction liveQueryChangedFunction) {
        observers.remove(liveQueryChangedFunction);
    }

    void update() {
        if (getView() == null) {
            throw new IllegalStateException("Cannot start LiveQuery when view is null");
        }
        setWillUpdate(false);
        updaterThread = runAsyncInternal(new CBLQueryCompleteFunction() {
            @Override
            public void onQueryChanged(CBLQueryEnumerator queryEnumerator) {
                if (queryEnumerator != null && !queryEnumerator.equals(rows)) {
                    setRows(queryEnumerator);
                    for (CBLLiveQueryChangedFunction observer : observers) {
                        observer.onLiveQueryChanged(queryEnumerator);
                    }
                }
            }

            @Override
            public void onFailureQueryChanged(Throwable exception) {
                for (CBLLiveQueryChangedFunction observer : observers) {
                    observer.onFailureLiveQueryChanged(exception);
                }
            }
        });
    }

    public void onDatabaseChanged(CBLDatabase database, Map<String, Object> changeNotification) {
        if (!willUpdate) {
            setWillUpdate(true);

            // wait for any existing updates to finish before starting
            // a new one.  TODO: this whole class needs review and solid testing
            waitForUpdateThread();

            update();
        }

    }

    public void onFailureDatabaseChanged(CBLiteException exception) {
        Log.e(CBLDatabase.TAG, "onFailureDatabaseChanged", exception);
    }

    private synchronized void setRows(CBLQueryEnumerator queryEnumerator) {
        rows = queryEnumerator;
    }

    private synchronized void setWillUpdate(boolean willUpdateParam) {
        willUpdate = willUpdateParam;
    }

    private void waitForUpdateThread() {
        if (updaterThread != null) {
            try {

                updaterThread.join();

            } catch (InterruptedException e) {
            }
        }
    }

}
