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
using System.IO;
using Com.Couchbase.Lite;
using Com.Couchbase.Lite.Support;
using Com.Couchbase.Lite.Util;
using Sharpen;

namespace Com.Couchbase.Lite
{
	/// <summary>Lets you stream a large attachment to a BlobStore asynchronously, e.g.</summary>
	/// <remarks>Lets you stream a large attachment to a BlobStore asynchronously, e.g. from a network download.
	/// 	</remarks>
	public class BlobStoreWriter
	{
		/// <summary>The underlying blob store where it should be stored.</summary>
		/// <remarks>The underlying blob store where it should be stored.</remarks>
		private BlobStore store;

		/// <summary>The number of bytes in the blob.</summary>
		/// <remarks>The number of bytes in the blob.</remarks>
		private int length;

		/// <summary>After finishing, this is the key for looking up the blob through the CBL_BlobStore.
		/// 	</summary>
		/// <remarks>After finishing, this is the key for looking up the blob through the CBL_BlobStore.
		/// 	</remarks>
		private BlobKey blobKey;

		/// <summary>After finishing, store md5 digest result here</summary>
		private byte[] md5DigestResult;

		/// <summary>Message digest for sha1 that is updated as data is appended</summary>
		private MessageDigest sha1Digest;

		private MessageDigest md5Digest;

		private BufferedOutputStream outStream;

		private FilePath tempFile;

		public BlobStoreWriter(BlobStore store)
		{
			this.store = store;
			try
			{
				sha1Digest = MessageDigest.GetInstance("SHA-1");
				sha1Digest.Reset();
				md5Digest = MessageDigest.GetInstance("MD5");
				md5Digest.Reset();
			}
			catch (NoSuchAlgorithmException e)
			{
				throw new InvalidOperationException(e);
			}
			try
			{
				OpenTempFile();
			}
			catch (FileNotFoundException e)
			{
				throw new InvalidOperationException(e);
			}
		}

		/// <exception cref="System.IO.FileNotFoundException"></exception>
		private void OpenTempFile()
		{
			string uuid = Misc.TDCreateUUID();
			string filename = string.Format("%s.blobtmp", uuid);
			FilePath tempDir = store.TempDir();
			tempFile = new FilePath(tempDir, filename);
			outStream = new BufferedOutputStream(new FileOutputStream(tempFile));
		}

		/// <summary>Appends data to the blob.</summary>
		/// <remarks>Appends data to the blob. Call this when new data is available.</remarks>
		public virtual void AppendData(byte[] data)
		{
			try
			{
				outStream.Write(data);
			}
			catch (IOException e)
			{
				throw new RuntimeException("Unable to write to stream.", e);
			}
			length += data.Length;
			sha1Digest.Update(data);
			md5Digest.Update(data);
		}

		internal virtual void Read(InputStream inputStream)
		{
			byte[] buffer = new byte[1024];
			int len;
			length = 0;
			try
			{
				while ((len = inputStream.Read(buffer)) != -1)
				{
					outStream.Write(buffer, 0, len);
					sha1Digest.Update(buffer);
					md5Digest.Update(buffer);
					length += len;
				}
			}
			catch (IOException e)
			{
				throw new RuntimeException("Unable to read from stream.", e);
			}
			finally
			{
				try
				{
					inputStream.Close();
				}
				catch (IOException e)
				{
					Log.W(Database.Tag, "Exception closing input stream", e);
				}
			}
		}

		/// <summary>Call this after all the data has been added.</summary>
		/// <remarks>Call this after all the data has been added.</remarks>
		public virtual void Finish()
		{
			try
			{
				outStream.Close();
			}
			catch (IOException e)
			{
				Log.W(Database.Tag, "Exception closing output stream", e);
			}
			blobKey = new BlobKey(sha1Digest.Digest());
			md5DigestResult = md5Digest.Digest();
		}

		/// <summary>Call this to cancel before finishing the data.</summary>
		/// <remarks>Call this to cancel before finishing the data.</remarks>
		public virtual void Cancel()
		{
			try
			{
				outStream.Close();
			}
			catch (IOException e)
			{
				Log.W(Database.Tag, "Exception closing output stream", e);
			}
			tempFile.Delete();
		}

		/// <summary>Installs a finished blob into the store.</summary>
		/// <remarks>Installs a finished blob into the store.</remarks>
		public virtual void Install()
		{
			if (tempFile == null)
			{
				return;
			}
			// already installed
			// Move temp file to correct location in blob store:
			string destPath = store.PathForKey(blobKey);
			FilePath destPathFile = new FilePath(destPath);
			bool result = tempFile.RenameTo(destPathFile);
			// If the move fails, assume it means a file with the same name already exists; in that
			// case it must have the identical contents, so we're still OK.
			if (result == false)
			{
				Cancel();
			}
			tempFile = null;
		}

		public virtual string MD5DigestString()
		{
			string base64Md5Digest = Base64.EncodeBytes(md5DigestResult);
			return string.Format("md5-%s", base64Md5Digest);
		}

		public virtual string SHA1DigestString()
		{
			string base64Sha1Digest = Base64.EncodeBytes(blobKey.GetBytes());
			return string.Format("sha1-%s", base64Sha1Digest);
		}

		public virtual int GetLength()
		{
			return length;
		}

		public virtual BlobKey GetBlobKey()
		{
			return blobKey;
		}
	}
}
