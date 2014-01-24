package com.couchbase.lite;

/**
 * Interface that Document model objects must implement.
 */
public interface DocumentModel {

    public void documentChanged(Document document, DocumentChange change);

}
