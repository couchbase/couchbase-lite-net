package com.couchbase.cblite;

import com.couchbase.cblite.internal.CBLRevisionInternal;

/**
 * Filter block, used in changes feeds and replication.
 */
public interface CBLFilterBlock {

    boolean filter(CBLRevisionInternal revision);

}
