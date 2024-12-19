//
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

#if CBL_PLATFORM_WINUI
using Couchbase.Lite.DI;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System;
using Windows.Storage;

namespace Couchbase.Lite.Support
{
    [CouchbaseDependency]
    internal sealed class DefaultDirectoryResolver : IDefaultDirectoryResolver
    {
        private const long APPMODEL_ERROR_NO_PACKAGE = 15700L;
        private static readonly bool IsPackaged;

        static DefaultDirectoryResolver()
        {
            int length = 0;
            StringBuilder sb = new StringBuilder(0);
            int result = GetCurrentPackageFullName(ref length, sb); sb = new StringBuilder(length);

            sb = new StringBuilder(length);
            result = GetCurrentPackageFullName(ref length, sb);
            IsPackaged = result != APPMODEL_ERROR_NO_PACKAGE;
            if(!IsPackaged) {
                Directory.CreateDirectory(DefaultDirectoryUnpackaged());
            }
        }

        public string DefaultDirectory()
        {
            return IsPackaged 
                ? DefaultDirectoryPackaged()
                : DefaultDirectoryUnpackaged();
        }

        private static string DefaultDirectoryPackaged() => ApplicationData.Current.LocalFolder.Path;

        private static string DefaultDirectoryUnpackaged() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CouchbaseLite");

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);
    }
}
#endif