package com.couchbase.cblite;

import java.util.Map;

public interface CBLChangeListener {

    public void onDatabaseChanged(CBLDatabase database, Map<String,Object> changeNotification);

    public void onFailureDatabaseChanged(Throwable exception);

}
