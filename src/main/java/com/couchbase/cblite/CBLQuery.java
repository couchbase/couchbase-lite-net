package com.couchbase.cblite;

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




}
