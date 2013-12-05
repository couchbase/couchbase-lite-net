package com.couchbase.lite.internal;

import com.couchbase.lite.CBLBlobKey;

/**
 *  A simple container for attachment metadata.
 */
public class AttachmentInternal {

    public enum CBLAttachmentEncoding {
        CBLAttachmentEncodingNone, CBLAttachmentEncodingGZIP
    }

    private String name;
    private String contentType;

    private CBLBlobKey blobKey;
    private long length;
    private long encodedLength;
    private CBLAttachmentEncoding encoding;
    private int revpos;

    public AttachmentInternal(String name, String contentType) {
        this.name = name;
        this.contentType = contentType;
    }

    public boolean isValid() {
        if (encoding != CBLAttachmentEncoding.CBLAttachmentEncodingNone) {
            if (encodedLength == 0 && length > 0) {
                return false;
            }
        }
        else if (encodedLength > 0) {
            return false;
        }
        if (revpos == 0) {
            return false;
        }
        return true;
    }

    public String getName() {
        return name;
    }

    public String getContentType() {
        return contentType;
    }

    public CBLBlobKey getBlobKey() {
        return blobKey;
    }

    public void setBlobKey(CBLBlobKey blobKey) {
        this.blobKey = blobKey;
    }

    public long getLength() {
        return length;
    }

    public void setLength(long length) {
        this.length = length;
    }

    public long getEncodedLength() {
        return encodedLength;
    }

    public void setEncodedLength(long encodedLength) {
        this.encodedLength = encodedLength;
    }

    public CBLAttachmentEncoding getEncoding() {
        return encoding;
    }

    public void setEncoding(CBLAttachmentEncoding encoding) {
        this.encoding = encoding;
    }

    public int getRevpos() {
        return revpos;
    }

    public void setRevpos(int revpos) {
        this.revpos = revpos;
    }


}

