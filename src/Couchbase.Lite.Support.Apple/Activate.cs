//
// Activate.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.IO;
using Couchbase.Lite.DI;
using Foundation;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// Support classes for Xamarin iOS
    /// </summary>
    public static class iOS
    {
        /// <summary>
        /// Activates the Xamarin iOS specific support classes
        /// </summary>
        public static void Activate()
        {
            Console.WriteLine("Loading support items");
            InjectableCollection.RegisterImplementation<IDefaultDirectoryResolver>(() => new DefaultDirectoryResolver());
#if __IOS__
            InjectableCollection.RegisterImplementation<ILogger>(() => new iOSDefaultLogger());
#else
            InjectableCollection.RegisterImplementation<ILogger>(() => new tvOSDefaultLogger());
#endif

            InjectableCollection.RegisterImplementation<ISslStreamFactory>(() => new SslStreamFactory());
            Console.WriteLine("Loading libLiteCore.dylib");
            var dylibPath = Path.Combine(NSBundle.MainBundle.BundlePath, "libLiteCore.dylib");
            if(!File.Exists(dylibPath)) {
                Console.WriteLine("Failed to find libLiteCore.dylib, nothing is going to work!");
            }

            var loaded = ObjCRuntime.Dlfcn.dlopen(dylibPath, 0);
            if(loaded == IntPtr.Zero) {
                Console.WriteLine("Failed to load libLiteCore.dylib, nothing is going to work!");
                var error = ObjCRuntime.Dlfcn.dlerror();
                if(String.IsNullOrEmpty(error)) {
                    Console.WriteLine("dlerror() was empty; most likely missing architecture");
                } else {
                    Console.WriteLine($"Error: {error}");
                }
            }
        }
    }
}
