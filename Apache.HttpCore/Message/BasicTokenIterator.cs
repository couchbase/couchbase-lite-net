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
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>
	/// Basic implementation of a
	/// <see cref="Org.Apache.Http.TokenIterator">Org.Apache.Http.TokenIterator</see>
	/// .
	/// This implementation parses <tt>#token<tt> sequences as
	/// defined by RFC 2616, section 2.
	/// It extends that definition somewhat beyond US-ASCII.
	/// </summary>
	/// <since>4.0</since>
	public class BasicTokenIterator : TokenIterator
	{
		/// <summary>The HTTP separator characters.</summary>
		/// <remarks>The HTTP separator characters. Defined in RFC 2616, section 2.2.</remarks>
		public const string HttpSeparators = " ,;=()<>@:\\\"/[]?{}\t";

		/// <summary>The iterator from which to obtain the next header.</summary>
		/// <remarks>The iterator from which to obtain the next header.</remarks>
		protected internal readonly HeaderIterator headerIt;

		/// <summary>The value of the current header.</summary>
		/// <remarks>
		/// The value of the current header.
		/// This is the header value that includes
		/// <see cref="currentToken">currentToken</see>
		/// .
		/// Undefined if the iteration is over.
		/// </remarks>
		protected internal string currentHeader;

		/// <summary>
		/// The token to be returned by the next call to
		/// <see cref="NextToken()">NextToken()</see>
		/// .
		/// <code>null</code> if the iteration is over.
		/// </summary>
		protected internal string currentToken;

		/// <summary>
		/// The position after
		/// <see cref="currentToken">currentToken</see>
		/// in
		/// <see cref="currentHeader">currentHeader</see>
		/// .
		/// Undefined if the iteration is over.
		/// </summary>
		protected internal int searchPos;

		/// <summary>
		/// Creates a new instance of
		/// <see cref="BasicTokenIterator">BasicTokenIterator</see>
		/// .
		/// </summary>
		/// <param name="headerIterator">the iterator for the headers to tokenize</param>
		public BasicTokenIterator(HeaderIterator headerIterator) : base()
		{
			// the order of the characters here is adjusted to put the
			// most likely candidates at the beginning of the collection
			this.headerIt = Args.NotNull(headerIterator, "Header iterator");
			this.searchPos = FindNext(-1);
		}

		// non-javadoc, see interface TokenIterator
		public virtual bool HasNext()
		{
			return (this.currentToken != null);
		}

		/// <summary>Obtains the next token from this iteration.</summary>
		/// <remarks>Obtains the next token from this iteration.</remarks>
		/// <returns>the next token in this iteration</returns>
		/// <exception cref="Sharpen.NoSuchElementException">if the iteration is already over
		/// 	</exception>
		/// <exception cref="Org.Apache.Http.ParseException">if an invalid header value is encountered
		/// 	</exception>
		public virtual string NextToken()
		{
			if (this.currentToken == null)
			{
				throw new NoSuchElementException("Iteration already finished.");
			}
			string result = this.currentToken;
			// updates currentToken, may trigger ParseException:
			this.searchPos = FindNext(this.searchPos);
			return result;
		}

		/// <summary>Returns the next token.</summary>
		/// <remarks>
		/// Returns the next token.
		/// Same as
		/// <see cref="NextToken()">NextToken()</see>
		/// , but with generic return type.
		/// </remarks>
		/// <returns>the next token in this iteration</returns>
		/// <exception cref="Sharpen.NoSuchElementException">if there are no more tokens</exception>
		/// <exception cref="Org.Apache.Http.ParseException">if an invalid header value is encountered
		/// 	</exception>
		public object Next()
		{
			return NextToken();
		}

		/// <summary>Removing tokens is not supported.</summary>
		/// <remarks>Removing tokens is not supported.</remarks>
		/// <exception cref="System.NotSupportedException">always</exception>
		public void Remove()
		{
			throw new NotSupportedException("Removing tokens is not supported.");
		}

		/// <summary>Determines the next token.</summary>
		/// <remarks>
		/// Determines the next token.
		/// If found, the token is stored in
		/// <see cref="currentToken">currentToken</see>
		/// .
		/// The return value indicates the position after the token
		/// in
		/// <see cref="currentHeader">currentHeader</see>
		/// . If necessary, the next header
		/// will be obtained from
		/// <see cref="headerIt">headerIt</see>
		/// .
		/// If not found,
		/// <see cref="currentToken">currentToken</see>
		/// is set to <code>null</code>.
		/// </remarks>
		/// <param name="pos">
		/// the position in the current header at which to
		/// start the search, -1 to search in the first header
		/// </param>
		/// <returns>
		/// the position after the found token in the current header, or
		/// negative if there was no next token
		/// </returns>
		/// <exception cref="Org.Apache.Http.ParseException">if an invalid header value is encountered
		/// 	</exception>
		protected internal virtual int FindNext(int pos)
		{
			int from = pos;
			if (from < 0)
			{
				// called from the constructor, initialize the first header
				if (!this.headerIt.HasNext())
				{
					return -1;
				}
				this.currentHeader = this.headerIt.NextHeader().GetValue();
				from = 0;
			}
			else
			{
				// called after a token, make sure there is a separator
				from = FindTokenSeparator(from);
			}
			int start = FindTokenStart(from);
			if (start < 0)
			{
				this.currentToken = null;
				return -1;
			}
			// nothing found
			int end = FindTokenEnd(start);
			this.currentToken = CreateToken(this.currentHeader, start, end);
			return end;
		}

		/// <summary>Creates a new token to be returned.</summary>
		/// <remarks>
		/// Creates a new token to be returned.
		/// Called from
		/// <see cref="FindNext(int)">findNext</see>
		/// after the token is identified.
		/// The default implementation simply calls
		/// <see cref="Sharpen.Runtime.Substring(int)">String.substring</see>
		/// .
		/// <br/>
		/// If header values are significantly longer than tokens, and some
		/// tokens are permanently referenced by the application, there can
		/// be problems with garbage collection. A substring will hold a
		/// reference to the full characters of the original string and
		/// therefore occupies more memory than might be expected.
		/// To avoid this, override this method and create a new string
		/// instead of a substring.
		/// </remarks>
		/// <param name="value">the full header value from which to create a token</param>
		/// <param name="start">the index of the first token character</param>
		/// <param name="end">the index after the last token character</param>
		/// <returns>a string representing the token identified by the arguments</returns>
		protected internal virtual string CreateToken(string value, int start, int end)
		{
			return Sharpen.Runtime.Substring(value, start, end);
		}

		/// <summary>Determines the starting position of the next token.</summary>
		/// <remarks>
		/// Determines the starting position of the next token.
		/// This method will iterate over headers if necessary.
		/// </remarks>
		/// <param name="pos">
		/// the position in the current header at which to
		/// start the search
		/// </param>
		/// <returns>
		/// the position of the token start in the current header,
		/// negative if no token start could be found
		/// </returns>
		protected internal virtual int FindTokenStart(int pos)
		{
			int from = Args.NotNegative(pos, "Search position");
			bool found = false;
			while (!found && (this.currentHeader != null))
			{
				int to = this.currentHeader.Length;
				while (!found && (from < to))
				{
					char ch = this.currentHeader[from];
					if (IsTokenSeparator(ch) || IsWhitespace(ch))
					{
						// whitspace and token separators are skipped
						from++;
					}
					else
					{
						if (IsTokenChar(this.currentHeader[from]))
						{
							// found the start of a token
							found = true;
						}
						else
						{
							throw new ParseException("Invalid character before token (pos " + from + "): " + 
								this.currentHeader);
						}
					}
				}
				if (!found)
				{
					if (this.headerIt.HasNext())
					{
						this.currentHeader = this.headerIt.NextHeader().GetValue();
						from = 0;
					}
					else
					{
						this.currentHeader = null;
					}
				}
			}
			// while headers
			return found ? from : -1;
		}

		/// <summary>Determines the position of the next token separator.</summary>
		/// <remarks>
		/// Determines the position of the next token separator.
		/// Because of multi-header joining rules, the end of a
		/// header value is a token separator. This method does
		/// therefore not need to iterate over headers.
		/// </remarks>
		/// <param name="pos">
		/// the position in the current header at which to
		/// start the search
		/// </param>
		/// <returns>
		/// the position of a token separator in the current header,
		/// or at the end
		/// </returns>
		/// <exception cref="Org.Apache.Http.ParseException">
		/// if a new token is found before a token separator.
		/// RFC 2616, section 2.1 explicitly requires a comma between
		/// tokens for <tt>#</tt>.
		/// </exception>
		protected internal virtual int FindTokenSeparator(int pos)
		{
			int from = Args.NotNegative(pos, "Search position");
			bool found = false;
			int to = this.currentHeader.Length;
			while (!found && (from < to))
			{
				char ch = this.currentHeader[from];
				if (IsTokenSeparator(ch))
				{
					found = true;
				}
				else
				{
					if (IsWhitespace(ch))
					{
						from++;
					}
					else
					{
						if (IsTokenChar(ch))
						{
							throw new ParseException("Tokens without separator (pos " + from + "): " + this.currentHeader
								);
						}
						else
						{
							throw new ParseException("Invalid character after token (pos " + from + "): " + this
								.currentHeader);
						}
					}
				}
			}
			return from;
		}

		/// <summary>Determines the ending position of the current token.</summary>
		/// <remarks>
		/// Determines the ending position of the current token.
		/// This method will not leave the current header value,
		/// since the end of the header value is a token boundary.
		/// </remarks>
		/// <param name="from">the position of the first character of the token</param>
		/// <returns>
		/// the position after the last character of the token.
		/// The behavior is undefined if <code>from</code> does not
		/// point to a token character in the current header value.
		/// </returns>
		protected internal virtual int FindTokenEnd(int from)
		{
			Args.NotNegative(from, "Search position");
			int to = this.currentHeader.Length;
			int end = from + 1;
			while ((end < to) && IsTokenChar(this.currentHeader[end]))
			{
				end++;
			}
			return end;
		}

		/// <summary>Checks whether a character is a token separator.</summary>
		/// <remarks>
		/// Checks whether a character is a token separator.
		/// RFC 2616, section 2.1 defines comma as the separator for
		/// <tt>#token</tt> sequences. The end of a header value will
		/// also separate tokens, but that is not a character check.
		/// </remarks>
		/// <param name="ch">the character to check</param>
		/// <returns>
		/// <code>true</code> if the character is a token separator,
		/// <code>false</code> otherwise
		/// </returns>
		protected internal virtual bool IsTokenSeparator(char ch)
		{
			return (ch == ',');
		}

		/// <summary>Checks whether a character is a whitespace character.</summary>
		/// <remarks>
		/// Checks whether a character is a whitespace character.
		/// RFC 2616, section 2.2 defines space and horizontal tab as whitespace.
		/// The optional preceeding line break is irrelevant, since header
		/// continuation is handled transparently when parsing messages.
		/// </remarks>
		/// <param name="ch">the character to check</param>
		/// <returns>
		/// <code>true</code> if the character is whitespace,
		/// <code>false</code> otherwise
		/// </returns>
		protected internal virtual bool IsWhitespace(char ch)
		{
			// we do not use Character.isWhitspace(ch) here, since that allows
			// many control characters which are not whitespace as per RFC 2616
			return ((ch == '\t') || System.Char.IsWhiteSpace(ch));
		}

		/// <summary>Checks whether a character is a valid token character.</summary>
		/// <remarks>
		/// Checks whether a character is a valid token character.
		/// Whitespace, control characters, and HTTP separators are not
		/// valid token characters. The HTTP specification (RFC 2616, section 2.2)
		/// defines tokens only for the US-ASCII character set, this
		/// method extends the definition to other character sets.
		/// </remarks>
		/// <param name="ch">the character to check</param>
		/// <returns>
		/// <code>true</code> if the character is a valid token start,
		/// <code>false</code> otherwise
		/// </returns>
		protected internal virtual bool IsTokenChar(char ch)
		{
			// common sense extension of ALPHA + DIGIT
			if (char.IsLetterOrDigit(ch))
			{
				return true;
			}
			// common sense extension of CTL
			if (char.IsISOControl(ch))
			{
				return false;
			}
			// no common sense extension for this
			if (IsHttpSeparator(ch))
			{
				return false;
			}
			// RFC 2616, section 2.2 defines a token character as
			// "any CHAR except CTLs or separators". The controls
			// and separators are included in the checks above.
			// This will yield unexpected results for Unicode format characters.
			// If that is a problem, overwrite isHttpSeparator(char) to filter
			// out the false positives.
			return true;
		}

		/// <summary>Checks whether a character is an HTTP separator.</summary>
		/// <remarks>
		/// Checks whether a character is an HTTP separator.
		/// The implementation in this class checks only for the HTTP separators
		/// defined in RFC 2616, section 2.2. If you need to detect other
		/// separators beyond the US-ASCII character set, override this method.
		/// </remarks>
		/// <param name="ch">the character to check</param>
		/// <returns><code>true</code> if the character is an HTTP separator</returns>
		protected internal virtual bool IsHttpSeparator(char ch)
		{
			return (HttpSeparators.IndexOf(ch) >= 0);
		}
	}
}
