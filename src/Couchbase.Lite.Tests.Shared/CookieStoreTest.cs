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
using Couchbase.Lite.Util;
using System.IO;
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
        public const string Tag = "CookieStoreTest";

        private DirectoryInfo GetCookiesDirectory()
        {
            return new DirectoryInfo(Path.Combine(manager.Directory, "test"));
        }

        private void CleanUpCookiesDirectory()
        {
            Directory.Delete(GetCookiesDirectory().FullName, true);
            Assert.AreEqual(false, GetCookiesDirectory().Exists);
        }

        [TearDown]
        protected void TearDown()
        {
            CleanUpCookiesDirectory();
        }

        [Test]
        public void TestSaveCookieStore()
        {
            var cookieStore = new CookieStore(GetCookiesDirectory().FullName);
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

            cookieStore = new CookieStore(GetCookiesDirectory().FullName);
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
            var cookieStore = new CookieStore(GetCookiesDirectory().FullName);
            Assert.AreEqual(0, cookieStore.Count);
            cookieStore.Save();

            cookieStore = new CookieStore(GetCookiesDirectory().FullName);
            Assert.AreEqual(0, cookieStore.Count);
        }
    }
}
