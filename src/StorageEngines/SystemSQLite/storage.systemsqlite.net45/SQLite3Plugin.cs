//
// SQLite3Plugin.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Runtime.InteropServices;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Storage.SystemSQLite
{
    internal sealed class SQLite3Plugin
    {
        [DllImport("kernel32")]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        public static void Init()
        {
            if(Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            SQLitePCL.ISQLite3Provider imp = new SQLitePCL.SQLite3Provider_esqlite3 ();
            SQLitePCL.raw.SetProvider (imp);
        }
    }
}

