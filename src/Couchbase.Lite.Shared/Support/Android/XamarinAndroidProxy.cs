// 
//  XamarinAndroidProxy.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 
#if __ANDROID__ || NET6_0_ANDROID
using System;
using System.Net;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Java.Lang;

namespace Couchbase.Lite.Support
{
    internal sealed class XamarinAndroidProxy : IProxy
    {
        #region IProxy

        public Task<WebProxy> CreateProxyAsync(Uri destination)
        {
            WebProxy webproxy = null;
            // if a proxy is enabled set it up here
            string host = JavaSystem.GetProperty("http.proxyHost")?.TrimEnd('/');
            string port = JavaSystem.GetProperty("http.proxyPort");

            try {
                webproxy = new WebProxy(host, Int32.Parse(port));
            } catch { // UriFormatException
                WriteLog.To.Sync.W("CreateProxyAsync", "The URI formed by combining Host and Port is not a valid URI. Please check your system proxy setting.");
                return Task.FromResult<WebProxy>(null);
            }

            return Task.FromResult(webproxy);
        }

        #endregion
    }
}
#endif