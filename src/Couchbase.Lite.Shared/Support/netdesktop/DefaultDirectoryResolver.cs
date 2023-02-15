﻿//
//  DefaultDirectoryResolver.cs
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

// Windows 2012 doesn't define the more generic variants
#if (NETFRAMEWORK || NET462 || NET6_0_OR_GREATER) && !__MOBILE__ && !NET6_0_WINDOWS10_0_19041_0
using System;
using System.IO;
using Couchbase.Lite.DI;

namespace Couchbase.Lite.Support
{
    // NOTE: AppContext.BaseDirectory is not entirely reliable, but there is no other choice
    // It seems to usually be in the right place?

    [CouchbaseDependency]
    internal sealed class DefaultDirectoryResolver : IDefaultDirectoryResolver
    {
        #region IDefaultDirectoryResolver

        public string DefaultDirectory()
        {
            var baseDirectory = AppContext.BaseDirectory ??
                                throw new RuntimeException("BaseDirectory was null, cannot continue...");
            return Path.Combine(baseDirectory, "CouchbaseLite");
        }

        #endregion
    }
}
#endif