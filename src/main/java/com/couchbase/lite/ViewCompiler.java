package com.couchbase.lite;

/**
 * An external object that knows how to map source code of some sort into executable functions.
 */
public interface ViewCompiler {

    Mapper compileMap(String mapSource, String language);

    Reducer compileReduce(String reduceSource, String language);

}
