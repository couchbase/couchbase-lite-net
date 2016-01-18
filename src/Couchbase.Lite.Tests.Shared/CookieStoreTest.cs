//
// CookieStoreTest.cs
//
// Author:
//     Pasin Suriyentrakorn  <pasin@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc
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
//

using System;
using System.IO;

using Couchbase.Lite.Util;
using NUnit.Framework;

#if NET_3_5
using System.Net.Couchbase;
#else
using System.Net;
#endif

namespace Couchbase.Lite
{
    public class CookieStoreTest : LiteTestCase
    {
        public const string TAG = "CookieStoreTest";

        public CookieStoreTest(string storageType) : base(storageType) {}

        [Test]
        public void TestSetCookiePersistent()
        {
            var cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);

            var cookie1 = new Cookie("whitechoco", "sweet", "/", "mycookie.com") {
                Comment = "yummy",
                CommentUri = new Uri("http://www.mycookie.com"),
                Expires = DateTime.Now.AddSeconds(60),
            };
            cookieStore.Add(cookie1);

            var cookie2 = new Cookie("darkchoco", "sweet", "/", "mycookie.com") {
                Comment = "yummy",
                CommentUri = new Uri("http://www.mycookie.com"),
                Expires = DateTime.Now.AddSeconds(60)
            };
            cookieStore.Add(cookie2);

            var cookies = cookieStore.GetCookies(new Uri("http://mycookie.com"));
            Assert.AreEqual(2, cookieStore.Count);
            Assert.AreEqual(cookie1, cookies[0]);
            Assert.AreEqual(cookie2, cookies[1]);

            // Set cookie with same name, domain, and path
            // with one of the previously set cookies:
            var cookie3 = new Cookie("darkchoco", "bitter sweet", "/", "mycookie.com") {
                Comment = "yummy",
                CommentUri = new Uri("http://www.mycookie.com"),
                Expires = DateTime.Now.AddSeconds(60)
            };
            cookieStore.Add(cookie3);
            Assert.AreEqual(2, cookieStore.Count);
            cookies = cookieStore.GetCookies(new Uri("http://mycookie.com"));
            Assert.AreEqual(cookie1, cookies[0]);
            Assert.AreEqual(cookie3, cookies[1]);

            cookieStore = new CookieStore(database, "cookie_store_unit_test");
            cookies = cookieStore.GetCookies(new Uri("http://mycookie.com"));
            Assert.AreEqual(2, cookieStore.Count);
            Assert.AreEqual(cookie1, cookies[0]);
            Assert.AreEqual(cookie3, cookies[1]);
        }

        [Test]
        public void TestSetCookieNameDomainPath()
        {
            var cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);
            var cookie1 = new Cookie("cookie1", "sweet", "/", "mycookie.com");
            cookieStore.Add(cookie1);
            var cookie2 = new Cookie("cookie1", "sweet", "", "mycookie.com");
            cookieStore.Add(cookie2);
            var cookie3 = new Cookie("cookie1", "sweet", "/path", "mycookie.com");
            cookieStore.Add(cookie3);
            var cookie4 = new Cookie("cookie1", "sweet", "/path/", "mycookie.com");
            cookieStore.Add(cookie4);
            var cookie5 = new Cookie("cookie1", "sweet", "/", "www.mycookie.com");
            cookieStore.Add(cookie5);
            var cookie6 = new Cookie("cookie7", "sweet", "/", "www.mycookie.com");
            cookieStore.Add(cookie6);

            Assert.AreEqual(6, cookieStore.Count);
            var cookies = cookieStore.GetCookies(new Uri("http://www.mycookie.com/path/"));
            Assert.AreEqual(6, cookies.Count);
            CollectionAssert.Contains(cookies, cookie1);
            CollectionAssert.Contains(cookies, cookie2);
            CollectionAssert.Contains(cookies, cookie3);
            CollectionAssert.Contains(cookies, cookie4);
            CollectionAssert.Contains(cookies, cookie5);
            CollectionAssert.Contains(cookies, cookie6);

            var cookie8 = new Cookie("cookie1", "bitter. sweet", "/", "mycookie.com");
            cookieStore.Add(cookie8);

            Assert.AreEqual(6, cookieStore.Count);
            cookies = cookieStore.GetCookies(new Uri("http://www.mycookie.com/path/"));
            Assert.AreEqual(6, cookies.Count);
            CollectionAssert.DoesNotContain(cookies, cookie1);
            CollectionAssert.Contains(cookies, cookie8);
        }

        [Test]
        public void TestSetCookieSessionOnly()
        {
            var cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);

            // No expires date specified for a cookie v0:
            var cookie1 = new Cookie("whitechoco", "sweet", "/", "mycookie.com");
            Assert.AreEqual(DateTime.MinValue, cookie1.Expires);
            cookieStore.Add(cookie1);

            // No max age specified for a cookie v1:
            var cookie2 = new Cookie("oatmeal_raisin", "sweet", "/", ".mycookie.com") { Version = 1 };
            Assert.AreEqual(DateTime.MinValue, cookie2.Expires);
            cookieStore.Add(cookie2);

            Assert.AreEqual(2, cookieStore.Count);
            var cookies = cookieStore.GetCookies(new Uri("http://mycookie.com"));
            Assert.AreEqual(cookie1, cookies[0]);
            Assert.AreEqual(cookie2, cookies[1]);

            cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);
        }

        [Test]
        public void TestDeleteCookie()
        {
            var cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);

            var cookie1 = new Cookie("whitechoco", "sweet", "/", "mycookie.com") {
                Expires = DateTime.Now.AddSeconds(3600),
            };
            cookieStore.Add(cookie1);

            var cookie2 = new Cookie("oatmeal_raisin", "sweet", "/", "mycookie.com") {
                Expires = DateTime.Now.AddSeconds(3600),
            };
            cookieStore.Add(cookie2);

            var cookie3 = new Cookie("darkchoco", "sweet", "/", "mycookie.com") {
                Expires = DateTime.Now.AddSeconds(3600),
            };
            cookieStore.Add(cookie3);

            Assert.AreEqual(3, cookieStore.Count);
            var cookies = cookieStore.GetCookies(new Uri("http://mycookie.com"));
            CollectionAssert.Contains(cookies, cookie1);
            CollectionAssert.Contains(cookies, cookie2);
            CollectionAssert.Contains(cookies, cookie3);

            cookieStore.Delete(new Uri("http://mycookie.com"), cookie2.Name);
            Assert.AreEqual(2, cookieStore.Count);
            cookies = cookieStore.GetCookies(new Uri("http://mycookie.com"));
            CollectionAssert.Contains(cookies, cookie1);
            CollectionAssert.Contains(cookies, cookie3);

            cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(2, cookieStore.Count);
            cookies = cookieStore.GetCookies(new Uri("http://mycookie.com"));
            CollectionAssert.Contains(cookies, cookie1);
            CollectionAssert.Contains(cookies, cookie3);

            cookieStore.Delete(new Uri("http://mycookie.com"), cookie1.Name);
            cookieStore.Delete(new Uri("http://mycookie.com"), cookie3.Name);
            Assert.AreEqual(0, cookieStore.Count);

            cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);
        }

        [Test]
        public void TestCookieExpires()
        {
            var cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);

            var cookie1 = new Cookie("whitechoco", "sweet", "/", ".mycookie.com") {
                Expires = DateTime.Now.AddSeconds(1),
                Version = 1
            };
            cookieStore.Add(cookie1);

            Assert.AreEqual(1, cookieStore.Count);
            var cookies = cookieStore.GetCookies(new Uri("http://mycookie.com"));
            Assert.AreEqual(1, cookies.Count);
            Assert.AreEqual(cookie1, cookies[0]);

            Sleep(1500);

            Assert.AreEqual(0, cookieStore.Count);
        }

        [Test]
        public void TestSaveCookieStore()
        {
            var cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);

            var name = "foo";
            var value = "bar";
            var uri = new Uri("http://foo.com/baz");
            var domain = uri.Host;
            var path = uri.PathAndQuery;
            var httpOnly = false;
            var isSecure = false;
            var expires = DateTime.Now.Add(TimeSpan.FromDays(1));

            var cookie = new Cookie(name, value);
            cookie.Path = path;
            cookie.Domain = domain;
            cookie.HttpOnly = httpOnly;
            cookie.Secure = isSecure;
            cookie.Expires = expires;

            cookieStore.Add(cookie);
            cookieStore.Save();

            cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(1, cookieStore.Count);

            var cookies = cookieStore.GetCookies(uri);
            Assert.AreEqual(1, cookies.Count);
            Assert.AreEqual(name, cookies[0].Name);
            Assert.AreEqual(value, cookies[0].Value);
            Assert.AreEqual(path, cookies[0].Path);
            Assert.AreEqual(domain, cookies[0].Domain);
            Assert.AreEqual(expires, cookies[0].Expires);
        }

        [Test]
        public void TestSaveEmptyCookieStore()
        {
            var cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);
            cookieStore.Save();

            cookieStore = new CookieStore(database, "cookie_store_unit_test");
            Assert.AreEqual(0, cookieStore.Count);
        }
    }
}
