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
#if __ANDROID__
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Java.Lang;
using Java.Net;

namespace Couchbase.Lite.Support
{
    internal sealed class XamarinAndroidProxy : IProxy
    {
        #region IProxy

        public Task<WebProxy> CreateProxyAsync(Uri destination)
        {
            WebProxy webProxy = null;
            string proxyHost = JavaSystem.GetProperty("http.proxyHost")?.TrimEnd('/');
            if (proxyHost != null) {
                var selector = ProxySelector.Default;
                if (selector != null) {
                    try {
                        var javaUri = new URI(EncodeUrl(destination));
                        var uriSelector = selector.Select(javaUri);
                        var proxy = uriSelector.FirstOrDefault();
                        if (proxy != null && proxy != Proxy.NoProxy && proxy.Address() is InetSocketAddress address) {
                            webProxy = new WebProxy(address.HostString, address.Port);
                        }
                    } catch { // UriFormatException
                        WriteLog.To.Sync.W("CreateProxyAsync", "The URI formed by combining Host and Port is not a valid URI. Please check your system proxy setting.");
                    }
                }
            }
            
            return Task.FromResult(webProxy);
        }

        #endregion

        private string EncodeUrl(Uri url)
        {
            // Copied from https://github.com/xamarin/xamarin-android/blob/master/src/Mono.Android/Xamarin.Android.Net/AndroidClientHandler.cs
            // Fixes an issue where urls with unencoded spaces are not recognized by the Java URI class.

            if (url == null)
                return string.Empty;

            // UriBuilder takes care of encoding everything properly
            var bldr = new UriBuilder(url);
            if (url.IsDefaultPort)
                bldr.Port = -1; // Avoids adding :80 or :443 to the host name in the result

            // bldr.Uri.ToString () would ruin the good job UriBuilder did
            return bldr.ToString();
        }
    }
}
#endif