package com.couchbase.cblite;

import com.couchbase.cblite.internal.InterfaceAudience;

import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * A result row from a CouchbaseLite view query.
 * Full-text and geo queries return subclasses -- see CBLFullTextQueryRow and CBLGeoQueryRow.
 */
public class CBLQueryRow {

    private Object key;
    private Object value;
    private long sequence;
    private String sourceDocumentId;
    private Map<String, Object> documentProperties;
    private CBLDatabase database;

    /**
     * Constructor
     *
     * The database property will be filled in when I'm added to a CBLQueryEnumerator.
     */
    @InterfaceAudience.Private
    CBLQueryRow(String documentId, long sequence, Object key, Object value, Map<String, Object> documentProperties) {
        this.sourceDocumentId = documentId;
        this.sequence = sequence;
        this.key = key;
        this.value = value;
        this.documentProperties = documentProperties;
    }

    /**
     * Gets the Database that owns the Query's View.
     */
    @InterfaceAudience.Public
    public CBLDatabase getDatabase() {
        return database;
    }

    /**
     * The document this row was mapped from.  This will be nil if a grouping was enabled in
     * the query, because then the result rows don't correspond to individual documents.
     */
    @InterfaceAudience.Public
    public CBLDocument getDocument() {
        if (getDocumentId() == null) {
            return null;
        }
        CBLDocument document = database.getDocument(getDocumentId());
        document.loadCurrentRevisionFrom(this);
        return document;
    }


    /**
     * The row's key: this is the first parameter passed to the emit() call that generated the row.
     */
    @InterfaceAudience.Public
    public Object getKey() {
        return key;
    }

    /**
     * The row's value: this is the second parameter passed to the emit() call that generated the row.
     */
    @InterfaceAudience.Public
    public Object getValue() {
        return value;
    }

    /**
     * The ID of the document described by this view row.  This is not necessarily the same as the
     * document that caused this row to be emitted; see the discussion of the .sourceDocumentID
     * property for details.
     */
    @InterfaceAudience.Public
    public String getDocumentId() {
        // _documentProperties may have been 'redirected' from a different document
        if (documentProperties != null &&
                documentProperties.get("_id") != null &&
                documentProperties.get("_id") instanceof String) {
            return (String) documentProperties.get("_id");
        }
        else {
            return sourceDocumentId;
        }
    }

    /**
     * The ID of the document that caused this view row to be emitted.  This is the value of
     * the "id" property of the JSON view row. It will be the same as the .documentID property,
     * unless the map function caused a related document to be linked by adding an "_id" key to
     * the emitted value; in this case .documentID will refer to the linked document, while
     * sourceDocumentID always refers to the original document.  In a reduced or grouped query
     * the value will be nil, since the rows don't correspond to individual documents.
     */
    @InterfaceAudience.Public
    public String getSourceDocumentId() {
        return sourceDocumentId;
    }

    /**
     * The revision ID of the document this row was mapped from.
     */
    @InterfaceAudience.Public
    public String getDocumentRevisionId() {
        String rev = null;
        if (documentProperties != null && documentProperties.containsKey("_rev")) {
            rev = (String) documentProperties.get("_rev");
        }
        if (rev == null) {
            if (value instanceof Map) {
                Map<String, Object> mapValue = (Map<String, Object>) value;
                rev = (String) mapValue.get("_rev");
                if (rev == null) {
                    rev = (String) mapValue.get("rev");
                }
            }
        }
        return rev;
    }

    /**
     * The properties of the document this row was mapped from.
     * To get this, you must have set the -prefetch property on the query; else this will be nil.
     * The map returned is immutable (run through Collections.unmodifiableMap)
     */
    @InterfaceAudience.Public
    public Map<String, Object> getDocumentProperties() {
        return documentProperties != null ? Collections.unmodifiableMap(documentProperties) : null;
    }

    /**
     * The local sequence number of the associated doc/revision.
     */
    @InterfaceAudience.Public
    public long getLocalSequence() {
        return sequence;
    }

    /**
     * This is used implicitly by -[CBLLiveQuery update] to decide whether the query result has changed
     * enough to notify the client. So it's important that it not give false positives, else the app
     * won't get notified of changes.
     */
    @Override
    @InterfaceAudience.Public
    public boolean equals(Object object) {
        if (object == this) {
            return true;
        }
        if (!(object instanceof CBLQueryRow)) {
            return false;
        }


        CBLQueryRow other = (CBLQueryRow) object;

        boolean documentPropertiesBothNull = (documentProperties == null &&
                other.getDocumentProperties() == null);
        boolean documentPropertiesEqual = documentPropertiesBothNull ||
                documentProperties.equals(other.getDocumentProperties());

        if (database == other.database
                && key.equals(other.getKey())
                && sourceDocumentId.equals(other.getSourceDocumentId())
                && documentPropertiesEqual) {
            // If values were emitted, compare them. Otherwise we have nothing to go on so check
            // if _anything_ about the doc has changed (i.e. the sequences are different.)
            if (value != null || other.getValue() != null) {
                return value.equals(other.getValue());
            }
            else {
                return sequence == other.sequence;
            }

        }
        return false;
    }


    @InterfaceAudience.Private
    void setDatabase(CBLDatabase database) {
        this.database = database;
    }

    @InterfaceAudience.Private
    public Map<String, Object> asJSONDictionary() {
        Map<String, Object> result = new HashMap<String, Object>();
        if (value != null || sourceDocumentId != null) {
            result.put("key", key);
            result.put("value", value);
            result.put("id", sourceDocumentId);
            if (documentProperties != null){
                result.put("doc", documentProperties);
            }
        } else {
            result.put("key", key);
            result.put("error", "not_found");
        }
        return result;
    }



}
