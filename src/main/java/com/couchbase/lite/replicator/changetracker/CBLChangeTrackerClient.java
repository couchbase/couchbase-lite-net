package com.couchbase.lite.replicator.changetracker;

import com.couchbase.lite.support.HttpClientFactory;

import java.util.Map;

public interface CBLChangeTrackerClient extends HttpClientFactory {

    void changeTrackerReceivedChange(Map<String,Object> change);

    void changeTrackerStopped(CBLChangeTracker tracker);
}
