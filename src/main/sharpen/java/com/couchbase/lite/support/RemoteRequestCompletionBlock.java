package com.couchbase.lite.support;


public interface RemoteRequestCompletionBlock {

    public void onCompletion(Object result, Throwable e);

}
