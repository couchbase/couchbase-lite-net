package com.couchbase.lite.replicator;

import com.couchbase.lite.internal.InterfaceAudience;
import com.couchbase.lite.support.HttpClientFactory;

import java.util.Map;

/**
 * @exclude
 */
@InterfaceAudience.Private
public interface ChangeTrackerClient extends HttpClientFactory {

    void changeTrackerReceivedChange(Map<String,Object> change);

    void changeTrackerStopped(ChangeTracker tracker);
}
