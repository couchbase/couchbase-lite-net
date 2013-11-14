package com.couchbase.cblite;

public interface CBLQueryCompleteFunction {

    public void onQueryChanged(CBLQueryEnumerator queryEnumerator);

    public void onFailureQueryChanged(Throwable exception);

}
