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

using Android.Content;
using Couchbase.Lite.DI;
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// Android specific support logic
    /// </summary>
    public static class Droid
    {
        /// <summary>
        /// Activates the support classes for Android
        /// </summary>
        /// <param name="context">The main context of the Android application</param>
        public static void Activate(Context context)
        {
            Service.RegisterServices(collection =>
            {
                collection.AddSingleton<IDefaultDirectoryResolver, DefaultDirectoryResolver>()
                    .AddSingleton<ILogger, AndroidDefaultLogger>()
                    .AddSingleton<ISslStreamFactory, SslStreamFactory>();
            });
        }
    }
}
