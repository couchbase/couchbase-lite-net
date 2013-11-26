package com.couchbase.cblite;

/**
 * An external object that knows how to map source code of some sort into executable functions.
 */
public interface CBLViewCompiler {

    CBLMapper compileMapFunction(String mapSource, String language);

    CBLReducer compileReduceFunction(String reduceSource, String language);

}
