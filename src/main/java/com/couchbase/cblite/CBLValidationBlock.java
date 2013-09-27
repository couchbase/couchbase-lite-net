package com.couchbase.cblite;

import com.couchbase.cblite.internal.CBLRevisionInternal;

/**
 * Validation block, used to approve revisions being added to the database.
 */
public interface CBLValidationBlock {

    boolean validate(CBLRevisionInternal newRevision, CBLValidationContext context);

}
