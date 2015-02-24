//
// CookieStore.cs
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
using System.Linq;
using System.Net;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Couchbase.Lite.Util
{
    public class CookieStore : CookieContainer
    {
        const string FileName = "cookies.json";

        readonly Object locker = new Object();

        readonly DirectoryInfo directory;

        private HashSet<Uri> _cookieUriReference = new HashSet<Uri>();

        #region Constructors

        public CookieStore() : this (null) { }

        public CookieStore(String directory) 
        {
            if (directory != null)
            {
                this.directory = new DirectoryInfo(directory);
            }
            DeserializeFromDisk();
        }

        #endregion

        #region Public
        public new void Add(CookieCollection cookies)
        {
            base.Add(cookies);
            foreach(Cookie cookie in cookies)
            {
                var urlString = String.Format("http://{0}{1}", cookie.Domain, cookie.Path);
                _cookieUriReference.Add(new Uri(urlString));
            }
            Save();
        }

        public new void Add(Cookie cookie)
        {
            base.Add(cookie);
            var urlString = String.Format("http://{0}{1}", cookie.Domain, cookie.Path);
            _cookieUriReference.Add(new Uri(urlString));
        }

        public void Delete(Uri uri, string name)
        {
            if (uri == null || name == null)
            {
                return;
            }

            lock (locker) 
            {
                var delete = false;
                var cookies = GetCookies(uri);
                foreach (Cookie cookie in cookies)
                {
                    if (name.Equals(cookie.Name))
                    {
                        cookie.Discard = true;
                        cookie.Expired = true;
                        cookie.Expires = DateTime.Now.Subtract(TimeSpan.FromDays(2));

                        if (!delete)
                        {
                            delete = true;
                        }
                    }
                }

                if (delete)
                {
                    // Trigger container cookie list refreshment
                    GetCookies(uri);
                    Save();
                }
            }
        }

        public void Save()
        {
            lock(locker)
            {
                SerializeToDisk();
            }
        }

        #endregion

        #region Private

        private string GetSaveCookiesFilePath()
        {
            if (directory == null)
            {
                return null;
            }

            if (!directory.Exists)
            {
                directory.Create();
                directory.Refresh();
            }

            return Path.Combine(directory.FullName, FileName);
        }

        private void SerializeToDisk()
        {
            var filePath = GetSaveCookiesFilePath();
            if (String.IsNullOrEmpty(filePath.Trim()))
            {
                return;
            }

            List<Cookie> aggregate = new List<Cookie>();
            foreach(var uri in _cookieUriReference)
            {
                var collection = GetCookies(uri);
                aggregate.AddRange(collection.Cast<Cookie>());
            }

            using (var writer = new StreamWriter(filePath))
            {
                var json = JsonConvert.SerializeObject(aggregate);
                writer.Write(json);
            }
        }

        private void DeserializeFromDisk()
        {
            var filePath = GetSaveCookiesFilePath();
            if (String.IsNullOrEmpty(filePath.Trim()))
            {
                return;
            }

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return;
            }

            using (var reader = new StreamReader(filePath))
            {
                var json = reader.ReadToEnd();

                var cookies = JsonConvert.DeserializeObject<List<Cookie>>(json);
                foreach(Cookie cookie in cookies)
                {
                    Add(cookie);
                }
            }
        }

        #endregion
    }
}

