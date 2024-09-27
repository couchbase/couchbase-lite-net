//
// NativeActivate.cs
//
// Copyright (c) 2018 Couchbase, Inc All rights reserved.
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
using System.Reflection;

using Couchbase.Lite.Support;

namespace LiteCore.Interop
{
    internal partial class Native
    {
        #region Constructors

        static Native()
        {
#if NET6_0_OR_GREATER
            if(OperatingSystem.IsIOS()) {
                System.Diagnostics.Debug.WriteLine("Setting DllImportResolver");
                System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (libraryName, assembly, searchPath) =>
                {
                    System.Diagnostics.Debug.WriteLine($"DllImportResolver called for {libraryName}");
                    if (libraryName == Constants.DllName) {
                        libraryName = "@rpath/LiteCore.framework/LiteCore";
                    }

                    return System.Runtime.InteropServices.NativeLibrary.Load(libraryName);
                });
            }
#endif

#if NEEDS_LITECORE_LOAD
            NetDesktop.LoadLiteCore();
#endif
        }

        #endregion
    }
}