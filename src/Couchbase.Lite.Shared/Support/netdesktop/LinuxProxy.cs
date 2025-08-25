// 
//  LinuxProxy.cs
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

// Windows 2012 doesn't define the more generic variants
#if NETFRAMEWORK || NET462 || NET6_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;

namespace Couchbase.Lite.Support;

[ExcludeFromCodeCoverage]
internal sealed class LinuxProxy : IProxy
{
    private const string Tag = nameof(LinuxProxy);

    private static bool Logged;

    public Task<WebProxy?> CreateProxyAsync(Uri destination)
    {
        if (!Logged) {
            WriteLog.To.Sync.W(Tag, "Linux does not support per URL proxy evaluation");
            Logged = true;
        }

        var proxyAddress = Environment.GetEnvironmentVariable("http_proxy") ?? Environment.GetEnvironmentVariable("all_proxy");
        if (proxyAddress == null) {
            return Task.FromResult(default(WebProxy));
        }

        var ignored = Environment.GetEnvironmentVariable("no_proxy")?.Split(',');
        return Task.FromResult<WebProxy?>(new WebProxy(new Uri(proxyAddress), ignored?.Contains("<local>") == true || ignored?.Contains("*") == true, ignored));
    }
}
#endif