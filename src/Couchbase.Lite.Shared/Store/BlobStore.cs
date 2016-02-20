//
// BlobStore.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.IO;

using Couchbase.Lite.Store;
using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    /// <summary>A persistent content-addressable store for arbitrary-size data blobs.</summary>
    /// <remarks>
    /// A persistent content-addressable store for arbitrary-size data blobs.
    /// Each blob is stored as a file named by its SHA-1 digest.
    /// </remarks>
    internal class BlobStore
    {
        private const string ENCRYPTION_MARKER_FILENAME = "_encryption";
        private const string ENCRYPTION_ALGORITHM = "AES";
        private const string TAG = "BlobStore";

        public static string FileExtension = ".blob";

        public static string TmpFileExtension = ".blobtmp";

        public static string TmpFilePrefix = "tmp";

        private readonly string _path;

        public SymmetricKey EncryptionKey { get; private set; }

        public BlobStore(string path, SymmetricKey encryptionKey)
        {
            if (path == null) {
                throw new ArgumentNullException("path");
            }

            _path = path;
            EncryptionKey = encryptionKey;
            if (Directory.Exists(path)) {
                // Existing blob-store.
                VerifyExistingStore();
            } else {
                // New blob store; create directory:
                Directory.CreateDirectory(path);
                if (!Directory.Exists(path)) {
                    throw new InvalidOperationException(string.Format("Unable to create directory for: {0}", path));
                }

                if (encryptionKey != null) {
                    MarkEncrypted(true);
                }
            }
        }

        public static BlobKey KeyForBlob(byte[] data)
        {
            MessageDigest md;
            try {
                md = MessageDigest.GetInstance("SHA-1");
            } catch (NotSupportedException) {
                Log.E(Database.TAG, "Error, SHA-1 digest is unavailable.");
                return null;
            }

            byte[] sha1hash = new byte[40];
            md.Update(data, 0, data.Length);
            sha1hash = md.Digest();
            BlobKey result = new BlobKey(sha1hash);
            return result;
        }

        public static BlobKey KeyForBlobFromFile(string file)
        {
            MessageDigest md;
            try {
                md = MessageDigest.GetInstance("SHA-1");
            } catch (NotSupportedException) {
                Log.E(Database.TAG, "Error, SHA-1 digest is unavailable.");
                return null;
            }

            byte[] sha1hash = new byte[40];
            try {
                var fis = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] buffer = new byte[65536];
                int lenRead = fis.Read(buffer, 0, buffer.Length);
                while (lenRead > 0)
                {
                    md.Update(buffer, 0, lenRead);
                    lenRead = fis.Read(buffer, 0, buffer.Length);
                }
                fis.Close();
            } catch (IOException) {
                Log.E(Database.TAG, "Error readin tmp file to compute key");
            }

            sha1hash = md.Digest();
            BlobKey result = new BlobKey(sha1hash);
            return result;
        }

        public bool HasBlobForKey(BlobKey key)
        {
            return File.Exists(PathForKey(key));
        }

        public string PathForKey(BlobKey key)
        {
            return _path + Path.DirectorySeparatorChar + key + FileExtension;
        }

        public long GetSizeOfBlob(BlobKey key)
        {
            string path = PathForKey(key);
            var info = new FileInfo(path);
            return info.Exists ? info.Length : 0;
        }

        public bool GetKeyForFilename(BlobKey outKey, string filename)
        {
            if (!filename.EndsWith(FileExtension)) {
                return false;
            }

            //trim off extension
            string rest = filename.Substring(_path.Length + 1, filename.Length - FileExtension.Length - (_path.Length + 1));
            outKey.Bytes = BlobKey.ConvertFromHex(rest);
            return true;
        }

        public byte[] BlobForKey(BlobKey key)
        {
            using (var blobStream = BlobStreamForKey(key)) {
                if (blobStream == null) {
                    return null;
                }

                return blobStream.ReadAllBytes();
            }
        }

        public Stream BlobStreamForKey(BlobKey key)
        {
            if (key == null) {
                return null;
            }

            string keyPath = PathForKey(key);
            var fileStream = default(Stream);
            try {
                fileStream = File.Open(keyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if(EncryptionKey != null) {
                    fileStream = EncryptionKey.DecryptStream(fileStream);
                }
            } catch (IOException e) {
                Log.E(Database.TAG, "Error reading file", e);
            }

            return fileStream;
        }

        public bool StoreBlobStream(Stream inputStream, out BlobKey outKey)
        {
            var tmp = default(string);
            try {
                tmp = Path.Combine(_path, Guid.NewGuid().ToString());
                var fos = File.Open(tmp, FileMode.Create);
                byte[] buffer = new byte[65536];
                int lenRead = inputStream.Read(buffer, 0, buffer.Length);
                while (lenRead > 0)  {
                    fos.Write(buffer, 0, lenRead);
                    lenRead = inputStream.Read(buffer, 0, buffer.Length);
                }

                inputStream.Close();
                fos.Close();
            } catch (IOException e) {
                Log.E(Database.TAG, "Error writing blog to tmp file", e);
                outKey = null;
                return false;
            }

            outKey = KeyForBlobFromFile(tmp);
            var keyPath = PathForKey(outKey);
            if (File.Exists(keyPath)) {
                // object with this hash already exists, we should delete tmp file and return true
                File.Delete(tmp);
            } else {
                // does not exist, we should rename tmp file to this name
                File.Move(tmp, keyPath);
            }

            return true;
        }

        public bool StoreBlob(byte[] data, BlobKey outKey)
        {
            BlobKey newKey = KeyForBlob(data);
            outKey.Bytes = newKey.Bytes;
            string keyPath = PathForKey(outKey);
            if (File.Exists(keyPath) && ((File.GetAttributes (keyPath) & FileAttributes.Offline) == 0)) {
                return true;
            }

            var fos = default(FileStream);
            try {
                fos = File.Open(keyPath, FileMode.Create);
                fos.Write(data, 0, data.Length);
            } catch (FileNotFoundException e) {
                Log.E(Database.TAG, "Error opening file for output", e);
                return false;
            }  catch (IOException ioe) {
                Log.E(Database.TAG, "Error writing to file", ioe);
                return false;
            } finally {
                if (fos != null) {
                    try {
                        fos.Close();
                    }
                    catch (IOException) {
                    }
                }
            }
            // ignore
            return true;
        }

        public ICollection<BlobKey> AllKeys()
        {
            ICollection<BlobKey> result = new HashSet<BlobKey>();;
            foreach (var attachment in Directory.GetFileSystemEntries(_path)) {
                if (File.GetAttributes(attachment).HasFlag(FileAttributes.Directory)) {
                    continue;
                }

                BlobKey attachmentKey = new BlobKey();
                GetKeyForFilename(attachmentKey, attachment);
                result.Add(attachmentKey);
            }

            return result;
        }

        public int Count()
        {
            return Directory.GetFiles(_path).Length;
        }

        public long TotalDataSize()
        {
            long total = 0;
            var info = new DirectoryInfo(_path);
            foreach (var attachment in info.GetFiles()) {
                total += attachment.Length;
            }

            return total;
        }

        public int DeleteBlobsExceptWithKeys(ICollection<BlobKey> keysToKeep)
        {
            int numDeleted = 0;
            foreach (var attachment in Directory.GetFiles(_path)) {
                BlobKey attachmentKey = new BlobKey();
                if (GetKeyForFilename(attachmentKey, attachment) && !keysToKeep.Contains(attachmentKey)) {
                    try {
                        File.Delete(attachment);
                        ++numDeleted;
                    } catch(Exception e) {
                        Log.E(Database.TAG, "Error deleting attachment");
                    }
                }
            }

            return numDeleted;
        }

        public int DeleteBlobs()
        {
            return DeleteBlobsExceptWithKeys(new List<BlobKey>());
        }

        public bool IsGZipped(BlobKey key)
        {
            var magic = 0;
            var path = PathForKey(key);
            if (File.Exists(path) && ((File.GetAttributes (path) & FileAttributes.Offline) == 0)) {
                try {
                    var raf = new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    magic = raf.ReadByte() & unchecked((0xff)) | ((raf.ReadByte() << 8) & unchecked((0xff00)));
                    raf.Close();
                }
                catch (Exception e) {
                    Log.E(TAG, "Error in IsGZipped", e);
                }
            }

            return magic == 0;
        }

        public string TempDir()
        {
            var path = Path.Combine(_path, "temp_attachments");
            try {
                Directory.CreateDirectory(path);
            } catch(Exception e) {
                throw new CouchbaseLiteException(String.Format("Unable to create directory for: {0}", path), e);
            }

            if (!Directory.Exists(path)) {
                throw new CouchbaseLiteException("Unable to create directory for: {0}", path);
            }
            return path;
        }

        public AtomicAction ActionToChangeEncryptionKey(SymmetricKey newKey)
        {
            var action = new AtomicAction();

            // Find all the blob files:
            var blobs = default(string[]);
            var oldKey = EncryptionKey;
            blobs = Directory.GetFiles(_path, "*" + FileExtension);
            if (blobs.Length == 0) {
                // No blobs, so nothing to encrypt. Just add/remove the encryption marker file:
                action.AddLogic(() =>
                {
                    Log.D(TAG, "{0} {1}", (newKey != null) ? "encrypting" : "decrypting", _path);
                    Log.D(TAG, "    No blobs to copy; done.");
                    EncryptionKey = newKey;
                    MarkEncrypted(newKey != null);
                }, () =>
                {
                    EncryptionKey = oldKey;
                    MarkEncrypted(oldKey != null);
                }, null);
                return action;
            }

            // Create a new directory for the new blob store. Have to do this now, before starting the
            // action, because farther down we create an action to move it...
            var tempPath = Path.Combine(Path.GetTempPath(), String.Format("CouchbaseLite-Temp-{0}", Misc.CreateGUID()));
            action.AddLogic(() => 
            {
                Log.D(TAG, "{0} {1}", (newKey != null) ? "encrypting" : "decrypting", _path);
                Directory.CreateDirectory(tempPath);
            }, () => Directory.Delete(tempPath, true), null);

            var tempStore = default(BlobStore);
            action.AddLogic(() =>
            {
                tempStore = new BlobStore(tempPath, newKey);
                tempStore.MarkEncrypted(true);
            }, null, null);

            // Copy each of my blobs into the new store (which will update its encryption):
            action.AddLogic(() =>
            {
                foreach(var blobName in blobs) {
                    // Copy file by reading with old key and writing with new one:
                    Log.D(TAG, "    Copying {0}", blobName);
                    Stream readStream = File.Open(blobName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if(EncryptionKey != null) {
                        readStream = EncryptionKey.DecryptStream(readStream);
                    }

                    var writer = new BlobStoreWriter(tempStore);
                    try {
                        writer.Read(readStream);
                        writer.Finish();
                        writer.Install();
                    } catch(Exception) {
                        writer.Cancel();
                        throw;
                    } finally {
                        readStream.Dispose();
                    }
                }
            }, null, null);

            // Replace the attachment dir with the new one:
            action.AddLogic(AtomicAction.MoveDirectory(tempPath, _path));

            // Finally update EncryptionKey:
            action.AddLogic(() =>
            {
                EncryptionKey = newKey;
            }, () =>
            {
                EncryptionKey = oldKey;
            }, null);

            return action;
        }

        public void ChangeEncryptionKey(SymmetricKey newKey)
        {
            ActionToChangeEncryptionKey(newKey).Run();
        }

        internal void MarkEncrypted(bool encrypted)
        {
            var encMarkerPath = Path.Combine(_path, ENCRYPTION_MARKER_FILENAME);
            if (encrypted) {
                try {
                    File.WriteAllText(encMarkerPath, ENCRYPTION_ALGORITHM);
                } catch(Exception e) {
                    throw new CouchbaseLiteException("Error enabling attachment encryption", e) { Code = StatusCode.Exception };
                }
            } else {
                try {
                    File.Delete(encMarkerPath);
                } catch(Exception e) {
                    throw new CouchbaseLiteException("Error disabling attachment encryption", e) { Code = StatusCode.Exception };
                }
            }
        }

        private void VerifyExistingStore()
        {
            var markerPath = Path.Combine(_path, ENCRYPTION_MARKER_FILENAME);
            var fileExists = File.Exists(markerPath);
            var encryptionAlg = default(string);
            try {
                encryptionAlg = fileExists ? File.ReadAllText(markerPath) : null;
            } catch(Exception e) {
                throw new CouchbaseLiteException("Error verifying BlobStore", e) { Code = StatusCode.Exception };
            }

            if (encryptionAlg != null) {
                // "_encryption" file is present, so make sure we support its format & have a key:
                if (EncryptionKey == null) {
                    throw new CouchbaseLiteException("Opening encrypted blob-store without providing a key", StatusCode.Unauthorized);
                } else if (ENCRYPTION_ALGORITHM != encryptionAlg) {
                    throw new CouchbaseLiteException("Blob-store uses unrecognized encryption '{0}'", encryptionAlg) {
                        Code = StatusCode.Unauthorized
                    };
                }
            } else if (!fileExists) {
                // No "_encryption" file was found, so on-disk store isn't encrypted:
                var encryptionKey = EncryptionKey;
                if (encryptionKey != null) {
                    // This store was created before the db encryption fix, so its files are not
                    // encrypted, even though they should be. Remedy that:
                    Log.I(TAG, "**** BlobStore should be encrypted; fixing it now...");
                    EncryptionKey = null;
                    ChangeEncryptionKey(encryptionKey);
                }
            }
        }


    }
}
