// 
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Apache.Http.Client;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class PersistentCookieStore : CookieStore
	{
		private const string CookieLocalDocName = "PersistentCookieStore";

		private bool omitNonPersistentCookies = false;

		private readonly ConcurrentHashMap<string, Apache.Http.Cookie.Cookie> cookies;

		private WeakReference<Database> dbWeakRef;

		/// <summary>Construct a persistent cookie store.</summary>
		/// <remarks>Construct a persistent cookie store.</remarks>
		public PersistentCookieStore(Database db)
		{
			// Weak reference to a Database.  The reason it's weak is because
			// there's a circular reference, and so the Weakref should avoid
			// the GC being thwarted.
			this.dbWeakRef = new WeakReference<Database>(db);
			cookies = new ConcurrentHashMap();
			// Load any previously stored cookies into the store
			LoadPreviouslyStoredCookies(db);
			// Clear out expired cookies
			ClearExpired(new DateTime());
		}

		private Database GetDb()
		{
			return dbWeakRef.Get();
		}

		private void LoadPreviouslyStoredCookies(Database db)
		{
			try
			{
				IDictionary<string, object> cookiesDoc = db.GetExistingLocalDocument(CookieLocalDocName
					);
				if (cookiesDoc == null)
				{
					return;
				}
				foreach (string name in cookiesDoc.Keys)
				{
					// ignore special couchbase lite fields like _id and _rev
					if (name.StartsWith("_"))
					{
						continue;
					}
					string encodedCookie = (string)cookiesDoc.Get(name);
					if (encodedCookie == null)
					{
						continue;
					}
					Apache.Http.Cookie.Cookie decodedCookie = DecodeCookie(encodedCookie);
					if (decodedCookie == null)
					{
						continue;
					}
					if (!decodedCookie.IsExpired(new DateTime()))
					{
						cookies.Put(name, decodedCookie);
					}
				}
			}
			catch (Exception e)
			{
				Log.E(Log.TagSync, "Exception loading previously stored cookies", e);
			}
		}

		public virtual void AddCookie(Apache.Http.Cookie.Cookie cookie)
		{
			if (omitNonPersistentCookies && !cookie.IsPersistent())
			{
				return;
			}
			string name = cookie.GetName() + cookie.GetDomain();
			// Do we already have this cookie?  If so, don't bother.
			if (cookies.ContainsKey(name) && cookies.Get(name).Equals(cookie))
			{
				return;
			}
			// Save cookie into local store, or remove if expired
			if (!cookie.IsExpired(new DateTime()))
			{
				cookies.Put(name, cookie);
			}
			else
			{
				Sharpen.Collections.Remove(cookies, name);
			}
			string encodedCookie = EncodeCookie(new SerializableCookie(cookie));
			IDictionary<string, object> cookiesDoc = GetDb().GetExistingLocalDocument(CookieLocalDocName
				);
			if (cookiesDoc == null)
			{
				cookiesDoc = new Dictionary<string, object>();
			}
			Log.V(Log.TagSync, "Saving cookie: %s w/ encoded value: %s", name, encodedCookie);
			cookiesDoc.Put(name, encodedCookie);
			try
			{
				GetDb().PutLocalDocument(CookieLocalDocName, cookiesDoc);
			}
			catch (CouchbaseLiteException e)
			{
				Log.E(Log.TagSync, "Exception saving local doc", e);
				throw new RuntimeException(e);
			}
		}

		public virtual void Clear()
		{
			try
			{
				GetDb().PutLocalDocument(CookieLocalDocName, null);
			}
			catch (CouchbaseLiteException e)
			{
				Log.E(Log.TagSync, "Exception saving local doc", e);
				throw new RuntimeException(e);
			}
			// Clear cookies from local store
			cookies.Clear();
		}

		public virtual bool ClearExpired(DateTime date)
		{
			bool clearedAny = false;
			foreach (KeyValuePair<string, Apache.Http.Cookie.Cookie> entry in cookies.EntrySet
				())
			{
				string name = entry.Key;
				Apache.Http.Cookie.Cookie cookie = entry.Value;
				if (cookie.IsExpired(date))
				{
					// Clear cookies from local store
					Sharpen.Collections.Remove(cookies, name);
					DeletePersistedCookie(name);
					// We've cleared at least one
					clearedAny = true;
				}
			}
			return clearedAny;
		}

		private void DeletePersistedCookie(string name)
		{
			IDictionary<string, object> cookiesDoc = GetDb().GetExistingLocalDocument(CookieLocalDocName
				);
			if (cookiesDoc == null)
			{
				return;
			}
			Sharpen.Collections.Remove(cookiesDoc, name);
			try
			{
				GetDb().PutLocalDocument(CookieLocalDocName, cookiesDoc);
			}
			catch (CouchbaseLiteException e)
			{
				Log.E(Log.TagSync, "Exception saving local doc", e);
				throw new RuntimeException(e);
			}
		}

		public virtual IList<Apache.Http.Cookie.Cookie> GetCookies()
		{
			return new ArrayList(cookies.Values);
		}

		/// <summary>
		/// Will make PersistentCookieStore instance ignore Cookies, which are non-persistent by
		/// signature (`Cookie.isPersistent`)
		/// </summary>
		/// <param name="omitNonPersistentCookies">true if non-persistent cookies should be omited
		/// 	</param>
		public virtual void SetOmitNonPersistentCookies(bool omitNonPersistentCookies)
		{
			this.omitNonPersistentCookies = omitNonPersistentCookies;
		}

		/// <summary>Non-standard helper method, to delete cookie</summary>
		/// <param name="cookie">cookie to be removed</param>
		public virtual void DeleteCookie(Apache.Http.Cookie.Cookie cookie)
		{
			string name = cookie.GetName();
			Sharpen.Collections.Remove(cookies, name);
			DeletePersistedCookie(name);
		}

		/// <summary>Serializes Cookie object into String</summary>
		/// <param name="cookie">cookie to be encoded, can be null</param>
		/// <returns>cookie encoded as String</returns>
		internal virtual string EncodeCookie(SerializableCookie cookie)
		{
			if (cookie == null)
			{
				return null;
			}
			ByteArrayOutputStream os = new ByteArrayOutputStream();
			try
			{
				ObjectOutputStream outputStream = new ObjectOutputStream(os);
				outputStream.WriteObject(cookie);
			}
			catch (Exception e)
			{
				Log.E(Log.TagSync, string.Format("encodeCookie failed.  cookie: %s", cookie), e);
				return null;
			}
			return ByteArrayToHexString(os.ToByteArray());
		}

		/// <summary>Returns cookie decoded from cookie string</summary>
		/// <param name="cookieString">string of cookie as returned from http request</param>
		/// <returns>decoded cookie or null if exception occured</returns>
		internal virtual Apache.Http.Cookie.Cookie DecodeCookie(string cookieString)
		{
			Apache.Http.Cookie.Cookie cookie = null;
			try
			{
				byte[] bytes = HexStringToByteArray(cookieString);
				ByteArrayInputStream byteArrayInputStream = new ByteArrayInputStream(bytes);
				ObjectInputStream objectInputStream = new ObjectInputStream(byteArrayInputStream);
				cookie = ((SerializableCookie)objectInputStream.ReadObject()).GetCookie();
			}
			catch (Exception exception)
			{
				Log.D(Log.TagSync, string.Format("decodeCookie failed.  encoded cookie: %s", cookieString
					), exception);
			}
			return cookie;
		}

		/// <summary>
		/// Using some super basic byte array &lt;-&gt; hex conversions so we don't have to rely on any
		/// large Base64 libraries.
		/// </summary>
		/// <remarks>
		/// Using some super basic byte array &lt;-&gt; hex conversions so we don't have to rely on any
		/// large Base64 libraries. Can be overridden if you like!
		/// </remarks>
		/// <param name="bytes">byte array to be converted</param>
		/// <returns>string containing hex values</returns>
		protected internal virtual string ByteArrayToHexString(byte[] bytes)
		{
			StringBuilder sb = new StringBuilder(bytes.Length * 2);
			foreach (byte element in bytes)
			{
				int v = element & unchecked((int)(0xff));
				if (v < 16)
				{
					sb.Append('0');
				}
				sb.Append(Sharpen.Extensions.ToHexString(v));
			}
			return sb.ToString().ToUpper(CultureInfo.InvariantCulture);
		}

		/// <summary>Converts hex values from strings to byte arra</summary>
		/// <param name="hexString">string of hex-encoded values</param>
		/// <returns>decoded byte array</returns>
		protected internal virtual byte[] HexStringToByteArray(string hexString)
		{
			int len = hexString.Length;
			byte[] data = new byte[len / 2];
			for (int i = 0; i < len; i += 2)
			{
				data[i / 2] = unchecked((byte)((char.Digit(hexString[i], 16) << 4) + char.Digit(hexString
					[i + 1], 16)));
			}
			return data;
		}
	}
}
