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
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Lite.DI;

/// <summary>
/// This is the entry point for registering dependency injection implementation in Couchbase Lite .NET
/// </summary>
public static class Service
{
    public static readonly ServiceProvider Provider;

    [ExcludeFromCodeCoverage]
    static Service()
    {
        var collection = new ServiceCollection();
#if CBL_PLATFORM_DOTNET || CBL_PLATFORM_DOTNETFX
        AutoRegister(typeof(Database).GetTypeInfo().Assembly, collection);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            collection.AddSingleton<IProxy>(new WindowsProxy());
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            collection.AddSingleton<IProxy>(new MacProxy());
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            collection.AddSingleton<IProxy>(new LinuxProxy());
        }
#elif CBL_PLATFORM_WINUI
        AutoRegister(typeof(Database).GetTypeInfo().Assembly, collection);
#elif CBL_PLATFORM_ANDROID
        #if !TEST_COVERAGE
        if (Droid.Context == null) {
            throw new RuntimeException(
                "Android context not set.  Please ensure that a call to Couchbase.Lite.Support.Droid.Activate() is made.");
        }

        AutoRegister(typeof(Database).Assembly, collection);
        collection.AddSingleton<IDefaultDirectoryResolver>(_ => new DefaultDirectoryResolver(Droid.Context));
        collection.AddSingleton<IMainThreadTaskScheduler>(_ => new MainThreadTaskScheduler(Droid.Context));
        #endif
#elif CBL_PLATFORM_APPLE
        AutoRegister(typeof(Database).Assembly, collection);
#else
        #error Unknown Platform
#endif
        
        Provider = collection.BuildServiceProvider();
    }

    /// <summary>
    /// Automatically register all the dependency types declared
    /// <see cref="CouchbaseDependencyAttribute" />s.  To auto register classes,
    /// they must implement an interface and must have a default constructor.
    /// </summary>
    /// <param name="assembly">The assembly to scan</param>
    /// <param name="serviceCollection">The collection to add to</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c></exception>
    /// <exception cref="InvalidOperationException">Thrown if an invalid type is found inside the assembly (i.e.
    /// one that does not implement any interfaces and/or does not have a parameter-less constructor)</exception>
    [ExcludeFromCodeCoverage]
    private static void AutoRegister(Assembly assembly, IServiceCollection serviceCollection)
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
                serviceCollection.AddTransient(interfaceType, type);
            } else {
                if (attribute.Lazy) {
                    serviceCollection.AddSingleton(interfaceType, type);
                } else {
                    serviceCollection.AddSingleton(interfaceType, Activator.CreateInstance(type)
                        ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, $"Unable to create instance of {type.Name}"));
                }
            }
        }
    }
}