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
using System.Reflection;

using Couchbase.Lite.DI;
using Couchbase.Lite.Util;

using LiteCore.Interop;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// Support classes for Xamarin iOS
    /// </summary>
    public static class iOS
	{
        #region Variables

        private static AtomicBool _Activated = false;

        #endregion

        #region Public Methods

        /// <summary>
		/// Activates the Xamarin iOS specific support classes
		/// </summary>
		public static void Activate()
		{
            if(_Activated.Set(true)) {
                return;
            }

            var version1 = typeof(iOS).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var version2 = typeof(Database).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            if (!version1.Equals(version2)) {
                throw new InvalidOperationException(
                    $"Mismatch between Couchbase.Lite and Couchbase.Lite.Support.iOS ({version2.InformationalVersion} vs {version1.InformationalVersion})");
            }
            
			Console.WriteLine("Loading support items");
            Service.AutoRegister(typeof(iOS).Assembly);
            Service.Register<ILiteCore>(new LiteCoreImpl());
            Service.Register<ILiteCoreRaw>(new LiteCoreRawImpl());
            Service.Register<IProxy>(new IOSProxy());
		    Database.Log.Console = new iOSConsoleLogger();
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
