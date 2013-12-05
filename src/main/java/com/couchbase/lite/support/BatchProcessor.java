package com.couchbase.lite.support;

import java.util.List;

public interface BatchProcessor<T> {

    void process(List<T> inbox);

}
