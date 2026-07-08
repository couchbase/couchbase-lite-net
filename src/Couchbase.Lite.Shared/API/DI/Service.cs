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

using System.Diagnostics.CodeAnalysis;

#if CBL_PLATFORM_DOTNET || CBL_PLATFORM_DOTNETFX
using System.Runtime.InteropServices;
#endif

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Support;

using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Lite.DI;

/// <summary>
/// This is the entry point for registering dependency injection implementation in Couchbase Lite .NET
/// </summary>
public static class Service
{
    /// <summary>
    /// The service provider containing all the dependency injected
    /// classes for Couchbase Lite .NET
    /// </summary>
    public static readonly ServiceProvider Provider;

    [ExcludeFromCodeCoverage]
    static Service()
    {
        // Each platform registers its implementations explicitly instead of scanning
        // the assembly for [CouchbaseDependency] classes.  Assembly scanning requires
        // reflection APIs (Assembly.GetTypes / Activator.CreateInstance) that are
        // unavailable under Native AOT and trimming.  When adding a new dependency
        // implementation, register it here for the relevant platforms.
        var collection = new ServiceCollection();
#if CBL_PLATFORM_DOTNET || CBL_PLATFORM_DOTNETFX
        collection.AddSingleton<IDefaultDirectoryResolver>(new DefaultDirectoryResolver());
        collection.AddSingleton<IConsoleLogWriter>(new ConsoleLogWriter());

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            collection.AddSingleton<IProxy>(new WindowsProxy());
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            collection.AddSingleton<IProxy>(new MacProxy());
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            collection.AddSingleton<IProxy>(new LinuxProxy());
        }
#elif CBL_PLATFORM_WINUI
        collection.AddSingleton<IDefaultDirectoryResolver>(new DefaultDirectoryResolver());
        collection.AddSingleton<IRuntimePlatform>(new WinUIRuntimePlatform());
        collection.AddSingleton<IProxy>(new WinUIProxy());
        collection.AddTransient<IReachability>(_ => new Reachability());
#elif CBL_PLATFORM_ANDROID
        #if !TEST_COVERAGE
        if (Droid.Context == null) {
            throw new RuntimeException(
                "Android context not set.  Please ensure that a call to Couchbase.Lite.Support.Droid.Activate() is made.");
        }

        collection.AddSingleton<IRuntimePlatform>(new AndroidRuntimePlatform());
        collection.AddSingleton<IConsoleLogWriter>(new ConsoleLogWriter());
        collection.AddSingleton<IProxy>(new DotnetAndroidProxy());
        collection.AddSingleton<IDefaultDirectoryResolver>(_ => new DefaultDirectoryResolver(Droid.Context));
        collection.AddSingleton<IMainThreadTaskScheduler>(_ => new MainThreadTaskScheduler(Droid.Context));
        #endif
#elif CBL_PLATFORM_APPLE
        collection.AddSingleton<IDefaultDirectoryResolver>(new DefaultDirectoryResolver());
        collection.AddSingleton<IConsoleLogWriter>(new ConsoleLogWriter());
        collection.AddSingleton<IRuntimePlatform>(new iOSRuntimePlatform());
        collection.AddSingleton<IProxy>(new IOSProxy());
        collection.AddTransient<IMainThreadTaskScheduler>(_ => new MainThreadTaskScheduler());
        collection.AddTransient<IReachability>(_ => new iOSReachability());
#else
        #error Unknown Platform
#endif

        Provider = collection.BuildServiceProvider();
    }
}
