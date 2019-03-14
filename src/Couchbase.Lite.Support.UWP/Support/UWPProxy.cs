// 
//  UWPProxy.cs
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
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Windows.Networking.Connectivity;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;

namespace Couchbase.Lite.Support
{
    internal sealed class UWPProxy : IProxy
    {
        #region Constants

        private const string Tag = nameof(UWPProxy);

        #if WINDOWS_PHONE
        private static bool _Logged;
        #endif

        #endregion

        #region IProxy

        public async Task<WebProxy> CreateProxyAsync(Uri destination)
        {
            #if WINDOWS_PHONE 
            if (!_Logged) {
                _Logged = true;
                WriteLog.To.Sync.W(Tag, "Proxy is not supported on Windows phone");
            }

            return null;
            #else
            var config = await NetworkInformation.GetProxyConfigurationAsync(destination);
            if (config.CanConnectDirectly) {
                WriteLog.To.Sync.V(Tag, "Able to connect directly, bypassing proxy");
                return null;
            }

            if (!config.ProxyUris.Any()) {
                WriteLog.To.Sync.V(Tag, "No proxy URLs found for destination");
                return null;
            }

            var uri = config.ProxyUris.FirstOrDefault();
            WriteLog.To.Sync.I(Tag, "Using {0} as proxy server for {1}", uri, destination);
            return new WebProxy(uri);
            #endif
        }

        #endregion
    }
}
