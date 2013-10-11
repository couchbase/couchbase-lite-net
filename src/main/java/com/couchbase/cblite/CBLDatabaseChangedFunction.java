package com.couchbase.cblite;

import java.util.Map;

public interface CBLDatabaseChangedFunction {

    public void onDatabaseChanged(CBLDatabase database, Map<String,Object> changeNotification);

    public void onFailureDatabaseChanged(CBLiteException exception);

}
