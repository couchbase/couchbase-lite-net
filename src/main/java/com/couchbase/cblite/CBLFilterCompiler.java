package com.couchbase.cblite;

public interface CBLFilterCompiler {

    CBLFilterBlock compileFilterFunction(String mapSource, String language);

}
