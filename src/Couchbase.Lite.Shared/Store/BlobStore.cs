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
using Couchbase.Lite;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
    /// <summary>A persistent content-addressable store for arbitrary-size data blobs.</summary>
    /// <remarks>
    /// A persistent content-addressable store for arbitrary-size data blobs.
    /// Each blob is stored as a file named by its SHA-1 digest.
    /// </remarks>
    internal class BlobStore
    {
        public static string FileExtension = ".blob";

        public static string TmpFileExtension = ".blobtmp";

        public static string TmpFilePrefix = "tmp";

        private readonly string path;

        public BlobStore(string path)
        {
            this.path = path;
            FilePath directory = new FilePath(path);
            directory.Mkdirs();
            if (!directory.IsDirectory())
            {
                throw new InvalidOperationException(string.Format("Unable to create directory for: {0}", directory));
            }
        }

        public static BlobKey KeyForBlob(byte[] data)
        {
            MessageDigest md;
            try
            {
                md = MessageDigest.GetInstance("SHA-1");
            }
            catch (NoSuchAlgorithmException)
            {
                Log.E(Database.Tag, "Error, SHA-1 digest is unavailable.");
                return null;
            }
            byte[] sha1hash = new byte[40];
            md.Update(data, 0, data.Length);
            sha1hash = md.Digest();
            BlobKey result = new BlobKey(sha1hash);
            return result;
        }

        public static BlobKey KeyForBlobFromFile(FileInfo file)
        {
            MessageDigest md;
            try
            {
                md = MessageDigest.GetInstance("SHA-1");
            }
            catch (NoSuchAlgorithmException)
            {
                Log.E(Database.Tag, "Error, SHA-1 digest is unavailable.");
                return null;
            }
            byte[] sha1hash = new byte[40];
            try
            {
                var fis = new FileInputStream(file);
                byte[] buffer = new byte[65536];
                int lenRead = fis.Read(buffer);
                while (lenRead > 0)
                {
                    md.Update(buffer, 0, lenRead);
                    lenRead = fis.Read(buffer);
                }
                fis.Close();
            }
            catch (IOException)
            {
                Log.E(Database.Tag, "Error readin tmp file to compute key");
            }
            sha1hash = md.Digest();
            BlobKey result = new BlobKey(sha1hash);
            return result;
        }

        public string PathForKey(BlobKey key)
        {
            return path + FilePath.separator + BlobKey.ConvertToHex(key.GetBytes()) + FileExtension;
        }

        public long GetSizeOfBlob(BlobKey key)
        {
            string path = PathForKey(key);
            FilePath file = new FilePath(path);
            return file.Length();
        }

        public bool GetKeyForFilename(BlobKey outKey, string filename)
        {
            if (!filename.EndsWith(FileExtension))
            {
                return false;
            }
            //trim off extension
            string rest = Sharpen.Runtime.Substring(filename, path.Length + 1, filename.Length
                 - FileExtension.Length);
            outKey.SetBytes(BlobKey.ConvertFromHex(rest));
            return true;
        }

        public byte[] BlobForKey(BlobKey key)
        {
            string path = PathForKey(key);
            FilePath file = new FilePath(path);
            byte[] result = null;
            try
            {
                result = GetBytesFromFile(file);
            }
            catch (IOException e)
            {
                Log.E(Database.Tag, "Error reading file", e);
            }
            return result;
        }

        public Stream BlobStreamForKey(BlobKey key)
        {
            var path = PathForKey(key);
            Log.D(Database.Tag, "Blob Path : " + path);
            var file = new FilePath(path);
            if (file.CanRead())
            {
                try
                {
                    return new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                catch (FileNotFoundException e)
                {
                    Log.E(Database.Tag, "Unexpected file not found in blob store", e);
                    return null;
                }
                catch (Exception e)
                {
                    Log.E(Database.Tag, "Cannot new FileStream", e);
                }
            }
            return null;
        }

        public bool StoreBlobStream(Stream inputStream, out BlobKey outKey)
        {
            FilePath tmp = null;
            try
            {
                tmp = FilePath.CreateTempFile(TmpFilePrefix, TmpFileExtension, new FilePath(this.path));
                FileOutputStream fos = new FileOutputStream(tmp);
                byte[] buffer = new byte[65536];
                int lenRead = ((InputStream)inputStream).Read(buffer);
                while (lenRead > 0)
                {
                    fos.Write(buffer, 0, lenRead);
                    lenRead = ((InputStream)inputStream).Read(buffer);
                }
                inputStream.Close();
                fos.Close();
            }
            catch (IOException e)
            {
                Log.E(Database.Tag, "Error writing blog to tmp file", e);
                outKey = null;
                return false;
            }

            outKey = KeyForBlobFromFile(tmp);
            var keyPath = PathForKey(outKey);
            var file = new FilePath(keyPath);
            if (file.CanRead())
            {
                // object with this hash already exists, we should delete tmp file and return true
                tmp.Delete();
            }
            else
            {
                // does not exist, we should rename tmp file to this name
                tmp.RenameTo(file);
            }

            return true;
        }

        public bool StoreBlob(byte[] data, BlobKey outKey)
        {
            BlobKey newKey = KeyForBlob(data);
            outKey.SetBytes(newKey.GetBytes());
            string path = PathForKey(outKey);
            FilePath file = new FilePath(path);
            if (file.CanRead())
            {
                return true;
            }
            FileOutputStream fos = null;
            try
            {
                fos = new FileOutputStream(file);
                fos.Write(data);
            }
            catch (FileNotFoundException e)
            {
                Log.E(Database.Tag, "Error opening file for output", e);
                return false;
            }
            catch (IOException ioe)
            {
                Log.E(Database.Tag, "Error writing to file", ioe);
                return false;
            }
            finally
            {
                if (fos != null)
                {
                    try
                    {
                        fos.Close();
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            // ignore
            return true;
        }

        /// <exception cref="System.IO.IOException"></exception>
        private static byte[] GetBytesFromFile(FilePath file)
        {
            InputStream @is = new FileInputStream(file);
            // Get the size of the file
            long length = file.Length();
            // Create the byte array to hold the data
            byte[] bytes = new byte[(int)length];
            // Read in the bytes
            int offset = 0;
            int numRead = 0;
            while (offset < bytes.Length && (numRead = @is.Read(bytes, offset, bytes.Length -
                 offset)) >= 0)
            {
                offset += numRead;
            }
            // Ensure all the bytes have been read in
            if (offset < bytes.Length)
            {
                throw new IOException("Could not completely read file " + file.GetName());
            }
            // Close the input stream and return bytes
            @is.Close();
            return bytes;
        }

        public ICollection<BlobKey> AllKeys()
        {
            ICollection<BlobKey> result = new HashSet<BlobKey>();
            FilePath file = new FilePath(path);
            FilePath[] contents = file.ListFiles();
            foreach (FilePath attachment in contents)
            {
                if (attachment.IsDirectory())
                {
                    continue;
                }
                BlobKey attachmentKey = new BlobKey();
                GetKeyForFilename(attachmentKey, attachment.GetPath());
                result.AddItem(attachmentKey);
            }
            return result;
        }

        public int Count()
        {
            FilePath file = new FilePath(path);
            FilePath[] contents = file.ListFiles();
            return contents.Length;
        }

        public long TotalDataSize()
        {
            long total = 0;
            FilePath file = new FilePath(path);
            FilePath[] contents = file.ListFiles();
            foreach (FilePath attachment in contents)
            {
                total += attachment.Length();
            }
            return total;
        }

        public int DeleteBlobsExceptWithKeys(IList<BlobKey> keysToKeep)
        {
            int numDeleted = 0;
            FilePath file = new FilePath(path);
            FilePath[] contents = file.ListFiles();
            foreach (FilePath attachment in contents)
            {
                BlobKey attachmentKey = new BlobKey();
                GetKeyForFilename(attachmentKey, attachment.GetPath());
                if (!keysToKeep.Contains(attachmentKey))
                {
                    bool result = attachment.Delete();
                    if (result)
                    {
                        ++numDeleted;
                    }
                    else
                    {
                        Log.E(Database.Tag, "Error deleting attachment");
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
            var file = new FilePath(path);
            if (file.CanRead())
            {
                try
                {
                    var raf = new RandomAccessFile(file, "r");
                    magic = raf.Read() & unchecked((0xff)) | ((raf.Read() << 8) & unchecked((0xff00)));
                    raf.Close();
                }
                catch (Exception e)
                {
                    Runtime.PrintStackTrace(e, Console.Error);
                }
            }
            return magic == GZIPInputStream.GzipMagic;
        }

        public FileInfo TempDir()
        {
            FilePath directory = new FilePath(path);
            FilePath tempDirectory = new FilePath(directory, "temp_attachments");
            tempDirectory.Mkdirs();
            if (!tempDirectory.IsDirectory())
            {
                throw new InvalidOperationException(string.Format("Unable to create directory for: {0}"
                    , tempDirectory));
            }
            return tempDirectory;
        }
    }
}
