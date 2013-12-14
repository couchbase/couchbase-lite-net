package com.couchbase.lite;

import com.couchbase.lite.internal.RevisionInternal;

/**
 * Validation block, used to approve revisions being added to the database.
 */
public interface ValidationBlock {

    boolean validate(RevisionInternal newRevision, ValidationContext context);

}
