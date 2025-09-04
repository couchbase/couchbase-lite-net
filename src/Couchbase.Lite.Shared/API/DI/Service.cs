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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

#if CBL_PLATFORM_DOTNET || CBL_PLATFORM_DOTNETFX
using System.Runtime.InteropServices;
#endif

#if !CBL_PLATFORM_APPLE && !CBL_PLATFORM_WINUI
using Couchbase.Lite.Support;
#endif

using LiteCore.Interop;
using SimpleInjector;

namespace Couchbase.Lite.DI;

/// <summary>
/// This is the entry point for registering dependency injection implementation in Couchbase Lite .NET
/// </summary>
public static class Service
{
    private static readonly Container ServiceCollection = new();

    [ExcludeFromCodeCoverage]
    static Service()
    {
        ServiceCollection.Options.AllowOverridingRegistrations = true;

#if CBL_PLATFORM_DOTNET || CBL_PLATFORM_DOTNETFX
        AutoRegister(typeof(Database).GetTypeInfo().Assembly);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            Register<IProxy>(new WindowsProxy());
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            Register<IProxy>(new MacProxy());
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            Register<IProxy>(new LinuxProxy());
        }
#elif CBL_PLATFORM_WINUI
        AutoRegister(typeof(Database).GetTypeInfo().Assembly);
#elif CBL_PLATFORM_ANDROID
        #if !TEST_COVERAGE
        if (Droid.Context == null) {
            throw new RuntimeException(
                "Android context not set.  Please ensure that a call to Couchbase.Lite.Support.Droid.Activate() is made.");
        }

        AutoRegister(typeof(Database).Assembly);
        Register<IDefaultDirectoryResolver>(() => new DefaultDirectoryResolver(Droid.Context));
        Register<IMainThreadTaskScheduler>(() => new MainThreadTaskScheduler(Droid.Context));
        #endif
#elif CBL_PLATFORM_APPLE
        AutoRegister(typeof(Database).Assembly);
#else
        #error Unknown Platform
#endif
    }

    /// <summary>
    /// Automatically register all the dependency types declared
    /// <see cref="CouchbaseDependencyAttribute" />s.  To auto register classes,
    /// they must implement an interface and must have a default constructor.
    /// </summary>
    /// <param name="assembly"></param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c></exception>
    /// <exception cref="InvalidOperationException">Thrown if an invalid type is found inside the assembly (i.e.
    /// one that does not implement any interfaces and/or does not have a parameter-less constructor)</exception>
    [ExcludeFromCodeCoverage]
    private static void AutoRegister(Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        foreach (var type in assembly.GetTypes().Where(x => x.GetTypeInfo().IsClass)) {
            var ti = type.GetTypeInfo();

            var attribute = ti.GetCustomAttribute<CouchbaseDependencyAttribute>();
            if (attribute == null) {
                continue;
            }

            var actualInterfaces = ti.ImplementedInterfaces;
            if(actualInterfaces == null) {
                throw new InvalidOperationException($"{type.Name} does not implement any interfaces!");
            }

            var interfaces = actualInterfaces as Type[] ?? actualInterfaces.ToArray();
            var minimalInterfaces = interfaces
                .Except(type.BaseType?.GetInterfaces() ?? Enumerable.Empty<Type>())
                .Except(interfaces.SelectMany(i => i.GetInterfaces()));
            var interfaceType = minimalInterfaces.FirstOrDefault();
            if(interfaceType == null) {
                throw new InvalidOperationException($"{type.Name} does not implement any interfaces of its own");
            }

            if(ti.DeclaredConstructors.All(x => x.GetParameters().Length != 0)) {
                throw new InvalidOperationException($"{type.Name} does not contain a default constructor");
            }

            if (attribute.Transient) {
                ServiceCollection.Register(interfaceType, type, Lifestyle.Transient);
            } else {
                if (attribute.Lazy) {
                    ServiceCollection.Register(interfaceType, type, Lifestyle.Singleton);
                } else {
                    ServiceCollection.RegisterInstance(interfaceType, Activator.CreateInstance(type)
                        ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, $"Unable to create instance of {type.Name}"));
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
    [ExcludeFromCodeCoverage]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static void Register<TService, TImplementation>(bool transient = false) where TService : class where TImplementation : class, TService
    {
        Lifestyle style = transient ? Lifestyle.Transient : Lifestyle.Singleton;
        ServiceCollection.Register<TService, TImplementation>(style);
    }

    /// <summary>
    /// Registers a lazy implementation for the given service
    /// </summary>
    /// <typeparam name="TService">The service type</typeparam>
    /// <param name="generator">The function that creates the object to use</param>
    /// <param name="transient">If <c>true</c> each call to <see cref="GetInstance{T}"/> will return
    /// a new instance, otherwise use a singleton</param>
    [ExcludeFromCodeCoverage]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static void Register<TService>(Func<TService> generator, bool transient = false) where TService : class
    {
        var style = transient ? Lifestyle.Transient : Lifestyle.Singleton;
        ServiceCollection.Register(generator, style);
    }

    /// <summary>
    /// Registers an instantiated object as a singleton implementation for a service
    /// </summary>
    /// <typeparam name="TService">The service type</typeparam>
    /// <param name="instance">The singleton instance to use as the implementation</param>
    [ExcludeFromCodeCoverage]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public static void Register<TService>(TService instance)
        where TService : class
    {
        ServiceCollection.RegisterInstance(instance);
    }

    /// <summary>
    /// Gets the implementation for the given service, or <c>null</c>
    /// if no implementation is registered
    /// </summary>
    /// <typeparam name="T">The type of service to get an implementation for</typeparam>
    /// <returns>The implementation for the given service</returns>
    public static T? GetInstance<T>() where T : class
    {
        try {
            return ServiceCollection.GetInstance<T>();
        } catch (ActivationException) {
            return null;
        }
    }

    internal static T GetRequiredInstance<T>() where T : class
    {
        return GetInstance<T>() ??  throw new InvalidOperationException(
            $"""
             A required dependency injection class is missing ({typeof(T).FullName}).
                                    If this is not a custom platform, please file a bug report at https://github.com/couchbase/couchbase-lite-net/issues
             """);
    }
}