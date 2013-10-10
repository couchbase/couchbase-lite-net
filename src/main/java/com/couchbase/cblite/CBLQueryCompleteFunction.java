package com.couchbase.cblite;

public interface CBLQueryCompleteFunction {

    public void onComplete(CBLQueryEnumerator queryEnumerator);

    public void onFailure(CBLiteException exception);

}
