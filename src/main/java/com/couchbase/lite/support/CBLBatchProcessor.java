package com.couchbase.lite.support;

import java.util.List;

public interface CBLBatchProcessor<T> {

    void process(List<T> inbox);

}
