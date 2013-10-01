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

import com.couchbase.cblite.internal.CBLRevisionInternal;

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
    }


    /**
     * Package Private Constructor
     */
    CBLAttachment(CBLRevisionBase revision, String name, Map<String, Object> metadata) {
        this.revision = revision;
        this.name = name;
        this.metadata = metadata;
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
    public URL getBodyURL() {
        // TODO: implement
        throw new RuntimeException("Not implemented");
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


}
