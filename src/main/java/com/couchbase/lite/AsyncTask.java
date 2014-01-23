package com.couchbase.lite;

/**
 * A delegate that can be run in a transaction on a Database.
 */
public interface AsyncTask {

    void run(Database database);

}

