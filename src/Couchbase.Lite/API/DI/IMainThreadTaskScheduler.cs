// 
//  IMainThreadTaskScheduler.cs
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

using System.Threading.Tasks;

using JetBrains.Annotations;

namespace Couchbase.Lite.DI
{
    /// <summary>
    /// An interface for an object that can behave as a <see cref="TaskScheduler"/>
    /// that invokes its tasks on the UI (main) thread of an application.  Not applicable
    /// for all platforms, as some do not have main threads set up in a way that is usable
    /// (e.g. .NET Core)
    /// </summary>
    public interface IMainThreadTaskScheduler
    {
        #region Properties

        /// <summary>
        /// Gets if the currently executing thread is the main thread
        /// of the application
        /// </summary>
        bool IsMainThread { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the object as a <see cref="TaskScheduler"/> so that
        /// it can be used for various .NET framework methods
        /// </summary>
        /// <returns>The main thread scheduler cast to a <see cref="TaskScheduler"/></returns>
        [NotNull]
        TaskScheduler AsTaskScheduler();

        #endregion
    }
}