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
	/// <summary>
	/// Basic implementation of a
	/// <see cref="Org.Apache.Http.HeaderElementIterator">Org.Apache.Http.HeaderElementIterator
	/// 	</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public class BasicHeaderElementIterator : HeaderElementIterator
	{
		private readonly HeaderIterator headerIt;

		private readonly HeaderValueParser parser;

		private HeaderElement currentElement = null;

		private CharArrayBuffer buffer = null;

		private ParserCursor cursor = null;

		/// <summary>Creates a new instance of BasicHeaderElementIterator</summary>
		public BasicHeaderElementIterator(HeaderIterator headerIterator, HeaderValueParser
			 parser)
		{
			this.headerIt = Args.NotNull(headerIterator, "Header iterator");
			this.parser = Args.NotNull(parser, "Parser");
		}

		public BasicHeaderElementIterator(HeaderIterator headerIterator) : this(headerIterator
			, BasicHeaderValueParser.Instance)
		{
		}

		private void BufferHeaderValue()
		{
			this.cursor = null;
			this.buffer = null;
			while (this.headerIt.HasNext())
			{
				Header h = this.headerIt.NextHeader();
				if (h is FormattedHeader)
				{
					this.buffer = ((FormattedHeader)h).GetBuffer();
					this.cursor = new ParserCursor(0, this.buffer.Length());
					this.cursor.UpdatePos(((FormattedHeader)h).GetValuePos());
					break;
				}
				else
				{
					string value = h.GetValue();
					if (value != null)
					{
						this.buffer = new CharArrayBuffer(value.Length);
						this.buffer.Append(value);
						this.cursor = new ParserCursor(0, this.buffer.Length());
						break;
					}
				}
			}
		}

		private void ParseNextElement()
		{
			// loop while there are headers left to parse
			while (this.headerIt.HasNext() || this.cursor != null)
			{
				if (this.cursor == null || this.cursor.AtEnd())
				{
					// get next header value
					BufferHeaderValue();
				}
				// Anything buffered?
				if (this.cursor != null)
				{
					// loop while there is data in the buffer
					while (!this.cursor.AtEnd())
					{
						HeaderElement e = this.parser.ParseHeaderElement(this.buffer, this.cursor);
						if (!(e.GetName().Length == 0 && e.GetValue() == null))
						{
							// Found something
							this.currentElement = e;
							return;
						}
					}
					// if at the end of the buffer
					if (this.cursor.AtEnd())
					{
						// discard it
						this.cursor = null;
						this.buffer = null;
					}
				}
			}
		}

		public virtual bool HasNext()
		{
			if (this.currentElement == null)
			{
				ParseNextElement();
			}
			return this.currentElement != null;
		}

		/// <exception cref="Sharpen.NoSuchElementException"></exception>
		public virtual HeaderElement NextElement()
		{
			if (this.currentElement == null)
			{
				ParseNextElement();
			}
			if (this.currentElement == null)
			{
				throw new NoSuchElementException("No more header elements available");
			}
			HeaderElement element = this.currentElement;
			this.currentElement = null;
			return element;
		}

		/// <exception cref="Sharpen.NoSuchElementException"></exception>
		public object Next()
		{
			return NextElement();
		}

		/// <exception cref="System.NotSupportedException"></exception>
		public virtual void Remove()
		{
			throw new NotSupportedException("Remove not supported");
		}
	}
}
