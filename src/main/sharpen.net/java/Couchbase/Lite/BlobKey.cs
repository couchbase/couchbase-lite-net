/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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
using System.Text;
using Couchbase.Lite.Support;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>Key identifying a data blob.</summary>
	/// <remarks>Key identifying a data blob. This happens to be a SHA-1 digest.</remarks>
	public class BlobKey
	{
		private byte[] bytes;

		public BlobKey()
		{
		}

		public BlobKey(byte[] bytes)
		{
			this.bytes = bytes;
		}

		/// <summary>Constructor</summary>
		/// <param name="base64Digest">
		/// string with base64'd digest, with leading "sha1-" attached.
		/// eg, "sha1-LKJ32423JK..."
		/// </param>
		public BlobKey(string base64Digest) : this(DecodeBase64Digest(base64Digest))
		{
		}

		/// <summary>
		/// Decode base64'd digest into a byte array that is suitable for use
		/// as a blob key.
		/// </summary>
		/// <remarks>
		/// Decode base64'd digest into a byte array that is suitable for use
		/// as a blob key.
		/// </remarks>
		/// <param name="base64Digest">
		/// string with base64'd digest, with leading "sha1-" attached.
		/// eg, "sha1-LKJ32423JK..."
		/// </param>
		/// <returns>a byte[] blob key</returns>
		private static byte[] DecodeBase64Digest(string base64Digest)
		{
			string expectedPrefix = "sha1-";
			if (!base64Digest.StartsWith(expectedPrefix))
			{
				throw new ArgumentException(base64Digest + " did not start with " + expectedPrefix
					);
			}
			base64Digest = base64Digest.ReplaceFirst(expectedPrefix, string.Empty);
			byte[] bytes = new byte[0];
			try
			{
				bytes = Base64.Decode(base64Digest);
			}
			catch (IOException e)
			{
				new ArgumentException(e);
			}
			return bytes;
		}

		public virtual void SetBytes(byte[] bytes)
		{
			this.bytes = bytes;
		}

		public virtual byte[] GetBytes()
		{
			return bytes;
		}

		public static string ConvertToHex(byte[] data)
		{
			StringBuilder buf = new StringBuilder();
			for (int i = 0; i < data.Length; i++)
			{
				int halfbyte = (data[i] >> 4) & unchecked((int)(0x0F));
				int two_halfs = 0;
				do
				{
					if ((0 <= halfbyte) && (halfbyte <= 9))
					{
						buf.Append((char)('0' + halfbyte));
					}
					else
					{
						buf.Append((char)('a' + (halfbyte - 10)));
					}
					halfbyte = data[i] & unchecked((int)(0x0F));
				}
				while (two_halfs++ < 1);
			}
			return buf.ToString();
		}

		public static byte[] ConvertFromHex(string s)
		{
			int len = s.Length;
			byte[] data = new byte[len / 2];
			for (int i = 0; i < len; i += 2)
			{
				data[i / 2] = unchecked((byte)((char.Digit(s[i], 16) << 4) + char.Digit(s[i + 1], 
					16)));
			}
			return data;
		}

		public override bool Equals(object o)
		{
			if (!(o is Couchbase.Lite.BlobKey))
			{
				return false;
			}
			Couchbase.Lite.BlobKey oBlobKey = (Couchbase.Lite.BlobKey)o;
			return Arrays.Equals(GetBytes(), oBlobKey.GetBytes());
		}

		public override int GetHashCode()
		{
			return Arrays.HashCode(bytes);
		}

		public override string ToString()
		{
			return Couchbase.Lite.BlobKey.ConvertToHex(bytes);
		}

		public virtual string Base64Digest()
		{
			return string.Format("sha1-%s", Base64.EncodeBytes(bytes));
		}
	}
}
