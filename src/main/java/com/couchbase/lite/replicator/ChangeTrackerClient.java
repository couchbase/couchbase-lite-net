package com.couchbase.lite.replicator;

import com.couchbase.lite.support.HttpClientFactory;

import java.util.Map;

public interface ChangeTrackerClient extends HttpClientFactory {

    void changeTrackerReceivedChange(Map<String,Object> change);

    void changeTrackerStopped(ChangeTracker tracker);
}
