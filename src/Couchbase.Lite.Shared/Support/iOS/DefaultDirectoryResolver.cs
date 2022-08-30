﻿//
// DefaultDirectoryResolver.cs
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
#if __IOS__
using System.IO;
using Couchbase.Lite.DI;
using Foundation;

namespace Couchbase.Lite.Support
{
    [CouchbaseDependency]
    internal sealed class DefaultDirectoryResolver : IDefaultDirectoryResolver
    {
        public string DefaultDirectory()
        {
            var dirID = NSSearchPathDirectory.ApplicationSupportDirectory;

            var paths = NSSearchPath.GetDirectories(dirID, NSSearchPathDomain.User, true);
            var path = paths[0];

            return Path.Combine(path, "CouchbaseLite");
        }
    }
}
#endif