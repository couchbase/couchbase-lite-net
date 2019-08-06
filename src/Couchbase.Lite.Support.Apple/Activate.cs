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
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// Support classes for Xamarin iOS
    /// </summary>
    public static class iOS
    {
        #region Public Methods

        /// <summary>
        /// Activates the Xamarin iOS specific support classes
        /// </summary>
        /// <exception cref="InvalidProgramException">Thrown if Couchbase.Lite and Couchbase.Lite.Support.iOS do not match</exception>
        [Obsolete("This call is no longer needed, and will be removed in 3.0")]
        public static void Activate()
        {
        }

        /// <summary>
        /// A sanity check to ensure that the versions of Couchbase.Lite and Couchbase.Lite.Support.iOS match.
        /// These libraries are not independent and must have the exact same version
        /// </summary>
        /// <exception cref="InvalidProgramException">Thrown if Couchbase.Lite and Couchbase.Lite.Support.iOS do not match</exception>
        public static void CheckVersion()
        {
            var version1 = typeof(iOS).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
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
                    $"Mismatch between Couchbase.Lite ({version2.InformationalVersion}) and Couchbase.Lite.Support.iOS ({version1.InformationalVersion})");
            }
        }

        /// <summary>
        /// [DEPRECATED] Enables text based logging for debugging purposes.  Log statements will
        /// be written to NSLog
        /// </summary>
        [Obsolete("This has been superceded by Database.Log.Console.  It is a no-op now")]
        public static void EnableTextLogging()
        {
            Console.WriteLine("CouchbaseLite Warning:  EnableTextLogging is now a no-op!");
        }

        #endregion
    }
}