package com.couchbase.cblite.replicator.changetracker;

import com.couchbase.cblite.support.HttpClientFactory;

import java.util.Map;

public interface CBLChangeTrackerClient extends HttpClientFactory {

    void changeTrackerReceivedChange(Map<String,Object> change);

    void changeTrackerStopped(CBLChangeTracker tracker);
}
