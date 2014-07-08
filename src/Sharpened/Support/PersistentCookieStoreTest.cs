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
using System.Collections.Generic;
using Apache.Http.Client;
using Apache.Http.Impl.Cookie;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class PersistentCookieStoreTest : LiteTestCase
	{
		/// <exception cref="System.Exception"></exception>
		public virtual void TestEncodeDecodeCookie()
		{
			PersistentCookieStore cookieStore = new PersistentCookieStore(database);
			string cookieName = "foo";
			string cookieVal = "bar";
			bool isSecure = false;
			bool httpOnly = false;
			string cookiePath = "baz";
			string cookieDomain = "foo.com";
			// expiration date - 1 day from now
			Calendar cal = Calendar.GetInstance();
			cal.SetTime(new DateTime());
			int numDaysToAdd = 1;
			cal.Add(Calendar.Date, numDaysToAdd);
			DateTime expirationDate = cal.GetTime();
			BasicClientCookie cookie = new BasicClientCookie(cookieName, cookieVal);
			cookie.SetExpiryDate(expirationDate);
			cookie.SetSecure(isSecure);
			cookie.SetDomain(cookieDomain);
			cookie.SetPath(cookiePath);
			string encodedCookie = cookieStore.EncodeCookie(new SerializableCookie(cookie));
			Apache.Http.Cookie.Cookie fetchedCookie = cookieStore.DecodeCookie(encodedCookie);
			NUnit.Framework.Assert.AreEqual(cookieName, fetchedCookie.GetName());
			NUnit.Framework.Assert.AreEqual(cookieVal, fetchedCookie.GetValue());
			NUnit.Framework.Assert.AreEqual(expirationDate, fetchedCookie.GetExpiryDate());
			NUnit.Framework.Assert.AreEqual(cookiePath, fetchedCookie.GetPath());
			NUnit.Framework.Assert.AreEqual(cookieDomain, fetchedCookie.GetDomain());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPersistentCookiestore()
		{
			CookieStore cookieStore = new PersistentCookieStore(database);
			NUnit.Framework.Assert.AreEqual(0, cookieStore.GetCookies().Count);
			string cookieName = "foo";
			string cookieVal = "bar";
			bool isSecure = false;
			bool httpOnly = false;
			string cookiePath = "baz";
			string cookieDomain = "foo.com";
			// expiration date - 1 day from now
			Calendar cal = Calendar.GetInstance();
			cal.SetTime(new DateTime());
			int numDaysToAdd = 1;
			cal.Add(Calendar.Date, numDaysToAdd);
			DateTime expirationDate = cal.GetTime();
			BasicClientCookie cookie = new BasicClientCookie(cookieName, cookieVal);
			cookie.SetExpiryDate(expirationDate);
			cookie.SetSecure(isSecure);
			cookie.SetDomain(cookieDomain);
			cookie.SetPath(cookiePath);
			cookieStore.AddCookie(cookie);
			CookieStore cookieStore2 = new PersistentCookieStore(database);
			NUnit.Framework.Assert.AreEqual(1, cookieStore.GetCookies().Count);
			IList<Apache.Http.Cookie.Cookie> fetchedCookies = cookieStore.GetCookies();
			Apache.Http.Cookie.Cookie fetchedCookie = fetchedCookies[0];
			NUnit.Framework.Assert.AreEqual(cookieName, fetchedCookie.GetName());
			NUnit.Framework.Assert.AreEqual(cookieVal, fetchedCookie.GetValue());
			NUnit.Framework.Assert.AreEqual(expirationDate, fetchedCookie.GetExpiryDate());
			NUnit.Framework.Assert.AreEqual(cookiePath, fetchedCookie.GetPath());
			NUnit.Framework.Assert.AreEqual(cookieDomain, fetchedCookie.GetDomain());
		}
	}
}
