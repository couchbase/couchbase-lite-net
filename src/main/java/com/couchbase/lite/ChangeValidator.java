package com.couchbase.lite;

/**
 * A delegate that can validate a key/value change.
 */
public interface ChangeValidator {

    /**
     * Validate a change
     *
     * @param key The key of the value being changed.
     * @param oldValue The old value.
     * @param newValue The new value.
     * @return true if the change is valid, false otherwise
     */
    boolean validateChange(String key, Object oldValue, Object newValue);

}
