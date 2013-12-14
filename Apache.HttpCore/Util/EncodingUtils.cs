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

using System.IO;
using Org.Apache.Http;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Util
{
	/// <summary>The home for utility methods that handle various encoding tasks.</summary>
	/// <remarks>The home for utility methods that handle various encoding tasks.</remarks>
	/// <since>4.0</since>
	public sealed class EncodingUtils
	{
		/// <summary>Converts the byte array of HTTP content characters to a string.</summary>
		/// <remarks>
		/// Converts the byte array of HTTP content characters to a string. If
		/// the specified charset is not supported, default system encoding
		/// is used.
		/// </remarks>
		/// <param name="data">the byte array to be encoded</param>
		/// <param name="offset">the index of the first byte to encode</param>
		/// <param name="length">the number of bytes to encode</param>
		/// <param name="charset">the desired character encoding</param>
		/// <returns>The result of the conversion.</returns>
		public static string GetString(byte[] data, int offset, int length, string charset
			)
		{
			Args.NotNull(data, "Input");
			Args.NotEmpty(charset, "Charset");
			try
			{
				return Sharpen.Runtime.GetStringForBytes(data, offset, length, charset);
			}
			catch (UnsupportedEncodingException)
			{
				return Sharpen.Runtime.GetStringForBytes(data, offset, length);
			}
		}

		/// <summary>Converts the byte array of HTTP content characters to a string.</summary>
		/// <remarks>
		/// Converts the byte array of HTTP content characters to a string. If
		/// the specified charset is not supported, default system encoding
		/// is used.
		/// </remarks>
		/// <param name="data">the byte array to be encoded</param>
		/// <param name="charset">the desired character encoding</param>
		/// <returns>The result of the conversion.</returns>
		public static string GetString(byte[] data, string charset)
		{
			Args.NotNull(data, "Input");
			return GetString(data, 0, data.Length, charset);
		}

		/// <summary>Converts the specified string to a byte array.</summary>
		/// <remarks>
		/// Converts the specified string to a byte array.  If the charset is not supported the
		/// default system charset is used.
		/// </remarks>
		/// <param name="data">the string to be encoded</param>
		/// <param name="charset">the desired character encoding</param>
		/// <returns>The resulting byte array.</returns>
		public static byte[] GetBytes(string data, string charset)
		{
			Args.NotNull(data, "Input");
			Args.NotEmpty(charset, "Charset");
			try
			{
				return Sharpen.Runtime.GetBytesForString(data, charset);
			}
			catch (UnsupportedEncodingException)
			{
				return Sharpen.Runtime.GetBytesForString(data);
			}
		}

		/// <summary>Converts the specified string to byte array of ASCII characters.</summary>
		/// <remarks>Converts the specified string to byte array of ASCII characters.</remarks>
		/// <param name="data">the string to be encoded</param>
		/// <returns>The string as a byte array.</returns>
		public static byte[] GetAsciiBytes(string data)
		{
			Args.NotNull(data, "Input");
			try
			{
				return Sharpen.Runtime.GetBytesForString(data, Consts.Ascii.Name());
			}
			catch (UnsupportedEncodingException)
			{
				throw new Error("ASCII not supported");
			}
		}

		/// <summary>Converts the byte array of ASCII characters to a string.</summary>
		/// <remarks>
		/// Converts the byte array of ASCII characters to a string. This method is
		/// to be used when decoding content of HTTP elements (such as response
		/// headers)
		/// </remarks>
		/// <param name="data">the byte array to be encoded</param>
		/// <param name="offset">the index of the first byte to encode</param>
		/// <param name="length">the number of bytes to encode</param>
		/// <returns>The string representation of the byte array</returns>
		public static string GetAsciiString(byte[] data, int offset, int length)
		{
			Args.NotNull(data, "Input");
			try
			{
				return Sharpen.Runtime.GetStringForBytes(data, offset, length, Consts.Ascii.Name(
					));
			}
			catch (UnsupportedEncodingException)
			{
				throw new Error("ASCII not supported");
			}
		}

		/// <summary>Converts the byte array of ASCII characters to a string.</summary>
		/// <remarks>
		/// Converts the byte array of ASCII characters to a string. This method is
		/// to be used when decoding content of HTTP elements (such as response
		/// headers)
		/// </remarks>
		/// <param name="data">the byte array to be encoded</param>
		/// <returns>The string representation of the byte array</returns>
		public static string GetAsciiString(byte[] data)
		{
			Args.NotNull(data, "Input");
			return GetAsciiString(data, 0, data.Length);
		}

		/// <summary>This class should not be instantiated.</summary>
		/// <remarks>This class should not be instantiated.</remarks>
		private EncodingUtils()
		{
		}
	}
}
