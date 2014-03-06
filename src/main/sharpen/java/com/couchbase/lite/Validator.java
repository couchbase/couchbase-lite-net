package com.couchbase.lite;

/**
 * Validation block, used to approve revisions being added to the database.
 */
public interface Validator {

    void validate(Revision newRevision, ValidationContext context);

}
