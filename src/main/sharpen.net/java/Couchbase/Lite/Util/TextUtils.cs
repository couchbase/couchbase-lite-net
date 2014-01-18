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

using System.Collections;
using System.IO;
using System.Text;
using Apache.Http.Util;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Util
{
	public class TextUtils
	{
		// COPY: Partially copied from android.text.TextUtils
		/// <summary>Returns a string containing the tokens joined by delimiters.</summary>
		/// <remarks>Returns a string containing the tokens joined by delimiters.</remarks>
		/// <param name="tokens">
		/// an array objects to be joined. Strings will be formed from
		/// the objects by calling object.toString().
		/// </param>
		public static string Join(CharSequence delimiter, IEnumerable tokens)
		{
			StringBuilder sb = new StringBuilder();
			bool firstTime = true;
			foreach (object token in tokens)
			{
				if (firstTime)
				{
					firstTime = false;
				}
				else
				{
					sb.Append(delimiter);
				}
				sb.Append(token);
			}
			return sb.ToString();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static byte[] Read(InputStream @is)
		{
			int initialCapacity = 1024;
			ByteArrayBuffer byteArrayBuffer = new ByteArrayBuffer(initialCapacity);
			byte[] bytes = new byte[512];
			int offset = 0;
			int numRead = 0;
			while ((numRead = @is.Read(bytes, offset, bytes.Length - offset)) >= 0)
			{
				byteArrayBuffer.Append(bytes, 0, numRead);
				offset += numRead;
			}
			return byteArrayBuffer.ToByteArray();
		}
	}
}
