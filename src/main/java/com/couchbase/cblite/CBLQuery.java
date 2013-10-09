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

    private CBLDatabase database;
    private CBLView view;  // null for _all_docs query
    private boolean temporaryView;
    private int skip;
    private int limit = Integer.MAX_VALUE;
    private Object startKey;
    private Object endKey;
    private String startKeyDocId;
    private String endKeyDocId;
    private CBLStaleness stale;
    private boolean descending;
    private boolean prefectch;
    private boolean mapOnly;
    private boolean includeDeleted;
    private List<Object> keys;
    private int groupLevel;
    private long lastSequence;
    private CBLStatus status;  // Result status of last query (.error property derived from this)

    CBLQuery(CBLDatabase database, CBLView view) {
        this.database = database;
        this.view = view;
        limit = Integer.MAX_VALUE;
        mapOnly = (view.getReduce() == null);
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
        prefectch = query.prefectch;
        keys = query.keys;
        groupLevel = query.groupLevel;
        mapOnly = query.mapOnly;
        startKeyDocId = query.startKeyDocId;
        endKeyDocId = query.endKeyDocId;
        stale = query.stale;
    }

    public CBLQueryEnumerator getRows() throws CBLiteException {
        List<Long> outSequence = new ArrayList<Long>();
        List<CBLQueryRow> rows = database.queryViewNamed(view.getName(), getQueryOptions(), outSequence);
        long lastSequence = outSequence.get(0);
        return new CBLQueryEnumerator(database, rows, lastSequence);
    }

    public CBLQueryEnumerator getRowsIfChanged() throws CBLiteException {
        // TODO
        throw new RuntimeException("Not implemented");
    }

    public CBLLiveQuery toLiveQuery() {
        // TODO
        throw new RuntimeException("Not implemented");
    }

    public void runAsync(CBLQueryCompleteFunction queryCompleteFunction) {
        // TODO
        throw new RuntimeException("Not implemented");
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

    public boolean isPrefectch() {
        return prefectch;
    }

    public void setPrefectch(boolean prefectch) {
        this.prefectch = prefectch;
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
        queryOptions.setIncludeDocs(isPrefectch());
        queryOptions.setUpdateSeq(true);
        queryOptions.setInclusiveEnd(true);
        queryOptions.setIncludeDeletedDocs(isIncludeDeleted());
        queryOptions.setStale(getStale());
        return queryOptions;
    }



}
