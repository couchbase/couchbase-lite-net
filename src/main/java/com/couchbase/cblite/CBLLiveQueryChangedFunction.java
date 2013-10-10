package com.couchbase.cblite;

public interface CBLLiveQueryChangedFunction {

    public void onLiveQueryChanged(CBLQueryEnumerator rows);

    public void onFailureLiveQueryChanged(CBLiteException exception);


}
