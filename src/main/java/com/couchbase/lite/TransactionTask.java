package com.couchbase.lite;

/**
 * A delegate that can be run in a transaction on a Database.
 */
public interface TransactionTask {

    /**
     * Run in a transaction
     *
     * @return true if the transaction should be committed, otherwise false.
     */
    boolean run();

}
