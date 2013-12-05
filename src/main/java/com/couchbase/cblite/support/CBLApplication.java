package com.couchbase.cblite.support;

import android.app.Application;

import com.couchbase.cblite.CBLManager;

/**
 * Helper class to make it easier to share a Couchbase Lite Manager
 * among different activities
 */
public class CBLApplication extends Application {

    private CBLManager manager;

    public CBLManager getManager() {
        return manager;
    }

    public void setManager(CBLManager manager) {
        this.manager = manager;
    }

}
