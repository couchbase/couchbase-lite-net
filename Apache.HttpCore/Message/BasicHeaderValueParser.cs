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
using Org.Apache.Http;
using Org.Apache.Http.Message;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>Basic implementation for parsing header values into elements.</summary>
	/// <remarks>
	/// Basic implementation for parsing header values into elements.
	/// Instances of this class are stateless and thread-safe.
	/// Derived classes are expected to maintain these properties.
	/// </remarks>
	/// <since>4.0</since>
	public class BasicHeaderValueParser : HeaderValueParser
	{
		/// <summary>A default instance of this class, for use as default or fallback.</summary>
		/// <remarks>
		/// A default instance of this class, for use as default or fallback.
		/// Note that
		/// <see cref="BasicHeaderValueParser">BasicHeaderValueParser</see>
		/// is not a singleton, there
		/// can be many instances of the class itself and of derived classes.
		/// The instance here provides non-customized, default behavior.
		/// </remarks>
		[System.ObsoleteAttribute(@"(4.3) use Instance")]
		[Obsolete]
		public static readonly Org.Apache.Http.Message.BasicHeaderValueParser Default = new 
			Org.Apache.Http.Message.BasicHeaderValueParser();

		public static readonly Org.Apache.Http.Message.BasicHeaderValueParser Instance = 
			new Org.Apache.Http.Message.BasicHeaderValueParser();

		private const char ParamDelimiter = ';';

		private const char ElemDelimiter = ',';

		private static readonly char[] AllDelimiters = new char[] { ParamDelimiter, ElemDelimiter
			 };

		public BasicHeaderValueParser() : base()
		{
		}

		/// <summary>Parses elements with the given parser.</summary>
		/// <remarks>Parses elements with the given parser.</remarks>
		/// <param name="value">the header value to parse</param>
		/// <param name="parser">the parser to use, or <code>null</code> for default</param>
		/// <returns>array holding the header elements, never <code>null</code></returns>
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public static HeaderElement[] ParseElements(string value, HeaderValueParser parser
			)
		{
			Args.NotNull(value, "Value");
			CharArrayBuffer buffer = new CharArrayBuffer(value.Length);
			buffer.Append(value);
			ParserCursor cursor = new ParserCursor(0, value.Length);
			return (parser != null ? parser : Org.Apache.Http.Message.BasicHeaderValueParser.
				Instance).ParseElements(buffer, cursor);
		}

		// non-javadoc, see interface HeaderValueParser
		public virtual HeaderElement[] ParseElements(CharArrayBuffer buffer, ParserCursor
			 cursor)
		{
			Args.NotNull(buffer, "Char array buffer");
			Args.NotNull(cursor, "Parser cursor");
			IList<HeaderElement> elements = new AList<HeaderElement>();
			while (!cursor.AtEnd())
			{
				HeaderElement element = ParseHeaderElement(buffer, cursor);
				if (!(element.GetName().Length == 0 && element.GetValue() == null))
				{
					elements.AddItem(element);
				}
			}
			return Sharpen.Collections.ToArray(elements, new HeaderElement[elements.Count]);
		}

		/// <summary>Parses an element with the given parser.</summary>
		/// <remarks>Parses an element with the given parser.</remarks>
		/// <param name="value">the header element to parse</param>
		/// <param name="parser">the parser to use, or <code>null</code> for default</param>
		/// <returns>the parsed header element</returns>
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public static HeaderElement ParseHeaderElement(string value, HeaderValueParser parser
			)
		{
			Args.NotNull(value, "Value");
			CharArrayBuffer buffer = new CharArrayBuffer(value.Length);
			buffer.Append(value);
			ParserCursor cursor = new ParserCursor(0, value.Length);
			return (parser != null ? parser : Org.Apache.Http.Message.BasicHeaderValueParser.
				Instance).ParseHeaderElement(buffer, cursor);
		}

		// non-javadoc, see interface HeaderValueParser
		public virtual HeaderElement ParseHeaderElement(CharArrayBuffer buffer, ParserCursor
			 cursor)
		{
			Args.NotNull(buffer, "Char array buffer");
			Args.NotNull(cursor, "Parser cursor");
			NameValuePair nvp = ParseNameValuePair(buffer, cursor);
			NameValuePair[] @params = null;
			if (!cursor.AtEnd())
			{
				char ch = buffer.CharAt(cursor.GetPos() - 1);
				if (ch != ElemDelimiter)
				{
					@params = ParseParameters(buffer, cursor);
				}
			}
			return CreateHeaderElement(nvp.GetName(), nvp.GetValue(), @params);
		}

		/// <summary>Creates a header element.</summary>
		/// <remarks>
		/// Creates a header element.
		/// Called from
		/// <see cref="ParseHeaderElement(string, HeaderValueParser)">ParseHeaderElement(string, HeaderValueParser)
		/// 	</see>
		/// .
		/// </remarks>
		/// <returns>a header element representing the argument</returns>
		protected internal virtual HeaderElement CreateHeaderElement(string name, string 
			value, NameValuePair[] @params)
		{
			return new BasicHeaderElement(name, value, @params);
		}

		/// <summary>Parses parameters with the given parser.</summary>
		/// <remarks>Parses parameters with the given parser.</remarks>
		/// <param name="value">the parameter list to parse</param>
		/// <param name="parser">the parser to use, or <code>null</code> for default</param>
		/// <returns>array holding the parameters, never <code>null</code></returns>
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public static NameValuePair[] ParseParameters(string value, HeaderValueParser parser
			)
		{
			Args.NotNull(value, "Value");
			CharArrayBuffer buffer = new CharArrayBuffer(value.Length);
			buffer.Append(value);
			ParserCursor cursor = new ParserCursor(0, value.Length);
			return (parser != null ? parser : Org.Apache.Http.Message.BasicHeaderValueParser.
				Instance).ParseParameters(buffer, cursor);
		}

		// non-javadoc, see interface HeaderValueParser
		public virtual NameValuePair[] ParseParameters(CharArrayBuffer buffer, ParserCursor
			 cursor)
		{
			Args.NotNull(buffer, "Char array buffer");
			Args.NotNull(cursor, "Parser cursor");
			int pos = cursor.GetPos();
			int indexTo = cursor.GetUpperBound();
			while (pos < indexTo)
			{
				char ch = buffer.CharAt(pos);
				if (HTTP.IsWhitespace(ch))
				{
					pos++;
				}
				else
				{
					break;
				}
			}
			cursor.UpdatePos(pos);
			if (cursor.AtEnd())
			{
				return new NameValuePair[] {  };
			}
			IList<NameValuePair> @params = new AList<NameValuePair>();
			while (!cursor.AtEnd())
			{
				NameValuePair param = ParseNameValuePair(buffer, cursor);
				@params.AddItem(param);
				char ch = buffer.CharAt(cursor.GetPos() - 1);
				if (ch == ElemDelimiter)
				{
					break;
				}
			}
			return Sharpen.Collections.ToArray(@params, new NameValuePair[@params.Count]);
		}

		/// <summary>Parses a name-value-pair with the given parser.</summary>
		/// <remarks>Parses a name-value-pair with the given parser.</remarks>
		/// <param name="value">the NVP to parse</param>
		/// <param name="parser">the parser to use, or <code>null</code> for default</param>
		/// <returns>the parsed name-value pair</returns>
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public static NameValuePair ParseNameValuePair(string value, HeaderValueParser parser
			)
		{
			Args.NotNull(value, "Value");
			CharArrayBuffer buffer = new CharArrayBuffer(value.Length);
			buffer.Append(value);
			ParserCursor cursor = new ParserCursor(0, value.Length);
			return (parser != null ? parser : Org.Apache.Http.Message.BasicHeaderValueParser.
				Instance).ParseNameValuePair(buffer, cursor);
		}

		// non-javadoc, see interface HeaderValueParser
		public virtual NameValuePair ParseNameValuePair(CharArrayBuffer buffer, ParserCursor
			 cursor)
		{
			return ParseNameValuePair(buffer, cursor, AllDelimiters);
		}

		private static bool IsOneOf(char ch, char[] chs)
		{
			if (chs != null)
			{
				foreach (char ch2 in chs)
				{
					if (ch == ch2)
					{
						return true;
					}
				}
			}
			return false;
		}

		public virtual NameValuePair ParseNameValuePair(CharArrayBuffer buffer, ParserCursor
			 cursor, char[] delimiters)
		{
			Args.NotNull(buffer, "Char array buffer");
			Args.NotNull(cursor, "Parser cursor");
			bool terminated = false;
			int pos = cursor.GetPos();
			int indexFrom = cursor.GetPos();
			int indexTo = cursor.GetUpperBound();
			// Find name
			string name;
			while (pos < indexTo)
			{
				char ch = buffer.CharAt(pos);
				if (ch == '=')
				{
					break;
				}
				if (IsOneOf(ch, delimiters))
				{
					terminated = true;
					break;
				}
				pos++;
			}
			if (pos == indexTo)
			{
				terminated = true;
				name = buffer.SubstringTrimmed(indexFrom, indexTo);
			}
			else
			{
				name = buffer.SubstringTrimmed(indexFrom, pos);
				pos++;
			}
			if (terminated)
			{
				cursor.UpdatePos(pos);
				return CreateNameValuePair(name, null);
			}
			// Find value
			string value;
			int i1 = pos;
			bool qouted = false;
			bool escaped = false;
			while (pos < indexTo)
			{
				char ch = buffer.CharAt(pos);
				if (ch == '"' && !escaped)
				{
					qouted = !qouted;
				}
				if (!qouted && !escaped && IsOneOf(ch, delimiters))
				{
					terminated = true;
					break;
				}
				if (escaped)
				{
					escaped = false;
				}
				else
				{
					escaped = qouted && ch == '\\';
				}
				pos++;
			}
			int i2 = pos;
			// Trim leading white spaces
			while (i1 < i2 && (HTTP.IsWhitespace(buffer.CharAt(i1))))
			{
				i1++;
			}
			// Trim trailing white spaces
			while ((i2 > i1) && (HTTP.IsWhitespace(buffer.CharAt(i2 - 1))))
			{
				i2--;
			}
			// Strip away quotes if necessary
			if (((i2 - i1) >= 2) && (buffer.CharAt(i1) == '"') && (buffer.CharAt(i2 - 1) == '"'
				))
			{
				i1++;
				i2--;
			}
			value = buffer.Substring(i1, i2);
			if (terminated)
			{
				pos++;
			}
			cursor.UpdatePos(pos);
			return CreateNameValuePair(name, value);
		}

		/// <summary>Creates a name-value pair.</summary>
		/// <remarks>
		/// Creates a name-value pair.
		/// Called from
		/// <see cref="ParseNameValuePair(string, HeaderValueParser)">ParseNameValuePair(string, HeaderValueParser)
		/// 	</see>
		/// .
		/// </remarks>
		/// <param name="name">the name</param>
		/// <param name="value">the value, or <code>null</code></param>
		/// <returns>a name-value pair representing the arguments</returns>
		protected internal virtual NameValuePair CreateNameValuePair(string name, string 
			value)
		{
			return new BasicNameValuePair(name, value);
		}
	}
}
