package com.couchbase.lite;

import com.couchbase.lite.internal.CBLRevisionInternal;

/**
 * Validation block, used to approve revisions being added to the database.
 */
public interface CBLValidationBlock {

    boolean validate(CBLRevisionInternal newRevision, CBLValidationContext context);

}
