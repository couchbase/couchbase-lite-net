package com.couchbase.lite.support;


public interface CBLRemoteRequestCompletionBlock {

    public void onCompletion(Object result, Throwable e);

}
