package com.couchbase.cblite;

public interface CBLCompileFilterDelegate {

    CBLFilterDelegate compileFilterFunction(String source, String language);

}
