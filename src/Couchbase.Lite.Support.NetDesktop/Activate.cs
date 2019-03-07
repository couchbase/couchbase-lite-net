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
        [Obsolete("This method is no longer needed, and will be removed in 3.0")]
        public static void Activate()
        {
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
                    if (CheckVS2015Redist()) {
                        throw new BadImageFormatException(
                            "Could not load LiteCore.dll!  Nothing is going to work!\r\n" +
                            $"LiteCore found in: ${foundPath}");
                    }

                    throw new DllNotFoundException("LiteCore.dll failed to load!  Please ensure that the Visual\r\n" +
                                                   "Studio 2015 C++ runtime is installed from https://www.microsoft.com/en-us/download/details.aspx?id=48145");
                }
            }
        }

        #endregion

        #region Private Methods

        private static bool CheckVS2015Redist()
        {
            // https://github.com/bitbeans/RedistributableChecker/blob/master/RedistributableChecker/RedistributablePackage.cs
            var id = IntPtr.Size == 4
                ? "{e2803110-78b3-4664-a479-3611a381656a}" // vs2015x86
                : "{d992c12e-cab2-426f-bde3-fb8c53950b0d}"; // vs2015x64
            var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Classes\Installer\Dependencies\{id}", false);
            var version = key?.GetValue("Version");
            return ((string) version)?.StartsWith("14") == true;
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        #endregion
    }
}