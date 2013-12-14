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
	/// This class represents a raw HTTP header whose content is parsed 'on demand'
	/// only when the header value needs to be consumed.
	/// </summary>
	/// <remarks>
	/// This class represents a raw HTTP header whose content is parsed 'on demand'
	/// only when the header value needs to be consumed.
	/// </remarks>
	/// <since>4.0</since>
	[System.Serializable]
	public class BufferedHeader : FormattedHeader, ICloneable
	{
		private const long serialVersionUID = -2768352615787625448L;

		/// <summary>Header name.</summary>
		/// <remarks>Header name.</remarks>
		private readonly string name;

		/// <summary>The buffer containing the entire header line.</summary>
		/// <remarks>The buffer containing the entire header line.</remarks>
		private readonly CharArrayBuffer buffer;

		/// <summary>The beginning of the header value in the buffer</summary>
		private readonly int valuePos;

		/// <summary>Creates a new header from a buffer.</summary>
		/// <remarks>
		/// Creates a new header from a buffer.
		/// The name of the header will be parsed immediately,
		/// the value only if it is accessed.
		/// </remarks>
		/// <param name="buffer">the buffer containing the header to represent</param>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		public BufferedHeader(CharArrayBuffer buffer) : base()
		{
			Args.NotNull(buffer, "Char array buffer");
			int colon = buffer.IndexOf(':');
			if (colon == -1)
			{
				throw new ParseException("Invalid header: " + buffer.ToString());
			}
			string s = buffer.SubstringTrimmed(0, colon);
			if (s.Length == 0)
			{
				throw new ParseException("Invalid header: " + buffer.ToString());
			}
			this.buffer = buffer;
			this.name = s;
			this.valuePos = colon + 1;
		}

		public virtual string GetName()
		{
			return this.name;
		}

		public virtual string GetValue()
		{
			return this.buffer.SubstringTrimmed(this.valuePos, this.buffer.Length());
		}

		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public virtual HeaderElement[] GetElements()
		{
			ParserCursor cursor = new ParserCursor(0, this.buffer.Length());
			cursor.UpdatePos(this.valuePos);
			return BasicHeaderValueParser.Instance.ParseElements(this.buffer, cursor);
		}

		public virtual int GetValuePos()
		{
			return this.valuePos;
		}

		public virtual CharArrayBuffer GetBuffer()
		{
			return this.buffer;
		}

		public override string ToString()
		{
			return this.buffer.ToString();
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			// buffer is considered immutable
			// no need to make a copy of it
			return base.MemberwiseClone();
		}
	}
}
