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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

using Microsoft.Win32;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// The .NET Desktop support class
    /// </summary>
    public static class NetDesktop
    {
        #region Variables

        private static int _Activated;

        #endregion

        #region Public Methods

        /// <summary>
        /// A sanity check to ensure that the versions of Couchbase.Lite and Couchbase.Lite.Support.NetDesktop match.
        /// These libraries are not independent and must have the exact same version
        /// </summary>
        /// <exception cref="InvalidProgramException">Thrown if Couchbase.Lite and Couchbase.Lite.Support.NetDesktop do not match</exception>
        public static void CheckVersion()
        {
            var version1 = typeof(NetDesktop).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (version1 == null) {
                throw new InvalidProgramException("This version of Couchbase.Lite.Support.NetDesktop has no version!");
            }
            
            var cblAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "Couchbase.Lite");
            if (cblAssembly == null) {
                throw new InvalidProgramException("Couchbase.Lite not detected in app loaded assemblies");
            }

            var version2 = cblAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (!version1.Equals(version2)) {
                throw new InvalidProgramException(
                    $"Mismatch between Couchbase.Lite ({version2?.InformationalVersion ?? "<no version>"}) and Couchbase.Lite.Support.NetDesktop ({version1.InformationalVersion})");
            }
        }

        /// <summary>
        /// Loads the native LiteCore.dll from disk. This is needed on Windows because
        /// the architecture is unknown until runtime and thus the correct dll needs
        /// to be chosen.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Thrown for 32-bit targets, they are not supported</exception>
        /// <exception cref="DllNotFoundException">LiteCore.dll could not be found</exception>
        /// <exception cref="BadImageFormatException">LiteCore.dll was the wrong architecture or corrupted in some way</exception>
        public static void LoadLiteCore()
        {
            if (Interlocked.Exchange(ref _Activated, 1) == 1) {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                var arch = RuntimeInformation.ProcessArchitecture;
                if (arch == Architecture.X86 || arch == Architecture.Arm) {
                    throw new PlatformNotSupportedException("32-bit Windows is no longer supported");
                }

#if NET6_0_OR_GREATER
                var codeBase = Path.GetDirectoryName(typeof(NetDesktop).GetTypeInfo().Assembly.Location);
#else
                var originalCodeBase = typeof(NetDesktop).GetTypeInfo().Assembly.CodeBase;
                var uri = new UriBuilder(originalCodeBase);
                var uriPath = Uri.UnescapeDataString(uri.Path);
                var codeBase = Path.GetDirectoryName(uriPath);
#endif
                if (codeBase == null) {
                    throw new DllNotFoundException(
                        "Couldn't find directory of the loaded support assembly, very weird!");
                }

                var architecture = arch.ToString().ToLowerInvariant();
                var dllPath = Path.Combine(codeBase ?? "", architecture, "LiteCore.dll");
                var dllPathAsp = Path.Combine(codeBase ?? "", "bin", architecture, "LiteCore.dll");
                var dllPathRuntimes =
                    Path.Combine(codeBase ?? "", "runtimes", $"win10-{architecture}", "native", "LiteCore.dll");
                var foundPath = default(string);
                foreach (var path in new[] { dllPathRuntimes, dllPath, dllPathAsp}) {
                    foundPath = File.Exists(path) ? path : null;
                    if (foundPath != null) {
                        break;
                    }
                }

                if (foundPath == null) {
                    throw new DllNotFoundException("Could not find LiteCore.dll!  Nothing is going to work!\r\n" +
                                                   "Tried searching in:\r\n" +
                                                   $"{dllPathRuntimes}\r\n" +
                                                   $"{dllPath}\r\n" +
                                                   $"{dllPathAsp}\r\n");
                }

                const uint loadWithAlteredSearchPath = 8;
                var ptr = LoadLibraryEx(foundPath, IntPtr.Zero, loadWithAlteredSearchPath);
                if (ptr == IntPtr.Zero) {
                    if (CheckVSRedist(architecture)) {
                        throw new BadImageFormatException(
                            "Could not load LiteCore.dll!  Nothing is going to work!\r\n" +
                            $"LiteCore found in: ${foundPath}");
                    }

                    throw new DllNotFoundException("LiteCore.dll failed to load!  Please ensure that the\r\n" +
                        "Visual Studio 2022 C++ runtime is installed from https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads");
                }
            }
        }

        #endregion

        #region Private Methods
        #if NET6_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif
        private static bool CheckVSRedist(string architecture)
        {
            // https://stackoverflow.com/a/34209692/1155387
            var key = Registry.ClassesRoot.OpenSubKey(@"Installer\Dependencies", false);
            var subkeys = key?.GetSubKeyNames();
            if(subkeys == null) {
                Console.WriteLine("Unable to find proper registry key!");
                return false;
            }

            foreach (var subkey in subkeys.Where(x => x.StartsWith("VC,redist"))) {
                var keyComponents = subkey.Split(',');
                if (keyComponents.Length != 5) {
                    Console.WriteLine($"Unexpected registry key format {subkey}...");
                    continue;
                }

                if (!keyComponents[1].EndsWith(architecture)) {
                    Console.WriteLine($"Found C++ runtime, but architecture wrong ({keyComponents[1]} is not valid for {architecture})");
                    continue;
                }

                if (!Single.TryParse(keyComponents[3], out var version)) {
                    Console.WriteLine($"Unparseable version found: {keyComponents[3]}");
                    continue;
                }

                if (version < 14.30) {
                    Console.WriteLine($"Found C++ runtime, but version too old ({keyComponents[3]} < 14.30). Please ensure that the\r\n" +
                        "Visual Studio 2022 C++ runtime is installed from https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads");
                    continue;
                }

                return true;
            }

            return false;

        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

#endregion
    }
}