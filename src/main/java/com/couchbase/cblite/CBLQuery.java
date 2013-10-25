package com.couchbase.cblite;

import java.util.ArrayList;
import java.util.List;

/**
 * Represents a query of a CouchbaseLite 'view', or of a view-like resource like _all_documents.
 */
public class CBLQuery {

    public enum CBLStaleness {
        CBLStaleNever, CBLStaleOK, CBLStaleUpdateAfter
    }

    /**
     * The database that contains this view.
     */
    private CBLDatabase database;

    /**
     * The view object associated with this query
     */
    private CBLView view;  // null for _all_docs query

    /**
     * Is this query based on a temporary view?
     */
    private boolean temporaryView;

    /**
     * The number of initial rows to skip. Default value is 0.
     * Should only be used with small values. For efficient paging, use startKey and limit.
     */
    private int skip;

    /**
     * The maximum number of rows to return. Default value is 0, meaning 'unlimited'.
     */
    private int limit = Integer.MAX_VALUE;

    /**
     * If non-nil, the key value to start at.
     */
    private Object startKey;

    /**
     * If non-nil, the key value to end after.
     */
    private Object endKey;

    /**
     * If non-nil, the document ID to start at.
     * (Useful if the view contains multiple identical keys, making .startKey ambiguous.)
     */
    private String startKeyDocId;

    /**
     * If non-nil, the document ID to end at.
     * (Useful if the view contains multiple identical keys, making .endKey ambiguous.)
     */
    private String endKeyDocId;

    /**
     * If set, the view will not be updated for this query, even if the database has changed.
     * This allows faster results at the expense of returning possibly out-of-date data.
     */
    private CBLStaleness stale;

    /**
     * Should the rows be returned in descending key order? Default value is NO.
     */
    private boolean descending;

    /**
     *  If set to YES, the results will include the entire document contents of the associated rows.
     *  These can be accessed via CBLQueryRow's -documentProperties property.
     *  This slows down the query, but can be a good optimization if you know you'll need the entire
     *  contents of each document. (This property is equivalent to "include_docs" in the CouchDB API.)
     */
    private boolean prefetch;

    /**
     * If set to YES, disables use of the reduce function.
     * (Equivalent to setting "?reduce=false" in the REST API.)
     */
    private boolean mapOnly;

    /**
     * If set to YES, queries created by -queryAllDocuments will include deleted documents.
     * This property has no effect in other types of queries.
     */
    private boolean includeDeleted;

    /**
     * If non-nil, the query will fetch only the rows with the given keys.
     */
    private List<Object> keys;

    /**
     * If non-zero, enables grouping of results, in views that have reduce functions.
     */
    private int groupLevel;

    private long lastSequence;
    private CBLStatus status;  // Result status of last query (.error property derived from this)

    CBLQuery(CBLDatabase database, CBLView view) {
        this.database = database;
        this.view = view;
        limit = Integer.MAX_VALUE;
        mapOnly = (view != null && view.getReduce() == null);
    }

    CBLQuery(CBLDatabase database, CBLMapFunction mapFunction) {
        this(database, database.makeAnonymousView());
        temporaryView = true;
        view.setMap(mapFunction, "");
    }

    CBLQuery(CBLDatabase database, CBLQuery query) {
        this(database, query.getView());
        limit = query.limit;
        skip = query.skip;
        startKey = query.startKey;
        endKey = query.endKey;
        descending = query.descending;
        prefetch = query.prefetch;
        keys = query.keys;
        groupLevel = query.groupLevel;
        mapOnly = query.mapOnly;
        startKeyDocId = query.startKeyDocId;
        endKeyDocId = query.endKeyDocId;
        stale = query.stale;
    }

    /**
     * Sends the query to the server and returns an enumerator over the result rows (Synchronous).
     * If the query fails, this method returns nil and sets the query's .error property.
     */
    public CBLQueryEnumerator getRows() throws CBLiteException {
        List<Long> outSequence = new ArrayList<Long>();
        String viewName = (view != null) ? view.getName() : null;
        List<CBLQueryRow> rows = database.queryViewNamed(viewName, getQueryOptions(), outSequence);
        long lastSequence = outSequence.get(0);
        return new CBLQueryEnumerator(database, rows, lastSequence);
    }

    /**
     * Same as -rows, except returns nil if the query results have not changed since the last time it
     * was evaluated (Synchronous).
     */
    public CBLQueryEnumerator getRowsIfChanged() throws CBLiteException {
        if (database.getLastSequenceNumber() == lastSequence) {
            return null;
        }
        return getRows();
    }

    /**
     * Returns a live query with the same parameters.
     */
    public CBLLiveQuery toLiveQuery() {
        return new CBLLiveQuery(this);
    }

    /**
     *  Starts an asynchronous query. Returns immediately, then calls the onLiveQueryChanged block when the
     *  query completes, passing it the row enumerator. If the query fails, the block will receive
     *  a non-nil enumerator but its .error property will be set to a value reflecting the error.
     *  The originating CBLQuery's .error property will NOT change.
     */
    public void runAsync(final CBLQueryCompleteFunction queryCompleteFunction) {
        runAsyncInternal(queryCompleteFunction);
    }

    Thread runAsyncInternal(final CBLQueryCompleteFunction queryCompleteFunction) {
        Thread t = new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    String viewName = view.getName();
                    CBLQueryOptions options = getQueryOptions();
                    List<Long> outSequence = new ArrayList<Long>();
                    List<CBLQueryRow> rows = database.queryViewNamed(viewName, options, outSequence);
                    long sequenceNumber = outSequence.get(0);
                    CBLQueryEnumerator enumerator = new CBLQueryEnumerator(database, rows, sequenceNumber);
                    queryCompleteFunction.onQueryChanged(enumerator);

                } catch (CBLiteException e) {
                    queryCompleteFunction.onFailureQueryChanged(e);
                }
            }
        });
        t.start();
        return t;

    }

    public CBLView getView() {
        return view;
    }

    public CBLDatabase getDatabase() {
        return database;
    }

    public int getSkip() {
        return skip;
    }

    public void setSkip(int skip) {
        this.skip = skip;
    }

    public int getLimit() {
        return limit;
    }

    public void setLimit(int limit) {
        this.limit = limit;
    }

    public boolean isDescending() {
        return descending;
    }

    public void setDescending(boolean descending) {
        this.descending = descending;
    }

    public Object getStartKey() {
        return startKey;
    }

    public void setStartKey(Object startKey) {
        this.startKey = startKey;
    }

    public Object getEndKey() {
        return endKey;
    }

    public void setEndKey(Object endKey) {
        this.endKey = endKey;
    }

    public String getStartKeyDocId() {
        return startKeyDocId;
    }

    public void setStartKeyDocId(String startKeyDocId) {
        this.startKeyDocId = startKeyDocId;
    }

    public String getEndKeyDocId() {
        return endKeyDocId;
    }

    public void setEndKeyDocId(String endKeyDocId) {
        this.endKeyDocId = endKeyDocId;
    }

    public CBLStaleness getStale() {
        return stale;
    }

    public void setStale(CBLStaleness stale) {
        this.stale = stale;
    }

    public List<Object> getKeys() {
        return keys;
    }

    public void setKeys(List<Object> keys) {
        this.keys = keys;
    }

    public boolean isPrefetch() {
        return prefetch;
    }

    public void setPrefetch(boolean prefetch) {
        this.prefetch = prefetch;
    }

    public boolean isMapOnly() {
        return mapOnly;
    }

    public void setMapOnly(boolean mapOnly) {
        this.mapOnly = mapOnly;
    }

    public boolean isIncludeDeleted() {
        return includeDeleted;
    }

    public void setIncludeDeleted(boolean includeDeleted) {
        this.includeDeleted = includeDeleted;
    }

    public int getGroupLevel() {
        return groupLevel;
    }

    public void setGroupLevel(int groupLevel) {
        this.groupLevel = groupLevel;
    }

    private CBLQueryOptions getQueryOptions() {
        CBLQueryOptions queryOptions = new CBLQueryOptions();
        queryOptions.setStartKey(getStartKey());
        queryOptions.setEndKey(getEndKey());
        queryOptions.setStartKey(getStartKey());
        queryOptions.setKeys(getKeys());
        queryOptions.setSkip(getSkip());
        queryOptions.setLimit(getLimit());
        queryOptions.setReduce(!isMapOnly());
        queryOptions.setReduceSpecified(true);
        queryOptions.setGroupLevel(getGroupLevel());
        queryOptions.setDescending(isDescending());
        queryOptions.setIncludeDocs(isPrefetch());
        queryOptions.setUpdateSeq(true);
        queryOptions.setInclusiveEnd(true);
        queryOptions.setIncludeDeletedDocs(isIncludeDeleted());
        queryOptions.setStale(getStale());
        return queryOptions;
    }

    @Override
    protected void finalize() throws Throwable {
        super.finalize();
        if (temporaryView) {
            view.delete();
        }
    }

}
