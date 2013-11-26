package com.couchbase.cblite;

public interface CBLReplicationFilterCompiler {

    CBLFilterDelegate compileFilterFunction(String source, String language);

}
