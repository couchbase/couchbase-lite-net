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
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>A class for combining a set of headers.</summary>
	/// <remarks>
	/// A class for combining a set of headers.
	/// This class allows for multiple headers with the same name and
	/// keeps track of the order in which headers were added.
	/// </remarks>
	/// <since>4.0</since>
	[System.Serializable]
	public class HeaderGroup : ICloneable
	{
		private const long serialVersionUID = 2608834160639271617L;

		/// <summary>The list of headers for this group, in the order in which they were added
		/// 	</summary>
		private readonly IList<Header> headers;

		/// <summary>Constructor for HeaderGroup.</summary>
		/// <remarks>Constructor for HeaderGroup.</remarks>
		public HeaderGroup()
		{
			this.headers = new AList<Header>(16);
		}

		/// <summary>Removes any contained headers.</summary>
		/// <remarks>Removes any contained headers.</remarks>
		public virtual void Clear()
		{
			headers.Clear();
		}

		/// <summary>Adds the given header to the group.</summary>
		/// <remarks>
		/// Adds the given header to the group.  The order in which this header was
		/// added is preserved.
		/// </remarks>
		/// <param name="header">the header to add</param>
		public virtual void AddHeader(Header header)
		{
			if (header == null)
			{
				return;
			}
			headers.AddItem(header);
		}

		/// <summary>Removes the given header.</summary>
		/// <remarks>Removes the given header.</remarks>
		/// <param name="header">the header to remove</param>
		public virtual void RemoveHeader(Header header)
		{
			if (header == null)
			{
				return;
			}
			headers.Remove(header);
		}

		/// <summary>Replaces the first occurence of the header with the same name.</summary>
		/// <remarks>
		/// Replaces the first occurence of the header with the same name. If no header with
		/// the same name is found the given header is added to the end of the list.
		/// </remarks>
		/// <param name="header">
		/// the new header that should replace the first header with the same
		/// name if present in the list.
		/// </param>
		public virtual void UpdateHeader(Header header)
		{
			if (header == null)
			{
				return;
			}
			// HTTPCORE-361 : we don't use the for-each syntax, i.e.
			//     for (Header header : headers)
			// as that creates an Iterator that needs to be garbage-collected
			for (int i = 0; i < this.headers.Count; i++)
			{
				Header current = this.headers[i];
				if (Sharpen.Runtime.EqualsIgnoreCase(current.GetName(), header.GetName()))
				{
					this.headers.Set(i, header);
					return;
				}
			}
			this.headers.AddItem(header);
		}

		/// <summary>
		/// Sets all of the headers contained within this group overriding any
		/// existing headers.
		/// </summary>
		/// <remarks>
		/// Sets all of the headers contained within this group overriding any
		/// existing headers. The headers are added in the order in which they appear
		/// in the array.
		/// </remarks>
		/// <param name="headers">the headers to set</param>
		public virtual void SetHeaders(Header[] headers)
		{
			Clear();
			if (headers == null)
			{
				return;
			}
			Sharpen.Collections.AddAll(this.headers, headers);
		}

		/// <summary>Gets a header representing all of the header values with the given name.
		/// 	</summary>
		/// <remarks>
		/// Gets a header representing all of the header values with the given name.
		/// If more that one header with the given name exists the values will be
		/// combined with a "," as per RFC 2616.
		/// <p>Header name comparison is case insensitive.
		/// </remarks>
		/// <param name="name">the name of the header(s) to get</param>
		/// <returns>
		/// a header with a condensed value or <code>null</code> if no
		/// headers by the given name are present
		/// </returns>
		public virtual Header GetCondensedHeader(string name)
		{
			Header[] hdrs = GetHeaders(name);
			if (hdrs.Length == 0)
			{
				return null;
			}
			else
			{
				if (hdrs.Length == 1)
				{
					return hdrs[0];
				}
				else
				{
					CharArrayBuffer valueBuffer = new CharArrayBuffer(128);
					valueBuffer.Append(hdrs[0].GetValue());
					for (int i = 1; i < hdrs.Length; i++)
					{
						valueBuffer.Append(", ");
						valueBuffer.Append(hdrs[i].GetValue());
					}
					return new BasicHeader(name.ToLower(Sharpen.Extensions.GetEnglishCulture()), valueBuffer
						.ToString());
				}
			}
		}

		/// <summary>Gets all of the headers with the given name.</summary>
		/// <remarks>
		/// Gets all of the headers with the given name.  The returned array
		/// maintains the relative order in which the headers were added.
		/// <p>Header name comparison is case insensitive.
		/// </remarks>
		/// <param name="name">the name of the header(s) to get</param>
		/// <returns>an array of length &gt;= 0</returns>
		public virtual Header[] GetHeaders(string name)
		{
			IList<Header> headersFound = new AList<Header>();
			// HTTPCORE-361 : we don't use the for-each syntax, i.e.
			//     for (Header header : headers)
			// as that creates an Iterator that needs to be garbage-collected
			for (int i = 0; i < this.headers.Count; i++)
			{
				Header header = this.headers[i];
				if (Sharpen.Runtime.EqualsIgnoreCase(header.GetName(), name))
				{
					headersFound.AddItem(header);
				}
			}
			return Sharpen.Collections.ToArray(headersFound, new Header[headersFound.Count]);
		}

		/// <summary>Gets the first header with the given name.</summary>
		/// <remarks>
		/// Gets the first header with the given name.
		/// <p>Header name comparison is case insensitive.
		/// </remarks>
		/// <param name="name">the name of the header to get</param>
		/// <returns>the first header or <code>null</code></returns>
		public virtual Header GetFirstHeader(string name)
		{
			// HTTPCORE-361 : we don't use the for-each syntax, i.e.
			//     for (Header header : headers)
			// as that creates an Iterator that needs to be garbage-collected
			for (int i = 0; i < this.headers.Count; i++)
			{
				Header header = this.headers[i];
				if (Sharpen.Runtime.EqualsIgnoreCase(header.GetName(), name))
				{
					return header;
				}
			}
			return null;
		}

		/// <summary>Gets the last header with the given name.</summary>
		/// <remarks>
		/// Gets the last header with the given name.
		/// <p>Header name comparison is case insensitive.
		/// </remarks>
		/// <param name="name">the name of the header to get</param>
		/// <returns>the last header or <code>null</code></returns>
		public virtual Header GetLastHeader(string name)
		{
			// start at the end of the list and work backwards
			for (int i = headers.Count - 1; i >= 0; i--)
			{
				Header header = headers[i];
				if (Sharpen.Runtime.EqualsIgnoreCase(header.GetName(), name))
				{
					return header;
				}
			}
			return null;
		}

		/// <summary>Gets all of the headers contained within this group.</summary>
		/// <remarks>Gets all of the headers contained within this group.</remarks>
		/// <returns>an array of length &gt;= 0</returns>
		public virtual Header[] GetAllHeaders()
		{
			return Sharpen.Collections.ToArray(headers, new Header[headers.Count]);
		}

		/// <summary>Tests if headers with the given name are contained within this group.</summary>
		/// <remarks>
		/// Tests if headers with the given name are contained within this group.
		/// <p>Header name comparison is case insensitive.
		/// </remarks>
		/// <param name="name">the header name to test for</param>
		/// <returns>
		/// <code>true</code> if at least one header with the name is
		/// contained, <code>false</code> otherwise
		/// </returns>
		public virtual bool ContainsHeader(string name)
		{
			// HTTPCORE-361 : we don't use the for-each syntax, i.e.
			//     for (Header header : headers)
			// as that creates an Iterator that needs to be garbage-collected
			for (int i = 0; i < this.headers.Count; i++)
			{
				Header header = this.headers[i];
				if (Sharpen.Runtime.EqualsIgnoreCase(header.GetName(), name))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Returns an iterator over this group of headers.</summary>
		/// <remarks>Returns an iterator over this group of headers.</remarks>
		/// <returns>iterator over this group of headers.</returns>
		/// <since>4.0</since>
		public virtual HeaderIterator Iterator()
		{
			return new BasicListHeaderIterator(this.headers, null);
		}

		/// <summary>Returns an iterator over the headers with a given name in this group.</summary>
		/// <remarks>Returns an iterator over the headers with a given name in this group.</remarks>
		/// <param name="name">
		/// the name of the headers over which to iterate, or
		/// <code>null</code> for all headers
		/// </param>
		/// <returns>iterator over some headers in this group.</returns>
		/// <since>4.0</since>
		public virtual HeaderIterator Iterator(string name)
		{
			return new BasicListHeaderIterator(this.headers, name);
		}

		/// <summary>Returns a copy of this object</summary>
		/// <returns>copy of this object</returns>
		/// <since>4.0</since>
		public virtual Org.Apache.Http.Message.HeaderGroup Copy()
		{
			Org.Apache.Http.Message.HeaderGroup clone = new Org.Apache.Http.Message.HeaderGroup
				();
			Sharpen.Collections.AddAll(clone.headers, this.headers);
			return clone;
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			return base.MemberwiseClone();
		}

		public override string ToString()
		{
			return this.headers.ToString();
		}
	}
}
