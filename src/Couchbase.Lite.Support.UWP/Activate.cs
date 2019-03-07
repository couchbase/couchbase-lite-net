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
#define DEBUG
using System;
using System.Diagnostics;
using System.Reflection;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// The UWP support class
    /// </summary>
    public static class UWP
    {
        #region Public Methods

        /// <summary>
        /// Activates the support classes for UWP
        /// </summary>
        [Obsolete("This call is no longer needed, and will be removed in 3.0")]
        public static void Activate()
        {

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
