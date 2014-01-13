package com.couchbase.lite;

import com.couchbase.lite.internal.RevisionInternal;

import java.util.Map;

/**
 * Filter block, used in changes feeds and replication.
 */
public interface ReplicationFilter {

    /**
     * True if the Revision should be included in the pushed replication, otherwise false.
     */
    boolean filter(SavedRevision revision, Map<String, Object> params);


}
