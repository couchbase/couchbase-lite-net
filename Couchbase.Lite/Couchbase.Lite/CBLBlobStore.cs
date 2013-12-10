/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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

using System;
using System.Collections.Generic;
using System.IO;
using Couchbase;
using Couchbase.Util;
using Sharpen;

namespace Couchbase
{
	/// <summary>A persistent content-addressable store for arbitrary-size data blobs.</summary>
	/// <remarks>
	/// A persistent content-addressable store for arbitrary-size data blobs.
	/// Each blob is stored as a file named by its SHA-1 digest.
	/// </remarks>
	public class CBLBlobStore
	{
		public static string FileExtension = ".blob";

		public static string TmpFileExtension = ".blobtmp";

		public static string TmpFilePrefix = "tmp";

		private string path;

		public CBLBlobStore(string path)
		{
			this.path = path;
			FilePath directory = new FilePath(path);
			if (!directory.Exists())
			{
				bool result = directory.Mkdirs();
				if (result == false)
				{
					throw new ArgumentException("Unable to create directory for blob store");
				}
			}
			else
			{
				if (!directory.IsDirectory())
				{
					throw new ArgumentException("Directory for blob store is not a directory");
				}
			}
		}

		public static CBLBlobKey KeyForBlob(byte[] data)
		{
			MessageDigest md;
			try
			{
				md = MessageDigest.GetInstance("SHA-1");
			}
			catch (NoSuchAlgorithmException)
			{
				Log.E(CBLDatabase.Tag, "Error, SHA-1 digest is unavailable.");
				return null;
			}
			byte[] sha1hash = new byte[40];
			md.Update(data, 0, data.Length);
			sha1hash = md.Digest();
			CBLBlobKey result = new CBLBlobKey(sha1hash);
			return result;
		}

		public static CBLBlobKey KeyForBlobFromFile(FilePath file)
		{
			MessageDigest md;
			try
			{
				md = MessageDigest.GetInstance("SHA-1");
			}
			catch (NoSuchAlgorithmException)
			{
				Log.E(CBLDatabase.Tag, "Error, SHA-1 digest is unavailable.");
				return null;
			}
			byte[] sha1hash = new byte[40];
			try
			{
				FileInputStream fis = new FileInputStream(file);
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
				Log.E(CBLDatabase.Tag, "Error readin tmp file to compute key");
			}
			sha1hash = md.Digest();
			CBLBlobKey result = new CBLBlobKey(sha1hash);
			return result;
		}

		public virtual string PathForKey(CBLBlobKey key)
		{
			return path + FilePath.separator + CBLBlobKey.ConvertToHex(key.GetBytes()) + FileExtension;
		}

		public virtual long GetSizeOfBlob(CBLBlobKey key)
		{
			string path = PathForKey(key);
			FilePath file = new FilePath(path);
			return file.Length();
		}

		public virtual bool GetKeyForFilename(CBLBlobKey outKey, string filename)
		{
			if (!filename.EndsWith(FileExtension))
			{
				return false;
			}
			//trim off extension
			string rest = Sharpen.Runtime.Substring(filename, path.Length + 1, filename.Length
				 - FileExtension.Length);
			outKey.SetBytes(CBLBlobKey.ConvertFromHex(rest));
			return true;
		}

		public virtual byte[] BlobForKey(CBLBlobKey key)
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
				Log.E(CBLDatabase.Tag, "Error reading file", e);
			}
			return result;
		}

		public virtual InputStream BlobStreamForKey(CBLBlobKey key)
		{
			string path = PathForKey(key);
			FilePath file = new FilePath(path);
			if (file.CanRead())
			{
				try
				{
					return new FileInputStream(file);
				}
				catch (FileNotFoundException e)
				{
					Log.E(CBLDatabase.Tag, "Unexpected file not found in blob store", e);
					return null;
				}
			}
			return null;
		}

		public virtual bool StoreBlobStream(InputStream inputStream, CBLBlobKey outKey)
		{
			FilePath tmp = null;
			try
			{
				tmp = FilePath.CreateTempFile(TmpFilePrefix, TmpFileExtension, new FilePath(path)
					);
				FileOutputStream fos = new FileOutputStream(tmp);
				byte[] buffer = new byte[65536];
				int lenRead = inputStream.Read(buffer);
				while (lenRead > 0)
				{
					fos.Write(buffer, 0, lenRead);
					lenRead = inputStream.Read(buffer);
				}
				inputStream.Close();
				fos.Close();
			}
			catch (IOException e)
			{
				Log.E(CBLDatabase.Tag, "Error writing blog to tmp file", e);
				return false;
			}
			CBLBlobKey newKey = KeyForBlobFromFile(tmp);
			outKey.SetBytes(newKey.GetBytes());
			string path = PathForKey(outKey);
			FilePath file = new FilePath(path);
			if (file.CanRead())
			{
				// object with this hash already exists, we should delete tmp file and return true
				tmp.Delete();
				return true;
			}
			else
			{
				// does not exist, we should rename tmp file to this name
				tmp.RenameTo(file);
			}
			return true;
		}

		public virtual bool StoreBlob(byte[] data, CBLBlobKey outKey)
		{
			CBLBlobKey newKey = KeyForBlob(data);
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
				Log.E(CBLDatabase.Tag, "Error opening file for output", e);
				return false;
			}
			catch (IOException ioe)
			{
				Log.E(CBLDatabase.Tag, "Error writing to file", ioe);
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

		public virtual ICollection<CBLBlobKey> AllKeys()
		{
			ICollection<CBLBlobKey> result = new HashSet<CBLBlobKey>();
			FilePath file = new FilePath(path);
			FilePath[] contents = file.ListFiles();
			foreach (FilePath attachment in contents)
			{
				if (attachment.IsDirectory())
				{
					continue;
				}
				CBLBlobKey attachmentKey = new CBLBlobKey();
				GetKeyForFilename(attachmentKey, attachment.GetPath());
				result.AddItem(attachmentKey);
			}
			return result;
		}

		public virtual int Count()
		{
			FilePath file = new FilePath(path);
			FilePath[] contents = file.ListFiles();
			return contents.Length;
		}

		public virtual long TotalDataSize()
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

		public virtual int DeleteBlobsExceptWithKeys(IList<CBLBlobKey> keysToKeep)
		{
			int numDeleted = 0;
			FilePath file = new FilePath(path);
			FilePath[] contents = file.ListFiles();
			foreach (FilePath attachment in contents)
			{
				CBLBlobKey attachmentKey = new CBLBlobKey();
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
						Log.E(CBLDatabase.Tag, "Error deleting attachmetn");
					}
				}
			}
			return numDeleted;
		}

		public virtual int DeleteBlobs()
		{
			return DeleteBlobsExceptWithKeys(new AList<CBLBlobKey>());
		}

		public virtual bool IsGZipped(CBLBlobKey key)
		{
			int magic = 0;
			string path = PathForKey(key);
			FilePath file = new FilePath(path);
			if (file.CanRead())
			{
				try
				{
					RandomAccessFile raf = new RandomAccessFile(file, "r");
					magic = raf.Read() & unchecked((int)(0xff)) | ((raf.Read() << 8) & unchecked((int
						)(0xff00)));
					raf.Close();
				}
				catch (Exception e)
				{
					Sharpen.Runtime.PrintStackTrace(e, System.Console.Error);
				}
			}
			return magic == GZIPInputStream.GzipMagic;
		}

		public virtual FilePath TempDir()
		{
			FilePath directory = new FilePath(path);
			FilePath tempDirectory = new FilePath(directory, "temp_attachments");
			if (!tempDirectory.Exists())
			{
				bool result = tempDirectory.Mkdirs();
				if (result == false)
				{
					throw new InvalidOperationException("Unable to create directory for temporary blob store"
						);
				}
			}
			else
			{
				if (!tempDirectory.IsDirectory())
				{
					throw new InvalidOperationException("Directory for temporary blob store is not a directory"
						);
				}
			}
			return tempDirectory;
		}
	}
}
