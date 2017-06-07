//
//  IInjectable.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Sync;

namespace Couchbase.Lite.DI
{
    /// <summary>
    /// A placeholder interface indicating that a class is meant to be used for
    /// dependency injection
    /// </summary>
    public interface IInjectable
    {
        
    }

    /// <summary>
    /// The central location for registering implementations for dependency injected classes.
    /// Adding to this will allow support for platforms that are not officially supported.
    /// </summary>
    public static class InjectableCollection
    {
        #region Constants

        //private const string Tag = nameof(InjectableCollection);
        private static readonly Dictionary<Type, Func<IInjectable>> _InjectableMap =
            new Dictionary<Type, Func<IInjectable>>();

        #endregion

        static InjectableCollection()
        {
            RegisterImplementation<IReachability>(() => new Reachability());
        }

        #region Public Methods

        /// <summary>
        /// A list of the types that lack default implementations and need to be registered on any new platforms.
        /// Others not here may have default implementations
        /// </summary>
        public static Type[] NeededTypes => new[] {typeof(ILogger), typeof(IDefaultDirectoryResolver), typeof(ISslStreamFactory) };

        /// <summary>
        /// Gets the implementation of the given interface
        /// </summary>
        /// <typeparam name="T">The interface to retrieve the implementation for</typeparam>
        /// <returns>The concrete implementation of the interface</returns>
        public static T GetImplementation<T>() where T : IInjectable
        {
            var type = typeof(T);
            Func<IInjectable> retVal;
            if(!_InjectableMap.TryGetValue(type, out retVal)) {
                throw new KeyNotFoundException($"No implementation registered for {type.FullName}");
            }

            return (T)retVal();
        }

        /// <summary>
        /// Registers an implementation of the given interface as a generator
        /// </summary>
        /// <typeparam name="T">The type of interface to register</typeparam>
        /// <param name="generator">The function that creates the concrete implementation</param>
        public static void RegisterImplementation<T>(Func<IInjectable> generator) where T : IInjectable
        {
            var type = typeof(T);
            _InjectableMap[type] = generator;
        }

        #endregion
    }
}
