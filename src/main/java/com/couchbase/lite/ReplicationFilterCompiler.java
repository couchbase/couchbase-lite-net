package com.couchbase.lite;

/**
 * A delegate that can be invoked to compile source code into a ReplicationFilter.
 */
public interface ReplicationFilterCompiler {

    /**
     *
     * Compile Filter Function
     *
     * @param source The source code to compile into a ReplicationFilter.
     * @param language The language of the source.
     * @return A compiled ReplicationFilter.
     */
    ReplicationFilter compileFilterFunction(String source, String language);

}
