﻿// 
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
#if __ANDROID__
using System;
using System.Net;
using System.Threading.Tasks;

using Couchbase.Lite.DI;

using Java.Lang;

namespace Couchbase.Lite.Support
{
    internal sealed class XamarinAndroidProxy : IProxy
    {
        #region IProxy

        public Task<WebProxy> CreateProxyAsync(Uri destination)
        {
            // if a proxy is enabled set it up here
            string host = JavaSystem.GetProperty("http.proxyHost")?.TrimEnd('/');
            string port = JavaSystem.GetProperty("http.proxyPort");

            if (!Uri.TryCreate(host, UriKind.RelativeOrAbsolute, out var validUri))
                return Task.FromResult<WebProxy>(null);

            //proxy auth
            //ICredentials credentials = new NetworkCredential("username", "password");
            //WebProxy proxy = new WebProxy(new Uri(host+':'+port), true, null, credentials);
            
            return Task.FromResult(new WebProxy(validUri.Host, Int32.Parse(port)));
        }

        #endregion
    }
}
#endif