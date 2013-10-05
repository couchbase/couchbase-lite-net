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

package com.couchbase.cblite.internal;

import com.couchbase.cblite.CBLDatabase;
import com.couchbase.cblite.CBLStatus;

import java.util.HashMap;
import java.util.Map;
import java.util.StringTokenizer;

/**
 * Stores information about a revision -- its docID, revID, and whether it's deleted.
 *
 * It can also store the sequence number and document contents (they can be added after creation).
 */
public class CBLRevisionInternal {

    private String docId;
    private String revId;
    private boolean deleted;
    private CBLBody body;
    private long sequence;
    private CBLDatabase database;  // TODO: get rid of this field!

    public CBLRevisionInternal(String docId, String revId, boolean deleted, CBLDatabase database) {
        this.docId = docId;
        this.revId = revId;
        this.deleted = deleted;
        this.database = database;
    }

    public CBLRevisionInternal(Map<String, Object>properties) {
        this.body = new CBLBody(properties);
    }

    public CBLRevisionInternal(CBLBody body, CBLDatabase database) {
        this((String)body.getPropertyForKey("_id"),
                (String)body.getPropertyForKey("_rev"),
                (((Boolean)body.getPropertyForKey("_deleted") != null)
                        && ((Boolean)body.getPropertyForKey("_deleted") == true)), database);
        this.body = body;
    }

    public CBLRevisionInternal(Map<String, Object> properties, CBLDatabase database) {
        this(new CBLBody(properties), database);
    }

    public Map<String,Object> getProperties() {
        Map<String,Object> result = null;
        if(body != null) {
            result = body.getProperties();
        }
        return result;
    }

    public Object getPropertyForKey(String key) {
        return getProperties().get(key);
    }

    public void setProperties(Map<String,Object> properties) {
        this.body = new CBLBody(properties);

        /*

        native_api branch: commenting this out to match ios code.  the expectation
                           is that this attachment handling will happen else

        // this is a much more simplified version that what happens on the iOS.  it was
        // done this way due to time constraints, so at some point this needs to be
        // revisited to port the remaining functionality.
        Map<String, Object> attachments = (Map<String, Object>) properties.get("_attachments");
        if (attachments != null && attachments.size() > 0) {
            for (String attachmentName : attachments.keySet()) {
                Map<String, Object> attachment = (Map<String, Object>) attachments.get(attachmentName);

                // if there is actual data in this attachment, no need to try to install it
                if (attachment.containsKey("data")) {
                    continue;
                }

                CBLStatus status = database.installPendingAttachment(attachment);
                if (status.isSuccessful() == false) {
                    String msg = String.format("Unable to install pending attachment: %s.  Status: %d", attachment.toString(), status.getCode());
                    throw new IllegalStateException(msg);
                }
            }

        }
        */

    }



    public byte[] getJson() {
        byte[] result = null;
        if(body != null) {
            result = body.getJson();
        }
        return result;
    }

    public void setJson(byte[] json) {
        this.body = new CBLBody(json);
    }

    @Override
    public boolean equals(Object o) {
        boolean result = false;
        if(o instanceof CBLRevisionInternal) {
            CBLRevisionInternal other = (CBLRevisionInternal)o;
            if(docId.equals(other.docId) && revId.equals(other.revId)) {
                result = true;
            }
        }
        return result;
    }

    @Override
    public int hashCode() {
        return docId.hashCode() ^ revId.hashCode();
    }

    public String getDocId() {
        return docId;
    }

    public void setDocId(String docId) {
        this.docId = docId;
    }

    public String getRevId() {
        return revId;
    }

    public void setRevId(String revId) {
        this.revId = revId;
    }

    public boolean isDeleted() {
        return deleted;
    }

    public void setDeleted(boolean deleted) {
        this.deleted = deleted;
    }

    public CBLBody getBody() {
        return body;
    }

    public void setBody(CBLBody body) {
        this.body = body;
    }

    public CBLRevisionInternal copyWithDocID(String docId, String revId) {
        assert((docId != null) && (revId != null));
        assert((this.docId == null) || (this.docId.equals(docId)));
        CBLRevisionInternal result = new CBLRevisionInternal(docId, revId, deleted, database);
        Map<String, Object> properties = getProperties();
        if(properties == null) {
            properties = new HashMap<String, Object>();
        }
        properties.put("_id", docId);
        properties.put("_rev", revId);
        result.setProperties(properties);
        return result;
    }

    public void setSequence(long sequence) {
        this.sequence = sequence;
    }

    public long getSequence() {
        return sequence;
    }

    @Override
    public String toString() {
        return "{" + this.docId + " #" + this.revId + (deleted ? "DEL" : "") + "}";
    }

    /**
     * Generation number: 1 for a new document, 2 for the 2nd revision, ...
     * Extracted from the numeric prefix of the revID.
     */
    public int getGeneration() {
        return generationFromRevID(revId);
    }

    public static int generationFromRevID(String revID) {
        int generation = 0;
        int dashPos = revID.indexOf("-");
        if(dashPos > 0) {
            generation = Integer.parseInt(revID.substring(0, dashPos));
        }
        return generation;
    }

    public static int CBLCollateRevIDs(String revId1, String revId2) {

        String rev1GenerationStr = null;
        String rev2GenerationStr = null;
        String rev1Hash = null;
        String rev2Hash = null;

        StringTokenizer st1 = new StringTokenizer(revId1, "-");
        try {
            rev1GenerationStr = st1.nextToken();
            rev1Hash = st1.nextToken();
        } catch (Exception e) {
        }

        StringTokenizer st2 = new StringTokenizer(revId2, "-");
        try {
            rev2GenerationStr = st2.nextToken();
            rev2Hash = st2.nextToken();
        } catch (Exception e) {
        }

        // improper rev IDs; just compare as plain text:
        if (rev1GenerationStr == null || rev2GenerationStr == null) {
            return revId1.compareToIgnoreCase(revId2);
        }

        Integer rev1Generation;
        Integer rev2Generation;

        try {
            rev1Generation = Integer.parseInt(rev1GenerationStr);
            rev2Generation = Integer.parseInt(rev2GenerationStr);
        } catch (NumberFormatException e) {
            // improper rev IDs; just compare as plain text:
            return revId1.compareToIgnoreCase(revId2);
        }

        // Compare generation numbers; if they match, compare suffixes:
        if (rev1Generation.compareTo(rev2Generation) != 0) {
            return rev1Generation.compareTo(rev2Generation);
        }
        else if (rev1Hash != null && rev2Hash != null) {
            // compare suffixes if possible
            return rev1Hash.compareTo(rev2Hash);
        }
        else {
            // just compare as plain text:
            return revId1.compareToIgnoreCase(revId2);
        }

    }

    public static int CBLCompareRevIDs(String revId1, String revId2) {
        assert(revId1 != null);
        assert(revId2 != null);
        return CBLCollateRevIDs(revId1, revId2);
    }

}
