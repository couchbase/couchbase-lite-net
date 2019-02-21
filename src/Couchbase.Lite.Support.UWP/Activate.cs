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
using System.Diagnostics;
using System.Reflection;

using Couchbase.Lite.DI;
using Couchbase.Lite.Util;

using LiteCore.Interop;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// The UWP support class
    /// </summary>
    public static class UWP
    {
        #region Variables

        private static AtomicBool _Activated;

        #endregion

        #region Public Methods

        /// <summary>
        /// Activates the support classes for UWP
        /// </summary>
        public static void Activate()
        {
            if(_Activated.Set(true)) {
                return;
            }

            var version1 = typeof(UWP).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var version2 = typeof(Database).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            if (!version1.Equals(version2)) {
                throw new InvalidOperationException(
                    $"Mismatch between Couchbase.Lite and Couchbase.Lite.Support.UWP ({version2.InformationalVersion} vs {version1.InformationalVersion})");
            }
            
            Service.AutoRegister(typeof(UWP).GetTypeInfo().Assembly);
            Service.Register<ILiteCore>(new LiteCoreImpl());
            Service.Register<ILiteCoreRaw>(new LiteCoreRawImpl());
            Service.Register<IProxy>(new UWPProxy());
            Database.Log.Console = new UwpConsoleLogger();
        }

        /// <summary>
        /// [DEPRECATED] Enables text based logging for debugging.  Logs will be written to
        /// a file in the "Logs" folder inside of the application package's
        /// local files directory
        /// </summary>
        [Obsolete("This has been superceded by Database.Log.Console.  It is a no-op now")]
        public static void EnableTextLogging()
        {
            Debug.WriteLine("CouchbaseLite Warning:  EnableTextLogging() is now a no-op");
        }

        #endregion
    }
}
