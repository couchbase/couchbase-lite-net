package com.couchbase.lite;

import com.couchbase.lite.internal.CBLRevisionInternal;

/**
 * Validation block, used to approve revisions being added to the database.
 */
public interface ValidationBlock {

    boolean validate(CBLRevisionInternal newRevision, ValidationContext context);

}
