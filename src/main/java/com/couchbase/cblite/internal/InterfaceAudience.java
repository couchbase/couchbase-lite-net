package com.couchbase.cblite.internal;


/**
 * Annotations to help mark methods as being public or private.  This is needed to
 * help with the issue that Java’s scoping is not very complete. One is often forced to
 * make a class public in order for other internal components to use it. It does not have
 * friends or sub-package-private like C++
 *
 * Motivated by http://hadoop.apache.org/docs/current/hadoop-project-dist/hadoop-common/InterfaceClassification.html
 */
public class InterfaceAudience {

    /**
     * Intended for use by any project or application.
     */
    public @interface Public {};

    /**
     * Intended for use only within Couchbase Lite itself.
     */
    public @interface Private {};
}
