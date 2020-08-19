﻿// 
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
        /// Activates the support classes for .NET Core / .NET Framework
        /// </summary>
        /// <exception cref="InvalidProgramException">Thrown if Couchbase.Lite and Couchbase.Lite.Support.NetDesktop do not match</exception>
        [Obsolete("This method is no longer needed, and will be removed in 3.0")]
        public static void Activate()
        {
        }

        /// <summary>
        /// A sanity check to ensure that the versions of Couchbase.Lite and Couchbase.Lite.Support.NetDesktop match.
        /// These libraries are not independent and must have the exact same version
        /// </summary>
        /// <exception cref="InvalidProgramException">Thrown if Couchbase.Lite and Couchbase.Lite.Support.NetDesktop do not match</exception>
        public static void CheckVersion()
        {
            var version1 = typeof(NetDesktop).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (version1 == null) {
                throw new InvalidProgramException("This version of Couchbase.Lite.Support.iOS has no version!");
            }
            
            var cblAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "Couchbase.Lite");
            if (cblAssembly == null) {
                throw new InvalidProgramException("Couchbase.Lite not detected in app loaded assemblies");
            }

            var version2 = cblAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (!version1.Equals(version2)) {
                throw new InvalidProgramException(
                    $"Mismatch between Couchbase.Lite ({version2.InformationalVersion}) and Couchbase.Lite.Support.NetDesktop ({version1.InformationalVersion})");
            }
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
        public static void EnableTextLogging(string directoryPath)
        {
            var dbType = Type.GetType("Couchbase.Lite.Database, Couchbase.Lite");
            var logType = Type.GetType("Couchbase.Lite.Logging.Log, Couchbase.Lite");
            var fileLoggerType = Type.GetType("Couchbase.Lite.Logging.FileLogger, Couchbase.Lite");

            var logObject = dbType.GetProperty("Log", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var fileLoggerObject = logType.GetProperty("File", BindingFlags.Public | BindingFlags.Instance).GetValue(logObject);
            var configProperty = fileLoggerType.GetProperty("Config", BindingFlags.Public | BindingFlags.Instance);
            if (directoryPath == null) {
                configProperty.SetValue(fileLoggerObject, null);
            } else {
                var logFileConfigType = Type.GetType("Couchbase.Lite.Logging.LogFileConfiguration, Couchbase.Lite");
                var constructor = logFileConfigType.GetConstructor(new[] {typeof(string), logFileConfigType});
                var oldConfig = configProperty.GetValue(fileLoggerObject);
                var newConfig = constructor.Invoke(new[] {directoryPath, oldConfig});
                var usePlaintextProperty = logFileConfigType.GetProperty("UsePlaintext", BindingFlags.Public | BindingFlags.Instance);
                usePlaintextProperty.SetValue(newConfig, true);
                configProperty.SetValue(fileLoggerObject, newConfig);
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
            var dbType = Type.GetType("Couchbase.Lite.Database, Couchbase.Lite");
            var logType = Type.GetType("Couchbase.Lite.Logging.Log, Couchbase.Lite");
            var fileLoggerType = Type.GetType("Couchbase.Lite.Logging.FileLogger, Couchbase.Lite");

            var logObject = dbType.GetProperty("Log", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var fileLoggerObject = logType.GetProperty("File", BindingFlags.Public | BindingFlags.Instance).GetValue(logObject);
            var configProperty = fileLoggerType.GetProperty("Config", BindingFlags.Public | BindingFlags.Instance);
            if (directoryPath == null) {
                configProperty.SetValue(fileLoggerObject, null);
            } else {
                var logFileConfigType = Type.GetType("Couchbase.Lite.Logging.LogFileConfiguration, Couchbase.Lite");
                var constructor = logFileConfigType.GetConstructor(new[] {typeof(string), logFileConfigType});
                var oldConfig = configProperty.GetValue(fileLoggerObject);
                var newConfig = constructor.Invoke(new[] {directoryPath, oldConfig});
                configProperty.SetValue(fileLoggerObject, newConfig);
            }
        }

        #endregion

        #region Internal Methods

        internal static void LoadLiteCore()
        {
            if (Interlocked.Exchange(ref _Activated, 1) == 1) {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                var codeBase = Path.GetDirectoryName(typeof(NetDesktop).GetTypeInfo().Assembly.Location);
                if (codeBase == null) {
                    throw new DllNotFoundException(
                        "Couldn't find directory of the loaded support assembly, very weird!");
                }

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
                foreach (var path in new[] {dllPathNuget, dllPath, dllPathAsp}) {
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
                    if (CheckVSRedist(architecture)) {
                        throw new BadImageFormatException(
                            "Could not load LiteCore.dll!  Nothing is going to work!\r\n" +
                            $"LiteCore found in: ${foundPath}");
                    }

                    throw new DllNotFoundException("LiteCore.dll failed to load!  Please ensure that the Visual\r\n" +
                                                   "Studio 2015-2019 C++ runtime is installed from https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads");
                }
            }
        }

        #endregion

        #region Private Methods

        private static bool CheckVSRedist(string architecture)
        {
            // https://stackoverflow.com/a/34209692/1155387
            var key = Registry.ClassesRoot.OpenSubKey(@"Installer\Dependencies", false);
            var subkeys = key.GetSubKeyNames();
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

                if (version < 14.10) {
                    Console.WriteLine($"Found C++ runtime, but version too old ({keyComponents[3]} < 14.10)");
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