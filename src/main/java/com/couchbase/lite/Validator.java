package com.couchbase.lite;

/**
 * Validation block, used to approve revisions being added to the database.
 */
public interface Validator {

    boolean validate(Revision newRevision, ValidationContext context);

}
