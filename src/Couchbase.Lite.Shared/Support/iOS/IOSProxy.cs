// 
//  IOSProxy.cs
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

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CoreFoundation;
using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Foundation;
using CFNetworkLib = CoreFoundation.CFNetwork;

namespace Couchbase.Lite.Support;

[CouchbaseDependency]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal sealed class IOSProxy : IProxy
{
    private const string Tag = nameof(IOSProxy);

    public Task<WebProxy?> CreateProxyAsync(Uri destination)
    {
        var proxySettings = CFNetworkLib.GetSystemProxySettings();
        if (proxySettings == null) {
            return Task.FromResult(default(WebProxy));
        }

        var proxies = CFNetworkLib.GetProxiesForUri(destination, proxySettings);
        if (proxies == null || proxies.Length == 0) {
            return Task.FromResult(default(WebProxy));
        }

        var proxy = proxies[0];
        var retVal = Task.FromResult(default(WebProxy));
        switch (proxy.ProxyType) {
            case CFProxyType.HTTP:
                retVal = Task.FromResult<WebProxy?>(new(new Uri($"{proxy.HostName}:{proxy.Port}")));
                break;
            case CFProxyType.AutoConfigurationUrl or CFProxyType.AutoConfigurationJavaScript:
                retVal = CreateAutoConfigProxyAsync(proxy, destination,
                    proxy.ProxyType == CFProxyType.AutoConfigurationUrl);
                break;
            default:
                WriteLog.To.Sync.I(Tag, $"Nothing to do for proxy type {proxy.ProxyType}");
                break;
        }

        return retVal;
    }

    private static async Task<WebProxy?> CreateAutoConfigProxyAsync(CFProxy proxy, Uri destination, bool isUrl)
    {
        NSUrl? pacURL = null;
        CFProxy[]? proxies = null;
        NSError? err = null;
        if (isUrl) {
            pacURL = proxy.AutoConfigurationUrl;
            WriteLog.To.Sync.V(Tag, "Resolving proxy PAC script at {0}", pacURL);
            var result = await CFNetworkLib.ExecuteProxyAutoConfigurationUrlAsync(pacURL!, destination,
                CancellationToken.None);
            if (result.error != null) {
                err = result.error;
            } else {
                proxies = result.proxies;
            }
        }
        else {
            WriteLog.To.Sync.V(Tag, "Resolving proxy PAC script");
            var js = proxy.AutoConfigurationJavaScript;
            Debug.Assert(js != null);
            proxies = CFNetworkLib.GetProxiesForAutoConfigurationScript(js, destination);
        }

        if (proxies == null) {
            WriteLog.To.Sync.W(Tag, "Failed to resolve proxy auto configuration script at {0}: {1}",
                pacURL, err?.LocalizedDescription);
            return null;
        }

        if (proxies.Length == 0) {
            WriteLog.To.Sync.W(Tag, "No proxies found in PAC script at {0}",
                pacURL?.AbsoluteString ?? "(inline script)");
            return null;
        }

        return new(new Uri($"{proxies[0].HostName}:{proxies[0].Port}"));
    }
}