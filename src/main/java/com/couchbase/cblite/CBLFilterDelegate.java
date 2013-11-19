package com.couchbase.cblite;

import com.couchbase.cblite.internal.CBLRevisionInternal;

import java.util.Map;

/**
 * Filter block, used in changes feeds and replication.
 */
public interface CBLFilterDelegate {

    // TODO: this needs to take a CBLSavedRevision as a parameter, however
    // TODO: the CBLSavedRevision class does not exist yet on either Android or iOS
    boolean filter(CBLRevisionInternal revision, Map<String, Object> params);


}
