package com.couchbase.cblite;

import com.couchbase.cblite.internal.InterfaceAudience;

import java.util.Iterator;
import java.util.List;

/**
 * Enumerator on a CBLQuery's result rows.
 * The objects returned are instances of CBLQueryRow.
 */
public class CBLQueryEnumerator implements Iterator<CBLQueryRow> {

    private CBLDatabase database;
    private List<CBLQueryRow> rows;
    private int nextRow;
    private long sequenceNumber;

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    CBLQueryEnumerator(CBLDatabase database, List<CBLQueryRow> rows, long sequenceNumber) {
        this.database = database;
        this.rows = rows;
        this.sequenceNumber = sequenceNumber;

        // Fill in the rows' database reference now
        for (CBLQueryRow row : rows) {
            row.setDatabase(database);
        }
    }

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    CBLQueryEnumerator(CBLQueryEnumerator other) {
        this.database = other.database;
        this.rows = other.rows;
        this.sequenceNumber = other.sequenceNumber;
    }

    /**
     * Gets the number of rows in the QueryEnumerator.
     */
    @InterfaceAudience.Public
    public int getCount() {
        return rows.size();
    }

    /**
     * Gets the Database's current sequence number at the time the View was generated for the results.
     */
    @InterfaceAudience.Public
    public long getSequenceNumber() {
        return sequenceNumber;
    }

    /**
     * Gets the next CBLQueryRow from the results, or null
     * if there are no more results.
     */
    @Override
    @InterfaceAudience.Public
    public CBLQueryRow next() {
        if (nextRow >= rows.size()) {
            return null;
        }
        return rows.get(nextRow++);
    }

    /**
     * Gets the QueryRow at the specified index in the results.
     */
    @InterfaceAudience.Public
    public CBLQueryRow getRow(int index) {
        return rows.get(index);
    }


    @Override
    @InterfaceAudience.Public
    public boolean equals(Object o) {
        if (this == o) return true;
        if (o == null || getClass() != o.getClass()) return false;

        CBLQueryEnumerator that = (CBLQueryEnumerator) o;

        if (rows != null ? !rows.equals(that.rows) : that.rows != null) return false;

        return true;
    }

    /**
     * Required to satisfy java Iterator interface
     */
    @Override
    @InterfaceAudience.Public
    public boolean hasNext() {
        return nextRow < rows.size();
    }

    /**
     * Required to satisfy java Iterator interface
     */
    @Override
    @InterfaceAudience.Public
    public void remove() {
        throw new UnsupportedOperationException("CBLQueryEnumerator does not allow remove() to be called");
    }
}
