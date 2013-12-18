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

using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using Sharpen;
using System;

namespace Couchbase.Lite
{
	public class Misc
	{
		public static string TDCreateUUID()
		{
            return Guid.NewGuid().ToString();
		}

		public static string TDHexSHA1Digest(byte[] input)
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
            byte[] sha1hash;
			md.Update(input, 0, input.Length);
			sha1hash = md.Digest();
			return ConvertToHex(sha1hash);
		}

		public static string ConvertToHex(byte[] data)
		{
			StringBuilder buf = new StringBuilder();
			for (int i = 0; i < data.Length; i++)
			{
				int halfbyte = (data[i] >> 4) & unchecked((0x0F));
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
					halfbyte = data[i] & unchecked((0x0F));
				}
				while (two_halfs++ < 1);
			}
			return buf.ToString();
		}

		public static int TDSequenceCompare(long a, long b)
		{
			long diff = a - b;
			return diff > 0 ? 1 : (diff < 0 ? -1 : 0);
		}

		public static string UnquoteString(string param)
		{
			return param.Replace("\"", string.Empty);
		}
	}
}
