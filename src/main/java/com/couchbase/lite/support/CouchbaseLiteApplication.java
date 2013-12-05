package com.couchbase.lite.support;

import android.app.Application;

import com.couchbase.lite.Manager;

/**
 * Helper class to make it easier to share a Couchbase Lite Manager
 * among different activities
 */
public class CouchbaseLiteApplication extends Application {

    private Manager manager;

    public Manager getManager() {
        return manager;
    }

    public void setManager(Manager manager) {
        this.manager = manager;
    }

}
