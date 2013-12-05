package com.couchbase.lite;

public interface CBLReplicationFilterCompiler {

    ReplicationFilter compileFilterFunction(String source, String language);

}
