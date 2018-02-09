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

using Android.Content;

using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// Android specific support logic
    /// </summary>
    public static class Droid
    {
        #region Variables

        private static AtomicBool _Activated = false;

        #endregion

        #region Public Methods

        /// <summary>
        /// Activates the support classes for Android
        /// </summary>
        /// <param name="context">The main context of the Android application</param>
        public static void Activate([NotNull]Context context)
        {
			if(_Activated.Set(true)) {
				return;
			}

            Service.AutoRegister(typeof(Droid).Assembly);
            Service.Register<IDefaultDirectoryResolver>(() => new DefaultDirectoryResolver(context));
            Service.Register<IMainThreadTaskScheduler>(() => new MainThreadTaskScheduler(context));
        }

        /// <summary>
		/// Enables text based logging for debugging purposes.  Log statements will
		/// be printed to logcat under the CouchbaseLite tag.
		/// </summary>
		public static void EnableTextLogging()
		{
			Log.EnableTextLogging(new AndroidDefaultLogger());
		}

        #endregion
    }
}
