package com.couchbase.lite;

import com.couchbase.lite.internal.CBLRevisionInternal;

import java.util.Map;

/**
 * Filter block, used in changes feeds and replication.
 */
public interface ReplicationFilter {

    // TODO: this needs to take a SavedRevision as a parameter, however
    // TODO: the SavedRevision class does not exist yet on either Android or iOS
    boolean filter(CBLRevisionInternal revision, Map<String, Object> params);


}
