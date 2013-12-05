package com.couchbase.lite;

/**
 * An external object that knows how to map source code of some sort into executable functions.
 */
public interface ViewCompiler {

    CBLMapper compileMap(String mapSource, String language);

    CBLReducer compileReduce(String reduceSource, String language);

}
