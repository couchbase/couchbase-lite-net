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
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Router;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Router
{
	public class URLConnection : HttpURLConnection
	{
		private Header resHeader;

		private bool sentRequest = false;

		private ByteArrayOutputStream os;

		private Body responseBody;

		private bool chunked = false;

		private Dictionary<string, IList<string>> requestProperties = new Dictionary<string
			, IList<string>>();

		private const string Post = "POST";

		private const string Get = "GET";

		private const string Put = "PUT";

		private const string Head = "HEAD";

		private OutputStream responseOutputStream;

		private InputStream responseInputStream;

		private InputStream requestInputStream;

		public URLConnection(Uri url) : base(url)
		{
			responseInputStream = new PipedInputStream();
			try
			{
				responseOutputStream = new PipedOutputStream((PipedInputStream)responseInputStream
					);
			}
			catch (IOException e)
			{
				Log.E(Database.Tag, "Exception creating piped output stream", e);
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Connect()
		{
		}

		public override void Disconnect()
		{
		}

		// TODO Auto-generated method stub
		public override bool UsingProxy()
		{
			// TODO Auto-generated method stub
			return false;
		}

		public override IDictionary<string, IList<string>> GetRequestProperties()
		{
			Dictionary<string, IList<string>> map = new Dictionary<string, IList<string>>();
			foreach (string key in requestProperties.Keys)
			{
				map.Put(key, Sharpen.Collections.UnmodifiableList(requestProperties.Get(key)));
			}
			return Sharpen.Collections.UnmodifiableMap(map);
		}

		public override string GetRequestProperty(string field)
		{
			IList<string> valuesList = requestProperties.Get(field);
			if (valuesList == null)
			{
				return null;
			}
			return valuesList[0];
		}

		public override void SetRequestProperty(string field, string newValue)
		{
			IList<string> valuesList = new AList<string>();
			valuesList.AddItem(newValue);
			requestProperties.Put(field, valuesList);
		}

		public override string GetHeaderField(int pos)
		{
			try
			{
				GetInputStream();
			}
			catch (IOException)
			{
			}
			// ignore
			if (null == resHeader)
			{
				return null;
			}
			return resHeader.Get(pos);
		}

		public override string GetHeaderField(string key)
		{
			try
			{
				GetInputStream();
			}
			catch (IOException)
			{
			}
			// ignore
			if (null == resHeader)
			{
				return null;
			}
			return resHeader.Get(key);
		}

		public override string GetHeaderFieldKey(int pos)
		{
			try
			{
				GetInputStream();
			}
			catch (IOException)
			{
			}
			// ignore
			if (null == resHeader)
			{
				return null;
			}
			return resHeader.GetKey(pos);
		}

		public override IDictionary<string, IList<string>> GetHeaderFields()
		{
			try
			{
				// ensure that resHeader exists
				GetInputStream();
			}
			catch (IOException)
			{
			}
			// ignore
			if (null == resHeader)
			{
				return null;
			}
			return resHeader.GetFieldMap();
		}

		internal virtual Header GetResHeader()
		{
			if (resHeader == null)
			{
				resHeader = new Header();
			}
			return resHeader;
		}

		public override int GetResponseCode()
		{
			return responseCode;
		}

		internal virtual void SetResponseCode(int responseCode)
		{
			this.responseCode = responseCode;
		}

		internal virtual void SetResponseBody(Body responseBody)
		{
			this.responseBody = responseBody;
		}

		public virtual Body GetResponseBody()
		{
			return this.responseBody;
		}

		internal virtual string GetBaseContentType()
		{
			string type = resHeader.Get("Content-Type");
			if (type == null)
			{
				return null;
			}
			int delimeterPos = type.IndexOf(';');
			if (delimeterPos > 0)
			{
				type = Sharpen.Runtime.Substring(type, delimeterPos);
			}
			return type;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override OutputStream GetOutputStream()
		{
			if (!doOutput)
			{
				throw new ProtocolException("Must set doOutput");
			}
			// you can't write after you read
			if (sentRequest)
			{
				throw new ProtocolException("Can't write after you read");
			}
			if (os != null)
			{
				return os;
			}
			// If the request method is neither PUT or POST, then you're not writing
			if (method != Put && method != Post)
			{
				throw new ProtocolException("Can only write to PUT or POST");
			}
			if (!connected)
			{
				// connect and see if there is cache available.
				Connect();
			}
			return os = new ByteArrayOutputStream();
		}

		public virtual void SetChunked(bool chunked)
		{
			this.chunked = chunked;
		}

		public virtual bool IsChunked()
		{
			return chunked;
		}

		public virtual void SetResponseInputStream(InputStream responseInputStream)
		{
			this.responseInputStream = responseInputStream;
		}

		public virtual InputStream GetResponseInputStream()
		{
			return responseInputStream;
		}

		public virtual void SetResponseOutputStream(OutputStream responseOutputStream)
		{
			this.responseOutputStream = responseOutputStream;
		}

		public virtual OutputStream GetResponseOutputStream()
		{
			return responseOutputStream;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override InputStream GetInputStream()
		{
			return responseInputStream;
		}

		public virtual InputStream GetRequestInputStream()
		{
			return requestInputStream;
		}

		public virtual void SetRequestInputStream(InputStream requestInputStream)
		{
			this.requestInputStream = requestInputStream;
		}
	}

	/// <summary>
	/// Heavily borrowed from Apache Harmony
	/// https://github.com/apache/harmony/blob/trunk/classlib/modules/luni/src/main/java/org/apache/harmony/luni/internal/net/www/protocol/http/Header.java
	/// Under Apache License Version 2.0
	/// </summary>
	internal class Header
	{
		private AList<string> props;

		private SortedDictionary<string, List<string>> keyTable;

		public Header() : base()
		{
			this.props = new AList<string>(20);
			this.keyTable = new SortedDictionary<string, List<string>>(string.CaseInsensitiveOrder
				);
		}

		public Header(IDictionary<string, IList<string>> map) : this()
		{
			// initialize fields
			foreach (KeyValuePair<string, IList<string>> next in map.EntrySet())
			{
				string key = next.Key;
				IList<string> value = next.Value;
				List<string> linkedList = new List<string>();
				foreach (string element in value)
				{
					linkedList.AddItem(element);
					props.AddItem(key);
					props.AddItem(element);
				}
				keyTable.Put(key, linkedList);
			}
		}

		public virtual void Add(string key, string value)
		{
			if (key == null)
			{
				throw new ArgumentNullException();
			}
			if (value == null)
			{
				return;
			}
			List<string> list = keyTable.Get(key);
			if (list == null)
			{
				list = new List<string>();
				keyTable.Put(key, list);
			}
			list.AddItem(value);
			props.AddItem(key);
			props.AddItem(value);
		}

		public virtual void RemoveAll(string key)
		{
			Sharpen.Collections.Remove(keyTable, key);
			for (int i = 0; i < props.Count; i += 2)
			{
				if (key.Equals(props[i]))
				{
					props.Remove(i);
					// key
					props.Remove(i);
				}
			}
		}

		// value
		public virtual void AddAll(string key, IList<string> headers)
		{
			foreach (string header in headers)
			{
				Add(key, header);
			}
		}

		public virtual void AddIfAbsent(string key, string value)
		{
			if (Get(key) == null)
			{
				Add(key, value);
			}
		}

		public virtual void Set(string key, string value)
		{
			RemoveAll(key);
			Add(key, value);
		}

		public virtual IDictionary<string, IList<string>> GetFieldMap()
		{
			IDictionary<string, IList<string>> result = new SortedDictionary<string, IList<string
				>>(string.CaseInsensitiveOrder);
			// android-changed
			foreach (KeyValuePair<string, List<string>> next in keyTable.EntrySet())
			{
				IList<string> v = next.Value;
				result.Put(next.Key, Sharpen.Collections.UnmodifiableList(v));
			}
			return Sharpen.Collections.UnmodifiableMap(result);
		}

		public virtual string Get(int pos)
		{
			if (pos >= 0 && pos < props.Count / 2)
			{
				return props[pos * 2 + 1];
			}
			return null;
		}

		public virtual string GetKey(int pos)
		{
			if (pos >= 0 && pos < props.Count / 2)
			{
				return props[pos * 2];
			}
			return null;
		}

		public virtual string Get(string key)
		{
			List<string> result = keyTable.Get(key);
			if (result == null)
			{
				return null;
			}
			return result.GetLast();
		}

		public virtual int Length()
		{
			return props.Count / 2;
		}
	}
}
