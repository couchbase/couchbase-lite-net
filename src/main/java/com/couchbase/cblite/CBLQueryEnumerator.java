package com.couchbase.cblite;

import java.util.List;

/**
 * Enumerator on a CBLQuery's result rows.
 * The objects returned are instances of CBLQueryRow.
 */
public class CBLQueryEnumerator {

    private CBLDatabase database;
    private List<CBLQueryRow> rows;
    private int nextRow;
    private long sequenceNumber;
    private CBLStatus status;

    CBLQueryEnumerator(CBLDatabase database, List<CBLQueryRow> rows, long sequenceNumber) {
        this.database = database;
        this.rows = rows;
        this.sequenceNumber = sequenceNumber;

        // Fill in the rows' database reference now
        for (CBLQueryRow row : rows) {
            row.setDatabase(database);
        }
    }

    CBLQueryEnumerator(CBLDatabase database, CBLStatus status) {
        this.database = database;
        this.status = status;
    }

    CBLQueryEnumerator(CBLQueryEnumerator other) {
        this.database = other.database;
        this.rows = other.rows;
        this.sequenceNumber = other.sequenceNumber;
    }

    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (o == null || getClass() != o.getClass()) return false;

        CBLQueryEnumerator that = (CBLQueryEnumerator) o;

        if (rows != null ? !rows.equals(that.rows) : that.rows != null) return false;

        return true;
    }

    public CBLQueryRow getRowAtIndex(int index) {
        return rows.get(index);
    }

    public int getCount() {
        return rows.size();
    }

    public CBLQueryRow getNextRow() {
        if (nextRow >= rows.size()) {
            return null;
        }
        return rows.get(nextRow++);
    }

    /**
     * TODO: in the spec this is called getError() and returns an "Error" object.
     * @return
     */
    public CBLStatus getStatus() {
        return status;
    }

    public long getSequenceNumber() {
        return sequenceNumber;
    }
}
