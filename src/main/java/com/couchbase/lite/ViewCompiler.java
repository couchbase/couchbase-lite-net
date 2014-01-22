package com.couchbase.lite;

import com.couchbase.lite.internal.InterfaceAudience;

/**
 * An external object that knows how to map source code of some sort into executable functions.
 */
public interface ViewCompiler {

    /**
     * Compiles source code into a MapDelegate.
     *
     * @param mapSource The source code to compile into a Mapper.
     * @param language The language of the source.
     * @return A compiled Mapper.
     */
    @InterfaceAudience.Public
    Mapper compileMap(String mapSource, String language);

    /**
     * Compiles source code into a ReduceDelegate.
     *
     * @param reduceSource The source code to compile into a Reducer.
     * @param language The language of the source.
     * @return A compiled Reducer
     */
    @InterfaceAudience.Public
    Reducer compileReduce(String reduceSource, String language);

}
