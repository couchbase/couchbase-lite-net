package com.couchbase.lite;

import com.couchbase.lite.internal.RevisionInternal;

/**
 * Validation block, used to approve revisions being added to the database.
 */
public interface Validator {  // TODO: rename this according to spec

    boolean validate(Revision newRevision, ValidationContext context);

}
