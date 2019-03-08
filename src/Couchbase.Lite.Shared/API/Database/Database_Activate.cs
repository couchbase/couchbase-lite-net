using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

using Couchbase.Lite.DI;
using Couchbase.Lite.Support;

using Microsoft.Win32;

namespace Couchbase.Lite
{
    public partial class Database
    {
        private static void Activate()
        {
            // Windows 2012 doesn't define NETFRAMEWORK for some reason
            #if NETCOREAPP2_0 || NETFRAMEWORK || NET461
            Service.AutoRegister(typeof(Database).GetTypeInfo().Assembly);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Service.Register<IProxy>(new WindowsProxy());
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Service.Register<IProxy>(new MacProxy());
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                Service.Register<IProxy>(new LinuxProxy());
            }

            Log.Console = new DesktopConsoleLogger();
            #elif UAP10_0_16299
            Service.AutoRegister(typeof(Database).GetTypeInfo().Assembly);
            Service.Register<IProxy>(new UWPProxy());
            Log.Console = new UwpConsoleLogger();
            #elif __ANDROID__
            if (Droid.Context == null) {
                throw new RuntimeException(
                    "Android context not set.  Please ensure that a call to Couchbase.Lite.Support.Droid.Activate() is made.");
            }

            Service.AutoRegister(typeof(Database).Assembly);
            Service.Register<IDefaultDirectoryResolver>(() => new DefaultDirectoryResolver(Droid.Context));
            Service.Register<IMainThreadTaskScheduler>(() => new MainThreadTaskScheduler(Droid.Context));
            Service.Register<IProxy>(new XamarinAndroidProxy());
            Log.Console = new AndroidConsoleLogger();
            #elif __IOS__
            Console.WriteLine("Loading support items");
            Service.AutoRegister(typeof(Database).Assembly);
            Service.Register<IProxy>(new IOSProxy());
            Log.Console = new iOSConsoleLogger();
            #elif NETSTANDARD2_0
            throw new RuntimeException(
                "Pure .NET Standard variant executed.  This means that Couchbase Lite is running on an unsupported platform");
            #else
            #error Unknown Platform
            #endif
        }
    }
}