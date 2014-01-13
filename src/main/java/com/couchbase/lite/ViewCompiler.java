package com.couchbase.lite;

import com.couchbase.lite.internal.InterfaceAudience;

/**
 * An external object that knows how to map source code of some sort into executable functions.
 */
public interface ViewCompiler {

    @InterfaceAudience.Public
    Mapper compileMap(String mapSource, String language);

    @InterfaceAudience.Public
    Reducer compileReduce(String reduceSource, String language);

}
