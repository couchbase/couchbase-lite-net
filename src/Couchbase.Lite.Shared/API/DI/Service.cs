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
using System.Runtime.InteropServices;
using Couchbase.Lite.Support;

using SimpleInjector;

using NotNull = JetBrains.Annotations.NotNullAttribute;
using CanBeNull = JetBrains.Annotations.CanBeNullAttribute;

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

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        static Service()
        {
            // Windows 2012 doesn't define NETFRAMEWORK for some reason
            #if NETCOREAPP2_0 || NETFRAMEWORK || NET461
            AutoRegister(typeof(Database).GetTypeInfo().Assembly);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Service.Register<IProxy>(new WindowsProxy());
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Service.Register<IProxy>(new MacProxy());
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                Service.Register<IProxy>(new LinuxProxy());
            }
            #elif UAP10_0_16299 || WINDOWS_UWP
            Service.AutoRegister(typeof(Database).GetTypeInfo().Assembly);
            Service.Register<IProxy>(new UWPProxy());
            #elif __ANDROID__
            #if !TEST_COVERAGE
            if (Droid.Context == null) {
                throw new RuntimeException(
                    "Android context not set.  Please ensure that a call to Couchbase.Lite.Support.Droid.Activate() is made.");
            }

            Service.AutoRegister(typeof(Database).Assembly);
            Service.Register<IDefaultDirectoryResolver>(() => new DefaultDirectoryResolver(Droid.Context));
            Service.Register<IMainThreadTaskScheduler>(() => new MainThreadTaskScheduler(Droid.Context));
            Service.Register<IProxy>(new XamarinAndroidProxy());
            #endif
            #elif __IOS__
            Service.AutoRegister(typeof(Database).Assembly);
            Service.Register<IProxy>(new IOSProxy());
            #elif NETSTANDARD2_0
            throw new RuntimeException(
                "Pure .NET Standard variant executed.  This means that Couchbase Lite is running on an unsupported platform");
            #else
            #error Unknown Platform
            #endif
        }

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
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        public static void AutoRegister([NotNull]Assembly assembly)
        {
            if (assembly == null) {
                throw new ArgumentNullException(nameof(assembly));
            }

            foreach (var type in assembly.GetTypes()?.Where(x => x.GetTypeInfo().IsClass)) {
                var ti = type.GetTypeInfo();

                var attribute = ti?.GetCustomAttribute<CouchbaseDependencyAttribute>();
                if (attribute == null) {
                    continue;
                }

                var actualInterfaces = ti?.ImplementedInterfaces;
                if(actualInterfaces == null) {
                    throw new InvalidOperationException($"{type.Name} does not implement any interfaces!");
                }

                var minimalInterfaces = actualInterfaces
                    .Except(type.BaseType?.GetInterfaces() ?? Enumerable.Empty<Type>())
                    .Except(actualInterfaces.SelectMany(i => i.GetInterfaces()));
                var interfaceType = minimalInterfaces.FirstOrDefault();
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
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
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
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
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
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
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
                       If this is not a custom platform, please file a bug report at https://github.com/couchbase/couchbase-lite-net/issues");
        }

        #endregion
    }
}
