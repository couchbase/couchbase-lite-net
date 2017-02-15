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
using Couchbase.Lite.Support.Internal;

namespace Couchbase.Lite.Support
{
    public interface IInjectable
    {}

    internal static class InjectableCollection
    {
        #region Constants

        //private const string Tag = nameof(InjectableCollection);
        private static readonly Dictionary<Type, Func<IInjectable>> _InjectableMap =
            new Dictionary<Type, Func<IInjectable>>();

        #endregion

        #region Constructors

        static InjectableCollection()
        {
            RegisterImplementation<ILogger>(() => new DefaultLogger());
            RegisterImplementation<IDefaultDirectoryResolver>(() => new DefaultDirectoryResolver());
        }

        #endregion

        #region Public Methods

        public static T GetImplementation<T>() where T : IInjectable
        {
            var type = typeof(T);
            Func<IInjectable> retVal;
            if(!_InjectableMap.TryGetValue(type, out retVal)) {
                throw new KeyNotFoundException($"No implementation registered for {type.FullName}");
            }

            return (T)retVal();
        }

        public static void RegisterImplementation<T>(Func<IInjectable> generator) where T : IInjectable
        {
            var type = typeof(T);
            //if(_injectableMap.ContainsKey(type)) {
            //    throw new InvalidOperationException($"{type.FullName} is already registered!");
            //}

            _InjectableMap[type] = generator;
        }

        #endregion
    }
}
