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
using Org.Apache.Http;
using Org.Apache.Http.Message;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>Basic implementation for formatting header value elements.</summary>
	/// <remarks>
	/// Basic implementation for formatting header value elements.
	/// Instances of this class are stateless and thread-safe.
	/// Derived classes are expected to maintain these properties.
	/// </remarks>
	/// <since>4.0</since>
	public class BasicHeaderValueFormatter : HeaderValueFormatter
	{
		/// <summary>A default instance of this class, for use as default or fallback.</summary>
		/// <remarks>
		/// A default instance of this class, for use as default or fallback.
		/// Note that
		/// <see cref="BasicHeaderValueFormatter">BasicHeaderValueFormatter</see>
		/// is not a singleton, there
		/// can be many instances of the class itself and of derived classes.
		/// The instance here provides non-customized, default behavior.
		/// </remarks>
		[System.ObsoleteAttribute(@"(4.3) use Instance")]
		[Obsolete]
		public static readonly Org.Apache.Http.Message.BasicHeaderValueFormatter Default = 
			new Org.Apache.Http.Message.BasicHeaderValueFormatter();

		public static readonly Org.Apache.Http.Message.BasicHeaderValueFormatter Instance
			 = new Org.Apache.Http.Message.BasicHeaderValueFormatter();

		/// <summary>Special characters that can be used as separators in HTTP parameters.</summary>
		/// <remarks>
		/// Special characters that can be used as separators in HTTP parameters.
		/// These special characters MUST be in a quoted string to be used within
		/// a parameter value .
		/// </remarks>
		public const string Separators = " ;,:@()<>\\\"/[]?={}\t";

		/// <summary>
		/// Unsafe special characters that must be escaped using the backslash
		/// character
		/// </summary>
		public const string UnsafeChars = "\"\\";

		public BasicHeaderValueFormatter() : base()
		{
		}

		/// <summary>Formats an array of header elements.</summary>
		/// <remarks>Formats an array of header elements.</remarks>
		/// <param name="elems">the header elements to format</param>
		/// <param name="quote">
		/// <code>true</code> to always format with quoted values,
		/// <code>false</code> to use quotes only when necessary
		/// </param>
		/// <param name="formatter">
		/// the formatter to use, or <code>null</code>
		/// for the
		/// <see cref="Instance">default</see>
		/// </param>
		/// <returns>the formatted header elements</returns>
		public static string FormatElements(HeaderElement[] elems, bool quote, HeaderValueFormatter
			 formatter)
		{
			return (formatter != null ? formatter : Org.Apache.Http.Message.BasicHeaderValueFormatter
				.Instance).FormatElements(null, elems, quote).ToString();
		}

		// non-javadoc, see interface HeaderValueFormatter
		public virtual CharArrayBuffer FormatElements(CharArrayBuffer charBuffer, HeaderElement
			[] elems, bool quote)
		{
			Args.NotNull(elems, "Header element array");
			int len = EstimateElementsLen(elems);
			CharArrayBuffer buffer = charBuffer;
			if (buffer == null)
			{
				buffer = new CharArrayBuffer(len);
			}
			else
			{
				buffer.EnsureCapacity(len);
			}
			for (int i = 0; i < elems.Length; i++)
			{
				if (i > 0)
				{
					buffer.Append(", ");
				}
				FormatHeaderElement(buffer, elems[i], quote);
			}
			return buffer;
		}

		/// <summary>Estimates the length of formatted header elements.</summary>
		/// <remarks>Estimates the length of formatted header elements.</remarks>
		/// <param name="elems">the header elements to format, or <code>null</code></param>
		/// <returns>a length estimate, in number of characters</returns>
		protected internal virtual int EstimateElementsLen(HeaderElement[] elems)
		{
			if ((elems == null) || (elems.Length < 1))
			{
				return 0;
			}
			int result = (elems.Length - 1) * 2;
			// elements separated by ", "
			foreach (HeaderElement elem in elems)
			{
				result += EstimateHeaderElementLen(elem);
			}
			return result;
		}

		/// <summary>Formats a header element.</summary>
		/// <remarks>Formats a header element.</remarks>
		/// <param name="elem">the header element to format</param>
		/// <param name="quote">
		/// <code>true</code> to always format with quoted values,
		/// <code>false</code> to use quotes only when necessary
		/// </param>
		/// <param name="formatter">
		/// the formatter to use, or <code>null</code>
		/// for the
		/// <see cref="Instance">default</see>
		/// </param>
		/// <returns>the formatted header element</returns>
		public static string FormatHeaderElement(HeaderElement elem, bool quote, HeaderValueFormatter
			 formatter)
		{
			return (formatter != null ? formatter : Org.Apache.Http.Message.BasicHeaderValueFormatter
				.Instance).FormatHeaderElement(null, elem, quote).ToString();
		}

		// non-javadoc, see interface HeaderValueFormatter
		public virtual CharArrayBuffer FormatHeaderElement(CharArrayBuffer charBuffer, HeaderElement
			 elem, bool quote)
		{
			Args.NotNull(elem, "Header element");
			int len = EstimateHeaderElementLen(elem);
			CharArrayBuffer buffer = charBuffer;
			if (buffer == null)
			{
				buffer = new CharArrayBuffer(len);
			}
			else
			{
				buffer.EnsureCapacity(len);
			}
			buffer.Append(elem.GetName());
			string value = elem.GetValue();
			if (value != null)
			{
				buffer.Append('=');
				DoFormatValue(buffer, value, quote);
			}
			int parcnt = elem.GetParameterCount();
			if (parcnt > 0)
			{
				for (int i = 0; i < parcnt; i++)
				{
					buffer.Append("; ");
					FormatNameValuePair(buffer, elem.GetParameter(i), quote);
				}
			}
			return buffer;
		}

		/// <summary>Estimates the length of a formatted header element.</summary>
		/// <remarks>Estimates the length of a formatted header element.</remarks>
		/// <param name="elem">the header element to format, or <code>null</code></param>
		/// <returns>a length estimate, in number of characters</returns>
		protected internal virtual int EstimateHeaderElementLen(HeaderElement elem)
		{
			if (elem == null)
			{
				return 0;
			}
			int result = elem.GetName().Length;
			// name
			string value = elem.GetValue();
			if (value != null)
			{
				// assume quotes, but no escaped characters
				result += 3 + value.Length;
			}
			// ="value"
			int parcnt = elem.GetParameterCount();
			if (parcnt > 0)
			{
				for (int i = 0; i < parcnt; i++)
				{
					result += 2 + EstimateNameValuePairLen(elem.GetParameter(i));
				}
			}
			// ; <param>
			return result;
		}

		/// <summary>Formats a set of parameters.</summary>
		/// <remarks>Formats a set of parameters.</remarks>
		/// <param name="nvps">the parameters to format</param>
		/// <param name="quote">
		/// <code>true</code> to always format with quoted values,
		/// <code>false</code> to use quotes only when necessary
		/// </param>
		/// <param name="formatter">
		/// the formatter to use, or <code>null</code>
		/// for the
		/// <see cref="Instance">default</see>
		/// </param>
		/// <returns>the formatted parameters</returns>
		public static string FormatParameters(NameValuePair[] nvps, bool quote, HeaderValueFormatter
			 formatter)
		{
			return (formatter != null ? formatter : Org.Apache.Http.Message.BasicHeaderValueFormatter
				.Instance).FormatParameters(null, nvps, quote).ToString();
		}

		// non-javadoc, see interface HeaderValueFormatter
		public virtual CharArrayBuffer FormatParameters(CharArrayBuffer charBuffer, NameValuePair
			[] nvps, bool quote)
		{
			Args.NotNull(nvps, "Header parameter array");
			int len = EstimateParametersLen(nvps);
			CharArrayBuffer buffer = charBuffer;
			if (buffer == null)
			{
				buffer = new CharArrayBuffer(len);
			}
			else
			{
				buffer.EnsureCapacity(len);
			}
			for (int i = 0; i < nvps.Length; i++)
			{
				if (i > 0)
				{
					buffer.Append("; ");
				}
				FormatNameValuePair(buffer, nvps[i], quote);
			}
			return buffer;
		}

		/// <summary>Estimates the length of formatted parameters.</summary>
		/// <remarks>Estimates the length of formatted parameters.</remarks>
		/// <param name="nvps">the parameters to format, or <code>null</code></param>
		/// <returns>a length estimate, in number of characters</returns>
		protected internal virtual int EstimateParametersLen(NameValuePair[] nvps)
		{
			if ((nvps == null) || (nvps.Length < 1))
			{
				return 0;
			}
			int result = (nvps.Length - 1) * 2;
			// "; " between the parameters
			foreach (NameValuePair nvp in nvps)
			{
				result += EstimateNameValuePairLen(nvp);
			}
			return result;
		}

		/// <summary>Formats a name-value pair.</summary>
		/// <remarks>Formats a name-value pair.</remarks>
		/// <param name="nvp">the name-value pair to format</param>
		/// <param name="quote">
		/// <code>true</code> to always format with a quoted value,
		/// <code>false</code> to use quotes only when necessary
		/// </param>
		/// <param name="formatter">
		/// the formatter to use, or <code>null</code>
		/// for the
		/// <see cref="Instance">default</see>
		/// </param>
		/// <returns>the formatted name-value pair</returns>
		public static string FormatNameValuePair(NameValuePair nvp, bool quote, HeaderValueFormatter
			 formatter)
		{
			return (formatter != null ? formatter : Org.Apache.Http.Message.BasicHeaderValueFormatter
				.Instance).FormatNameValuePair(null, nvp, quote).ToString();
		}

		// non-javadoc, see interface HeaderValueFormatter
		public virtual CharArrayBuffer FormatNameValuePair(CharArrayBuffer charBuffer, NameValuePair
			 nvp, bool quote)
		{
			Args.NotNull(nvp, "Name / value pair");
			int len = EstimateNameValuePairLen(nvp);
			CharArrayBuffer buffer = charBuffer;
			if (buffer == null)
			{
				buffer = new CharArrayBuffer(len);
			}
			else
			{
				buffer.EnsureCapacity(len);
			}
			buffer.Append(nvp.GetName());
			string value = nvp.GetValue();
			if (value != null)
			{
				buffer.Append('=');
				DoFormatValue(buffer, value, quote);
			}
			return buffer;
		}

		/// <summary>Estimates the length of a formatted name-value pair.</summary>
		/// <remarks>Estimates the length of a formatted name-value pair.</remarks>
		/// <param name="nvp">the name-value pair to format, or <code>null</code></param>
		/// <returns>a length estimate, in number of characters</returns>
		protected internal virtual int EstimateNameValuePairLen(NameValuePair nvp)
		{
			if (nvp == null)
			{
				return 0;
			}
			int result = nvp.GetName().Length;
			// name
			string value = nvp.GetValue();
			if (value != null)
			{
				// assume quotes, but no escaped characters
				result += 3 + value.Length;
			}
			// ="value"
			return result;
		}

		/// <summary>Actually formats the value of a name-value pair.</summary>
		/// <remarks>
		/// Actually formats the value of a name-value pair.
		/// This does not include a leading = character.
		/// Called from
		/// <see cref="FormatNameValuePair(Org.Apache.Http.NameValuePair, bool, HeaderValueFormatter)
		/// 	">formatNameValuePair</see>
		/// .
		/// </remarks>
		/// <param name="buffer">the buffer to append to, never <code>null</code></param>
		/// <param name="value">the value to append, never <code>null</code></param>
		/// <param name="quote">
		/// <code>true</code> to always format with quotes,
		/// <code>false</code> to use quotes only when necessary
		/// </param>
		protected internal virtual void DoFormatValue(CharArrayBuffer buffer, string value
			, bool quote)
		{
			bool quoteFlag = quote;
			if (!quoteFlag)
			{
				for (int i = 0; (i < value.Length) && !quoteFlag; i++)
				{
					quoteFlag = IsSeparator(value[i]);
				}
			}
			if (quoteFlag)
			{
				buffer.Append('"');
			}
			for (int i_1 = 0; i_1 < value.Length; i_1++)
			{
				char ch = value[i_1];
				if (IsUnsafe(ch))
				{
					buffer.Append('\\');
				}
				buffer.Append(ch);
			}
			if (quoteFlag)
			{
				buffer.Append('"');
			}
		}

		/// <summary>
		/// Checks whether a character is a
		/// <see cref="Separators">separator</see>
		/// .
		/// </summary>
		/// <param name="ch">the character to check</param>
		/// <returns>
		/// <code>true</code> if the character is a separator,
		/// <code>false</code> otherwise
		/// </returns>
		protected internal virtual bool IsSeparator(char ch)
		{
			return Separators.IndexOf(ch) >= 0;
		}

		/// <summary>
		/// Checks whether a character is
		/// <see cref="UnsafeChars">unsafe</see>
		/// .
		/// </summary>
		/// <param name="ch">the character to check</param>
		/// <returns>
		/// <code>true</code> if the character is unsafe,
		/// <code>false</code> otherwise
		/// </returns>
		protected internal virtual bool IsUnsafe(char ch)
		{
			return UnsafeChars.IndexOf(ch) >= 0;
		}
	}
}
