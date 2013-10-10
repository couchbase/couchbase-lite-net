package com.couchbase.cblite;

public interface CBLDatabaseChangedFunction {

    public void onDatabaseChanged(CBLDatabase database);

    public void onFailureDatabaseChanged(CBLiteException exception);

}
