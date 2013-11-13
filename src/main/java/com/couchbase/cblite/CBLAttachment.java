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

import android.net.Uri;

import com.couchbase.cblite.internal.CBLAttachmentInternal;
import com.couchbase.cblite.internal.CBLRevisionInternal;

import java.io.File;
import java.io.InputStream;
import java.net.URL;
import java.util.Collection;
import java.util.Collections;
import java.util.HashMap;
import java.util.Map;

public class CBLAttachment {


    /**
     * The owning document revision.
     */
    private CBLRevisionBase revision;

    /**
     * Whether or not this attachment is gzipped
     */
    private boolean gzipped;

    /**
     * The owning document.
     */
    private CBLDocument document;

    /**
     * The filename.
     */
    private String name;

    /**
     * The CouchbaseLite metadata about the attachment, that lives in the document.
     */
    private Map<String, Object> metadata;

    /**
     * The body data.
     */
    private InputStream body;

    /**
     * Public Constructor
     */
    public CBLAttachment(InputStream contentStream, String contentType) {
        this.body = contentStream;
        metadata = new HashMap<String, Object>();
        metadata.put("content_type", contentType);
        metadata.put("follows", true);
        this.gzipped = false;
    }


    /**
     * Package Private Constructor
     */
    CBLAttachment(CBLRevisionBase revision, String name, Map<String, Object> metadata) {
        this.revision = revision;
        this.name = name;
        this.metadata = metadata;
        this.gzipped = false;

    }

    public InputStream getBody() throws CBLiteException {
        if (body != null) {
            return body;
        }
        else {
            CBLDatabase db = revision.getDatabase();
            CBLAttachment attachment = db.getAttachmentForSequence(revision.getSequence(), this.name);
            body = attachment.getBody();
            return body;
        }
    }

    /**
     * Get the MIME type of the contents.
     */
    public String getContentType() {
        return (String) metadata.get("content_type");
    }

    public CBLDocument getDocument() {
        return document;
    }


    public CBLRevisionBase getRevision() {
        return revision;
    }

    public String getName() {
        return name;
    }

    void setName(String name) {
        this.name = name;
    }

    void setRevision(CBLRevisionBase revision) {
        this.revision = revision;
    }

    /**
     * Get the length in bytes of the contents.
     */
    public long getLength() {
        Long length = (Long) metadata.get("length");
        if (length != null) {
            return length.longValue();
        }
        else {
            return 0;
        }
    }

    public Map<String, Object> getMetadata() {
        return Collections.unmodifiableMap(metadata);
    }

    /**
     * Get the URL of the file containing the body.
     * This is read-only! DO NOT MODIFY OR DELETE THIS FILE.
     */
    public Uri getBodyURL() {
        try {
            CBLDatabase db = revision.getDatabase();
            String filePath = db.getAttachmentPathForSequence(revision.getSequence(), name);
            if (filePath != null && filePath.length() > 0) {
                return Uri.fromFile(new File(filePath));
            }
            return null;
        } catch (CBLiteException e) {
            throw new RuntimeException(e);
        }
    }

    InputStream getBodyIfNew() {
        return body;
    }

    /**
     * Updates the body, creating a new document revision in the process.
     * If all you need to do to a document is update a single attachment this is an easy way
     * to do it; but if you need to change multiple attachments, or change other body
     * properties, do them in one step by calling putProperties on the revision
     * or document.
     * @param body  The new body, or nil to delete the attachment.
     * @param contentType  The new content type, or nil to leave it the same.
     */
    public CBLRevision update(InputStream body, String contentType) throws CBLiteException {
        CBLDatabase db = revision.getDatabase();
        CBLRevisionInternal newRevisionInternal = db.updateAttachment(name, body, contentType, revision.getDocument().getId(), revision.getId());
        return new CBLRevision(document, newRevisionInternal);
    }

    /**
     * Goes through an _attachments dictionary and replaces any values that are CBLAttachment objects
     * with proper JSON metadata dicts. It registers the attachment bodies with the blob store and sets
     * the metadata 'digest' and 'follows' properties accordingly.
     */
    static Map<String, Object> installAttachmentBodies(Map<String, Object> attachments, CBLDatabase database) {

        Map<String, Object> updatedAttachments = new HashMap<String, Object>();
        for (String name : attachments.keySet()) {
            Object value = attachments.get(name);
            if (value instanceof CBLAttachment) {
                CBLAttachment attachment = (CBLAttachment) value;
                Map<String, Object> metadata = attachment.getMetadata();
                InputStream body = attachment.getBodyIfNew();
                if (body != null) {
                    // Copy attachment body into the database's blob store:
                    CBLBlobStoreWriter writer = blobStoreWriterForBody(body, database);
                    metadata.put("length", writer.getLength());
                    metadata.put("digest", writer.mD5DigestString());
                    metadata.put("follows", true);
                    database.rememberAttachmentWriter(writer);
                }
                updatedAttachments.put(name, metadata);
            }
            else if (value instanceof CBLAttachmentInternal) {
                throw new IllegalArgumentException("CBLAttachmentInternal objects not expected here.  Could indicate a bug");
            }
        }
        return updatedAttachments;
    }

    static CBLBlobStoreWriter blobStoreWriterForBody(InputStream body, CBLDatabase database) {
        CBLBlobStoreWriter writer = database.getAttachmentWriter();
        writer.read(body);
        writer.finish();
        return writer;

    }

    public boolean getGZipped() {
        return gzipped;
    }

    public void setGZipped(boolean gzipped) {
        this.gzipped = gzipped;
    }


}
