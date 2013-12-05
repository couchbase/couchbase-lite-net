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

package com.couchbase.lite;

import com.couchbase.lite.internal.CBLAttachmentInternal;
import com.couchbase.lite.internal.InterfaceAudience;

import java.io.InputStream;
import java.util.Collections;
import java.util.HashMap;
import java.util.Map;

public class Attachment {

    /**
     * The owning document revision.
     */
    private Revision revision;

    /**
     * Whether or not this attachment is gzipped
     */
    private boolean gzipped;

    /**
     * The owning document.
     */
    private Document document;

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
     * Constructor
     */
    Attachment(InputStream contentStream, String contentType) {
        this.body = contentStream;
        metadata = new HashMap<String, Object>();
        metadata.put("content_type", contentType);
        metadata.put("follows", true);
        this.gzipped = false;
    }

    /**
     * Constructor
     */
    Attachment(Revision revision, String name, Map<String, Object> metadata) {
        this.revision = revision;
        this.name = name;
        this.metadata = metadata;
        this.gzipped = false;

    }

    /**
     * Get the owning document revision.
     */
    @InterfaceAudience.Public
    public Revision getRevision() {
        return revision;
    }


    /**
     * Get the owning document.
     */
    @InterfaceAudience.Public
    public Document getDocument() {
        return document;
    }

    /**
     * Get the filename.
     */
    @InterfaceAudience.Public
    public String getName() {
        return name;
    }

    /**
     * Get the MIME type of the contents.
     */
    @InterfaceAudience.Public
    public String getContentType() {
        return (String) metadata.get("content_type");
    }


    /**
     * Get the content (aka 'body') data.
     * @throws CouchbaseLiteException
     */
    @InterfaceAudience.Public
    public InputStream getContent() throws CouchbaseLiteException {
        if (body != null) {
            return body;
        }
        else {
            Database db = revision.getDatabase();
            Attachment attachment = db.getAttachmentForSequence(revision.getSequence(), this.name);
            body = attachment.getContent();
            return body;
        }
    }

    /**
     * Get the length in bytes of the contents.
     */
    @InterfaceAudience.Public
    public long getLength() {
        Long length = (Long) metadata.get("length");
        if (length != null) {
            return length.longValue();
        }
        else {
            return 0;
        }
    }

    /**
     * The CouchbaseLite metadata about the attachment, that lives in the document.
     */
    @InterfaceAudience.Public
    public Map<String, Object> getMetadata() {
        return Collections.unmodifiableMap(metadata);
    }

    void setName(String name) {
        this.name = name;
    }

    void setRevision(Revision revision) {
        this.revision = revision;
    }

    InputStream getBodyIfNew() {
        return body;
    }

    /**
     * Goes through an _attachments dictionary and replaces any values that are Attachment objects
     * with proper JSON metadata dicts. It registers the attachment bodies with the blob store and sets
     * the metadata 'digest' and 'follows' properties accordingly.
     */
    static Map<String, Object> installAttachmentBodies(Map<String, Object> attachments, Database database) {

        Map<String, Object> updatedAttachments = new HashMap<String, Object>();
        for (String name : attachments.keySet()) {
            Object value = attachments.get(name);
            if (value instanceof Attachment) {
                Attachment attachment = (Attachment) value;
                Map<String, Object> metadata = attachment.getMetadata();
                InputStream body = attachment.getBodyIfNew();
                if (body != null) {
                    // Copy attachment body into the database's blob store:
                    BlobStoreWriter writer = blobStoreWriterForBody(body, database);
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

    static BlobStoreWriter blobStoreWriterForBody(InputStream body, Database database) {
        BlobStoreWriter writer = database.getAttachmentWriter();
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
