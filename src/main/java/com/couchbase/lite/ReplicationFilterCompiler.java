package com.couchbase.lite;

public interface ReplicationFilterCompiler {

    ReplicationFilter compileFilterFunction(String source, String language);

}
