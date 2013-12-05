package com.couchbase.cblite;

/**
 * An external object that knows how to map source code of some sort into executable functions.
 */
public interface CBLViewCompiler {

    CBLMapper compileMap(String mapSource, String language);

    CBLReducer compileReduce(String reduceSource, String language);

}
