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

import java.io.File;
import java.io.FileInputStream;
import java.io.FileNotFoundException;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.RandomAccessFile;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.zip.GZIPInputStream;

import com.couchbase.lite.util.Log;

/**
 * A persistent content-addressable store for arbitrary-size data blobs.
 * Each blob is stored as a file named by its SHA-1 digest.
 */
public class CBLBlobStore {

    public static String FILE_EXTENSION = ".blob";
    public static String TMP_FILE_EXTENSION = ".blobtmp";
    public static String TMP_FILE_PREFIX = "tmp";

    private String path;

    public CBLBlobStore(String path) {
        this.path = path;
        File directory = new File(path);
        if(!directory.exists()) {
            boolean result = directory.mkdirs();
            if(result == false) {
                throw new IllegalArgumentException("Unable to create directory for blob store");
            }
        }
        else if(!directory.isDirectory()) {
            throw new IllegalArgumentException("Directory for blob store is not a directory");
        }
    }

    public static BlobKey keyForBlob(byte[] data) {
        MessageDigest md;
        try {
            md = MessageDigest.getInstance("SHA-1");
        } catch (NoSuchAlgorithmException e) {
            Log.e(Database.TAG, "Error, SHA-1 digest is unavailable.");
            return null;
        }
        byte[] sha1hash = new byte[40];
        md.update(data, 0, data.length);
        sha1hash = md.digest();
        BlobKey result = new BlobKey(sha1hash);
        return result;
    }

    public static BlobKey keyForBlobFromFile(File file) {
        MessageDigest md;
        try {
            md = MessageDigest.getInstance("SHA-1");
        } catch (NoSuchAlgorithmException e) {
            Log.e(Database.TAG, "Error, SHA-1 digest is unavailable.");
            return null;
        }
        byte[] sha1hash = new byte[40];

        try {
            FileInputStream fis = new FileInputStream(file);
            byte[] buffer = new byte[65536];
            int lenRead = fis.read(buffer);
            while(lenRead > 0) {
                md.update(buffer, 0, lenRead);
                lenRead = fis.read(buffer);
            }
            fis.close();
        } catch (IOException e) {
            Log.e(Database.TAG, "Error readin tmp file to compute key");
        }

        sha1hash = md.digest();
        BlobKey result = new BlobKey(sha1hash);
        return result;
    }

    public String pathForKey(BlobKey key) {
        return path + File.separator + BlobKey.convertToHex(key.getBytes()) + FILE_EXTENSION;
    }

    public long getSizeOfBlob(BlobKey key) {
        String path = pathForKey(key);
        File file = new File(path);
        return file.length();
    }

    public boolean getKeyForFilename(BlobKey outKey, String filename) {
        if(!filename.endsWith(FILE_EXTENSION)) {
            return false;
        }
        //trim off extension
        String rest = filename.substring(path.length() + 1, filename.length() - FILE_EXTENSION.length());

        outKey.setBytes(BlobKey.convertFromHex(rest));

        return true;
    }

    public byte[] blobForKey(BlobKey key) {
        String path = pathForKey(key);
        File file = new File(path);
        byte[] result = null;
        try {
            result = getBytesFromFile(file);
        } catch (IOException e) {
            Log.e(Database.TAG, "Error reading file", e);
        }
        return result;
    }

    public InputStream blobStreamForKey(BlobKey key) {
        String path = pathForKey(key);
        File file = new File(path);
        if(file.canRead()) {
            try {
                return new FileInputStream(file);
            } catch (FileNotFoundException e) {
                Log.e(Database.TAG, "Unexpected file not found in blob store", e);
                return null;
            }
        }
        return null;
    }

    public boolean storeBlobStream(InputStream inputStream, BlobKey outKey) {

        File tmp = null;
        try {
            tmp = File.createTempFile(TMP_FILE_PREFIX, TMP_FILE_EXTENSION, new File(path));
            FileOutputStream fos = new FileOutputStream(tmp);
            byte[] buffer = new byte[65536];
            int lenRead = inputStream.read(buffer);
            while(lenRead > 0) {
                fos.write(buffer, 0, lenRead);
                lenRead = inputStream.read(buffer);
            }
            inputStream.close();
            fos.close();
        } catch (IOException e) {
            Log.e(Database.TAG, "Error writing blog to tmp file", e);
            return false;
        }

        BlobKey newKey = keyForBlobFromFile(tmp);
        outKey.setBytes(newKey.getBytes());
        String path = pathForKey(outKey);
        File file = new File(path);

        if(file.canRead()) {
            // object with this hash already exists, we should delete tmp file and return true
            tmp.delete();
            return true;
        } else {
            // does not exist, we should rename tmp file to this name
            tmp.renameTo(file);
        }
        return true;
    }

    public boolean storeBlob(byte[] data, BlobKey outKey) {
        BlobKey newKey = keyForBlob(data);
        outKey.setBytes(newKey.getBytes());
        String path = pathForKey(outKey);
        File file = new File(path);
        if(file.canRead()) {
            return true;
        }

        FileOutputStream fos = null;
        try {
            fos = new FileOutputStream(file);
            fos.write(data);
        } catch (FileNotFoundException e) {
            Log.e(Database.TAG, "Error opening file for output", e);
            return false;
        } catch(IOException ioe) {
            Log.e(Database.TAG, "Error writing to file", ioe);
            return false;
        } finally {
            if(fos != null) {
                try {
                    fos.close();
                } catch (IOException e) {
                    // ignore
                }
            }
        }

        return true;
    }

    private static byte[] getBytesFromFile(File file) throws IOException {
        InputStream is = new FileInputStream(file);

        // Get the size of the file
        long length = file.length();

        // Create the byte array to hold the data
        byte[] bytes = new byte[(int)length];

        // Read in the bytes
        int offset = 0;
        int numRead = 0;
        while (offset < bytes.length
               && (numRead=is.read(bytes, offset, bytes.length-offset)) >= 0) {
            offset += numRead;
        }

        // Ensure all the bytes have been read in
        if (offset < bytes.length) {
            throw new IOException("Could not completely read file "+file.getName());
        }

        // Close the input stream and return bytes
        is.close();
        return bytes;
    }

    public Set<BlobKey> allKeys() {
        Set<BlobKey> result = new HashSet<BlobKey>();
        File file = new File(path);
        File[] contents = file.listFiles();
        for (File attachment : contents) {
            if (attachment.isDirectory()) {
                continue;
            }
            BlobKey attachmentKey = new BlobKey();
            getKeyForFilename(attachmentKey, attachment.getPath());
            result.add(attachmentKey);
        }
        return result;
    }

    public int count() {
        File file = new File(path);
        File[] contents = file.listFiles();
        return contents.length;
    }

    public long totalDataSize() {
        long total = 0;
        File file = new File(path);
        File[] contents = file.listFiles();
        for (File attachment : contents) {
            total += attachment.length();
        }
        return total;
    }

    public int deleteBlobsExceptWithKeys(List<BlobKey> keysToKeep) {
        int numDeleted = 0;
        File file = new File(path);
        File[] contents = file.listFiles();
        for (File attachment : contents) {
            BlobKey attachmentKey = new BlobKey();
            getKeyForFilename(attachmentKey, attachment.getPath());
            if(!keysToKeep.contains(attachmentKey)) {
                boolean result = attachment.delete();
                if(result) {
                    ++numDeleted;
                }
                else {
                    Log.e(Database.TAG, "Error deleting attachmetn");
                }
            }
        }
        return numDeleted;
    }

    public int deleteBlobs() {
        return deleteBlobsExceptWithKeys(new ArrayList<BlobKey>());
    }
    
    public boolean isGZipped(BlobKey key) {
        int magic = 0;
        String path = pathForKey(key);
        File file = new File(path);
        if (file.canRead()) {
            try {
                RandomAccessFile raf = new RandomAccessFile(file, "r");
                magic = raf.read() & 0xff | ((raf.read() << 8) & 0xff00);
                raf.close();
            } catch (Throwable e) {
                e.printStackTrace(System.err);
            }
        }
        return magic == GZIPInputStream.GZIP_MAGIC;
    }

    public File tempDir() {

        File directory = new File(path);
        File tempDirectory = new File(directory, "temp_attachments");

        if(!tempDirectory.exists()) {
            boolean result = tempDirectory.mkdirs();
            if(result == false) {
                throw new IllegalStateException("Unable to create directory for temporary blob store");
            }
        }
        else if(!tempDirectory.isDirectory()) {
            throw new IllegalStateException("Directory for temporary blob store is not a directory");
        }
        return tempDirectory;




    }

}
