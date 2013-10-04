/**
 * Original iOS version by  Jens Alfke
 * Ported to Android by Marty Schoch
 *
 * Copyright (c) 2012 Couchbase, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

package com.couchbase.cblite;

import android.util.Log;

import com.couchbase.cblite.internal.CBLRevisionInternal;

import java.util.ArrayList;
import java.util.Collections;
import java.util.EnumSet;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Stores information about a revision -- its docID, revID, and whether it's deleted.
 *
 * It can also store the sequence number and document contents (they can be added after creation).
 */
public class CBLRevision extends CBLRevisionBase {

    private CBLRevisionInternal revisionInternal;
    private boolean checkedProperties;

    CBLRevision(CBLDocument document, CBLRevisionInternal revision) {
        super(document);
        this.revisionInternal = revision;
    }

    CBLRevision(CBLDatabase database, CBLRevisionInternal revision) {
        this(database.getDocument(revision.getDocId()), revision);
    }

    /**
     * Creates a new mutable child revision whose properties and attachments are initially identical
     * to this one's, which you can modify and then save.
     * @return
     */
    public CBLNewRevision createNewRevision() {
        CBLNewRevision newRevision = new CBLNewRevision(document, this);
        return newRevision;
    }


    /**
     * The contents of this revision of the document.
     * Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
     *
     * @return contents of this revision of the document.
     */

    public Map<String,Object> getProperties() {
        Map<String, Object> properties = revisionInternal.getProperties();
        if (properties == null && !checkedProperties) {
            if (loadProperties() == true) {
                properties = revisionInternal.getProperties();
            }
            checkedProperties = true;
        }
        return Collections.unmodifiableMap(properties);
    }

    boolean loadProperties() {
        try {
            HashMap<String, Object> emptyProperties = new HashMap<String, Object>();
            CBLRevisionInternal loadRevision = new CBLRevisionInternal(emptyProperties, database);
            database.loadRevisionBody(loadRevision, EnumSet.noneOf(CBLDatabase.TDContentOptions.class));
            if (loadRevision == null) {
                Log.w(CBLDatabase.TAG, "Couldn't load body/sequence of %s" + this);
                return false;
            }
            revisionInternal = loadRevision;
            return true;

        } catch (CBLiteException e) {
            throw new RuntimeException(e);
        }

    }

    /**
     * Creates and saves a new revision with the given properties.
     * This will fail with a 412 error if the receiver is not the current revision of the document.
     */
    public CBLRevision putProperties(Map<String,Object> properties) throws CBLiteException {
        return document.putProperties(properties, revisionInternal.getRevId());
    }

    /**
     * Has this object fetched its contents from the database yet?
     */
    public boolean isPropertiesLoaded() {
        return revisionInternal.getProperties() != null;
    }

    /**
     * Deletes the document by creating a new deletion-marker revision.
     *
     * @return
     * @throws CBLiteException
     */
    public CBLRevision deleteDocument() throws CBLiteException {
        return putProperties(null);
    }

    /**
     * Returns the history of this document as an array of CBLRevisions, in forward order.
     * Older revisions are NOT guaranteed to have their properties available.
     *
     * @return
     * @throws CBLiteException
     */
    public List<CBLRevision> getRevisionHistory() throws CBLiteException {
        List<CBLRevision> revisions = new ArrayList<CBLRevision>();
        List<CBLRevisionInternal> internalRevisions = database.getRevisionHistory(revisionInternal);
        for (CBLRevisionInternal internalRevision : internalRevisions) {
            if (internalRevision.getRevId().equals(getId())) {
                revisions.add(this);
            }
            else {
                CBLRevision revision = document.getRevisionFromRev(internalRevision);
                revisions.add(revision);
            }

        }
        Collections.reverse(revisions);
        return Collections.unmodifiableList(revisions);
    }

}


