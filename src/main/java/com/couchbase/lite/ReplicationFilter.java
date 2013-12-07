package com.couchbase.lite;

import com.couchbase.lite.internal.RevisionInternal;

import java.util.Map;

/**
 * Filter block, used in changes feeds and replication.
 */
public interface ReplicationFilter {

    // TODO: this needs to take a SavedRevision as a parameter, however
    boolean filter(RevisionInternal revision, Map<String, Object> params);


}
