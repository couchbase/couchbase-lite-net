package com.couchbase.cblite;

import java.util.Collections;
import java.util.Map;

/**
 * A result row from a CouchbaseLite view query.
 * Full-text and geo queries return subclasses -- see CBLFullTextQueryRow and CBLGeoQueryRow.
 */
public class CBLQueryRow {

    private Object key;
    private Object value;
    private Object parsedKey;
    private Object parsedValue;
    private long sequence;
    private String sourceDocumentId;
    private Map<String, Object> documentProperties;
    private CBLDatabase database;

    /**
     * The row's key: this is the first parameter passed to the emit() call that generated the row.
     */
    public Object getKey() {
        return key;
    }

    /**
     * The row's value: this is the second parameter passed to the emit() call that generated the row.
     */
    public Object getValue() {
        return value;
    }

    /**
     * The ID of the document described by this view row.  This is not necessarily the same as the
     * document that caused this row to be emitted; see the discussion of the .sourceDocumentID
     * property for details.
     */
    public String getDocumentId() {
        return sourceDocumentId;
    }

    /**
     * The ID of the document that caused this view row to be emitted.  This is the value of
     * the "id" property of the JSON view row. It will be the same as the .documentID property,
     * unless the map function caused a related document to be linked by adding an "_id" key to
     * the emitted value; in this case .documentID will refer to the linked document, while
     * sourceDocumentID always refers to the original document.  In a reduced or grouped query
     * the value will be nil, since the rows don't correspond to individual documents.
     */
    public String getSourceDocumentId() {
        return sourceDocumentId;
    }

    /**
     * The revision ID of the document this row was mapped from.
     */
    public String getDocumentRevision() {
        // TODO: implement
        throw new RuntimeException("TODO: implement");
    }

    public CBLDatabase getDatabase() {
        return database;
    }

    /**
     * The document this row was mapped from.  This will be nil if a grouping was enabled in
     * the query, because then the result rows don't correspond to individual documents.
     */
    public CBLDocument getDocument() {
        // TODO: implement
        throw new RuntimeException("TODO: implement");
    }

    /**
     * The properties of the document this row was mapped from.
     * To get this, you must have set the -prefetch property on the query; else this will be nil.
     */
    public Map<String, Object> getDocumentProperties() {
        Collections.unmodifiableMap(documentProperties);
    }

    /**
     * If this row's key is an array, returns the item at that index in the array.
     * If the key is not an array, index=0 will return the key itself.
     * If the index is out of range, returns nil.
     */
    public Object getKeyAtIndex(int index) {
        // TODO: implement
        throw new RuntimeException("TODO: implement");
    }

    /**
     * Convenience for use in keypaths. Returns the key at the given index.
     */
    public Object getKey0() {
        return getKeyAtIndex(0);
    }
    public Object getKey1() {
        return getKeyAtIndex(1);
    }
    public Object getKey2() {
        return getKeyAtIndex(2);
    }
    public Object getKey3() {
        return getKeyAtIndex(3);
    }

    /**
     * The local sequence number of the associated doc/revision.
     */
    public long getLocalSequence() {
        // TODO: implement
        throw new RuntimeException("TODO: implement");
    }

}
