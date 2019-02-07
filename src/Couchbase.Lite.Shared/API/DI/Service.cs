// 
//  Service.cs
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
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;
using JetBrains.Annotations;

using SimpleInjector;

namespace Couchbase.Lite.DI
{

    /// <summary>
    /// This is the entry point for registering dependency injection implementation in Couchbase Lite .NET
    /// </summary>
    public static class Service
    {
        #region Constants

        private const string Tag = nameof(Service);

        [NotNull]
        private static readonly Type[] _RequiredTypes = new[] {
            typeof(IDefaultDirectoryResolver)
        };

        #endregion

        #region Variables

        [NotNull]
        private static readonly Container _Collection = new Container();

        #endregion

        #region Public Methods

        /// <summary>
        /// Automatically register all the dependency types declared
        /// <see cref="CouchbaseDependencyAttribute" />s.  To auto register classes,
        /// they must implement an interface and must have a default constructor.
        /// </summary>
        /// <param name="assembly"></param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if an invalid type is found inside of the assembly (i.e.
        /// one that does not implement any interfaces and/or does not have a parameter-less constructor)</exception>
        public static void AutoRegister([NotNull]Assembly assembly)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(assembly), assembly);

            foreach (var type in assembly.GetTypes()?.Where(x => x.GetTypeInfo().IsClass)) {
                var ti = type.GetTypeInfo();

                var attribute = ti?.GetCustomAttribute<CouchbaseDependencyAttribute>();
                if (attribute == null) {
                    continue;
                }

                var interfaceType = ti?.ImplementedInterfaces?.FirstOrDefault();
                if (interfaceType == null) {
                    throw new InvalidOperationException($"{type.Name} does not implement any interfaces!");
                }

                if(ti?.DeclaredConstructors?.All(x => x?.GetParameters()?.Length != 0) == true) {
                    throw new InvalidOperationException($"{type.Name} does not contain a default constructor");
                }

                if (attribute.Transient) {
                    _Collection.Register(interfaceType, type, Lifestyle.Transient);
                } else {
                    if (attribute.Lazy) {
                        _Collection.Register(interfaceType, type, Lifestyle.Singleton);
                    } else {
                        _Collection.RegisterSingleton(interfaceType, Activator.CreateInstance(type));
                    }
                }
            }
        }

        /// <summary>
        /// Registers an implementation for the given service
        /// </summary>
        /// <typeparam name="TService">The service type</typeparam>
        /// <typeparam name="TImplementation">The implementation type</typeparam>
        /// <param name="transient">If <c>true</c> each call to <see cref="GetInstance{T}"/> will return
        /// a new instance, otherwise use a singleton</param>
        public static void Register<TService, TImplementation>(bool transient = false) where TService : class where TImplementation : class, TService
        {
            Lifestyle style = transient ? Lifestyle.Transient : Lifestyle.Singleton;
            _Collection.Register<TService, TImplementation>(style);
        }

        /// <summary>
        /// Registers a lazy implementation for the given service
        /// </summary>
        /// <typeparam name="TService">The service type</typeparam>
        /// <param name="generator">The function that creates the object to use</param>
        /// <param name="transient">If <c>true</c> each call to <see cref="GetInstance{T}"/> will return
        /// a new instance, otherwise use a singleton</param>
        public static void Register<TService>(Func<TService> generator, bool transient = false) where TService : class
        {
            Lifestyle style = transient ? Lifestyle.Transient : Lifestyle.Singleton;
            _Collection.Register(generator, style);
        }

        /// <summary>
        /// Registers an instantiated object as a singleton implementation for a service
        /// </summary>
        /// <typeparam name="TService">The service type</typeparam>
        /// <param name="instance">The singleton instance to use as the implementation</param>
        public static void Register<TService>(TService instance)
            where TService : class
        {
            _Collection.RegisterSingleton(instance);
        }

        /// <summary>
        /// Gets the implementation for the given service, or <c>null</c>
        /// if no implementation is registered
        /// </summary>
        /// <typeparam name="T">The type of service to get an implementation for</typeparam>
        /// <returns>The implementation for the given service</returns>
        [CanBeNull]
        public static T GetInstance<T>() where  T : class
        {
            try {
                return _Collection.GetInstance<T>();
            } catch (ActivationException) {
                return null;
            }
        }

        [NotNull]
        internal static T GetRequiredInstance<T>() where T : class
        {
            return GetInstance<T>() ??  throw new InvalidOperationException(
                       $@"A required dependency injection class is missing ({typeof(T).FullName}).
                       Please ensure that you have called the proper Activate() class in the 
                       support assembly (e.g. Couchbase.Lite.Support.UWP.Activate()) or that you 
                       have manually registered dependencies via the Couchbase.Lite.DI.Service 
                       class.");
        }

        #endregion
    }
}
