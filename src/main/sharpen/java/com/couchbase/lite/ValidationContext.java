package com.couchbase.lite;

import com.couchbase.lite.internal.RevisionInternal;

import java.util.ArrayList;
import java.util.EnumSet;
import java.util.List;
import java.util.Map;

/**
 * Context passed into a Validator.
 */
public interface ValidationContext {

    /**
     * The contents of the current revision of the document, or nil if this is a new document.
     */
    SavedRevision getCurrentRevision();

    /**
     * Gets the keys whose values have changed between the current and new Revisions
     */
    public List<String> getChangedKeys();


    /**
     * Rejects the new Revision.
     */
    public void reject();

    /**
     * Rejects the new Revision. The specified message will be included with the resulting error.
     */
    public void reject(String message);

    /**
     * Calls the ChangeValidator for each key/value that has changed, passing both the old
     * and new values. If any delegate call returns false, the enumeration stops and false is
     * returned, otherwise true is returned.
     */
    public boolean validateChanges(ChangeValidator changeValidator);


}



class ValidationContextImpl implements ValidationContext {

    private Database database;
    private RevisionInternal currentRevision;
    private RevisionInternal newRev;
    private String rejectMessage;
    private List<String> changedKeys;

    ValidationContextImpl(Database database, RevisionInternal currentRevision, RevisionInternal newRev) {
        this.database = database;
        this.currentRevision = currentRevision;
        this.newRev = newRev;
    }

    RevisionInternal getCurrentRevisionInternal() {
        if (currentRevision != null) {
            try {
                currentRevision = database.loadRevisionBody(currentRevision, EnumSet.noneOf(Database.TDContentOptions.class));
            } catch (CouchbaseLiteException e) {
                throw new RuntimeException(e);
            }
        }
        return currentRevision;
    }

    @Override
    public SavedRevision getCurrentRevision() {
        final RevisionInternal cur = getCurrentRevisionInternal();
        return cur != null ? new SavedRevision(database, cur) : null;
    }

    @Override
    public List<String> getChangedKeys() {
        if (changedKeys == null) {
            changedKeys = new ArrayList<String>();
            Map<String, Object> cur = getCurrentRevision().getProperties();
            Map<String, Object> nuu = newRev.getProperties();
            for (String key : cur.keySet()) {
                if (!cur.get(key).equals(nuu.get(key)) && !key.equals("_rev")) {
                    changedKeys.add(key);
                }
            }
            for (String key : nuu.keySet()) {
                if (cur.get(key) == null && !key.equals("_rev") && !key.equals("_id")) {
                    changedKeys.add(key);
                }
            }
        }
        return changedKeys;
    }

    @Override
    public void reject() {
        if (rejectMessage == null) {
            rejectMessage = "invalid document";
        }
    }

    @Override
    public void reject(String message) {
        if (rejectMessage == null) {
            rejectMessage = message;
        }
    }

    @Override
    public boolean validateChanges(ChangeValidator changeValidator) {
        Map<String, Object> cur = getCurrentRevision().getProperties();
        Map<String, Object> nuu = newRev.getProperties();
        for (String key : getChangedKeys()) {
            if (!changeValidator.validateChange(key, cur.get(key), nuu.get(key))) {
                reject(String.format("Illegal change to '%s' property", key));
                return false;
            }
        }
        return true;
    }

    String getRejectMessage() {
        return rejectMessage;
    }
}
