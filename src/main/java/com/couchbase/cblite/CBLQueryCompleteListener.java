package com.couchbase.cblite;

public interface CBLQueryCompleteListener {

    public void onQueryChanged(CBLQueryEnumerator queryEnumerator);

    public void onFailureQueryChanged(Throwable exception);

}
