// 
// Activate.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Couchbase.Lite.DI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// The .NET Desktop support class
    /// </summary>
    public static class NetDesktop
    {
        #region Public Methods

        /// <summary>
        /// Activates the support classes for .NET Core / .NET Framework
        /// </summary>
        public static void Activate()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                var codeBase = AppContext.BaseDirectory;
                if (!codeBase.EndsWith("\\")) {
                    codeBase = codeBase + "\\";
                }

                UriBuilder uri = new UriBuilder(codeBase);
                var directory = Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));

                Debug.Assert(Path.IsPathRooted(directory), "directory is not rooted.");
                var architecture = IntPtr.Size == 4
                    ? "x86"
                    : "x64";

                var dllPath = Path.Combine(directory, architecture, "LiteCore.dll");
                var dllPathAsp = Path.Combine(directory, "bin", architecture, "LiteCore.dll");
                var foundPath = default(string);
                foreach (var path in new[] { dllPath, dllPathAsp }) {
                    foundPath = File.Exists(path) ? path : null;
                    if (foundPath != null) {
                        break;
                    }
                }

                if (foundPath == null) {
                    throw new DllNotFoundException("Could not find LiteCore.dll!  Nothing is going to work!");
                }

                const uint loadWithAlteredSearchPath = 8;
                var ptr = LoadLibraryEx(foundPath, IntPtr.Zero, loadWithAlteredSearchPath);
                if (ptr == IntPtr.Zero) {
                    throw new BadImageFormatException("Could not load LiteCore.dll!  Nothing is going to work!");
                }
            }

            Service.RegisterServices(collection =>
            {
                collection.AddSingleton<IDefaultDirectoryResolver, DefaultDirectoryResolver>()
                    .AddSingleton<ISslStreamFactory, SslStreamFactory>()
                    .AddSingleton<ILoggerProvider>(
                        provider => new FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "Logs")));
            });
        }

        #endregion

        #region Private Methods

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        #endregion
    }
}
