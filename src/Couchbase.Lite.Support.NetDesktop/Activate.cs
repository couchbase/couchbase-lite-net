// 
//  Activate.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// The .NET Desktop support class
    /// </summary>
    public static class NetDesktop
    {
        #region Variables

        private static AtomicBool _Activated;

        #endregion

        #region Public Methods

        /// <summary>
        /// Activates the support classes for .NET Core / .NET Framework
        /// </summary>
        public static void Activate()
        {
            if (_Activated.Set(true)) {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
#if NETCOREAPP2_0 || NET461
                var codeBase = Path.GetDirectoryName(typeof(NetDesktop).GetTypeInfo().Assembly.Location);
                if (codeBase == null) {
                    throw new DllNotFoundException(
                        "Couldn't find directory of the loaded support assembly, very weird!");
                }
#else
                var codeBase = AppContext.BaseDirectory;
#endif

                var architecture = IntPtr.Size == 4
                    ? "x86"
                    : "x64";
                
                var nugetBase = codeBase;
                for (int i = 0; i < 2; i++) {
                    nugetBase = Path.GetDirectoryName(nugetBase);
                }

                var dllPath = Path.Combine(codeBase, architecture, "LiteCore.dll");
                var dllPathAsp = Path.Combine(codeBase, "bin", architecture, "LiteCore.dll");
                var dllPathNuget =
                    Path.Combine(nugetBase, "runtimes", $"win7-{architecture}", "native", "LiteCore.dll");
                var foundPath = default(string);
                foreach (var path in new[] { dllPathNuget, dllPath, dllPathAsp }) {
                    foundPath = File.Exists(path) ? path : null;
                    if (foundPath != null) {
                        break;
                    }
                }

                if (foundPath == null) {
                    throw new DllNotFoundException("Could not find LiteCore.dll!  Nothing is going to work!\r\n" +
                                                   "Tried searching in:\r\n" +
                                                   $"{dllPathNuget}\r\n" +
                                                   $"{dllPath}\r\n" +
                                                   $"{dllPathAsp}\r\n");
                }

                const uint loadWithAlteredSearchPath = 8;
                var ptr = LoadLibraryEx(foundPath, IntPtr.Zero, loadWithAlteredSearchPath);
                if (ptr == IntPtr.Zero) {
                    throw new BadImageFormatException("Could not load LiteCore.dll!  Nothing is going to work!\r\n" +
                                                      $"LiteCore found in: ${foundPath}");
                }
            }

            Service.AutoRegister(typeof(NetDesktop).GetTypeInfo().Assembly);

            Service.Register<ILiteCore>(new LiteCoreImpl());
            Service.Register<ILiteCoreRaw>(new LiteCoreRawImpl());
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Service.Register<IProxy>(new WindowsProxy());
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Service.Register<IProxy>(new MacProxy());
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                Service.Register<IProxy>(new LinuxProxy());
            }

            Database.Log.Console = new DesktopConsoleLogger();
        }

        /// <summary>
        /// <para>[DEPRECATED] Turns on text based logging for debugging purposes.  The logs will be written 
        /// to the directory specified in <paramref name="directoryPath"/>.  It is equivalent to setting
        /// a configuration with <c>UsePlaintext</c> set to <c>true</c> on <c>Database.Log.File.Config</c></para>
        ///
        /// <para>This will override binary
        /// logging.  It is not recommended to use this method anymore, but to use <c>Database.Log.Console</c>
        /// to get information to the console, or <c>Database.Log.Custom</c> to set up custom logging logic
        /// </para>
        /// </summary>
        /// <param name="directoryPath">The directory to write logs to</param>
        [Obsolete("This has been superseded by new logging logic.  See doc comments for details.")]
        public static void EnableTextLogging([NotNull]string directoryPath)
        {
            if (directoryPath == null) {
                Database.Log.File.Config = null;
            } else {
                Database.Log.File.Config = new LogFileConfiguration(directoryPath, Database.Log.File.Config)
                {
                    UsePlaintext = true
                };
            }
        }

        /// <summary>
        /// [DEPRECATED] Directs the binary log files to write to the specified directory.  Useful if
        /// the default directory does not have write permission.
        /// </summary>
        /// <param name="directoryPath">The path to write binary logs to</param>
        [Obsolete("This has been superseded by Database.Log.File.Directory.")]
        public static void SetBinaryLogDirectory(string directoryPath)
        {
            if (directoryPath == null) {
                Database.Log.File.Config = null;
            } else {
                Database.Log.File.Config = new LogFileConfiguration(directoryPath, Database.Log.File.Config);
            }
        }

        #endregion

        #region Private Methods

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        #endregion
    }
}