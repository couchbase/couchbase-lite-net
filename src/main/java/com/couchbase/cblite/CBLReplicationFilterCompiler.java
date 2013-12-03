package com.couchbase.cblite;

public interface CBLReplicationFilterCompiler {

    ReplicationFilter compileFilterFunction(String source, String language);

}
