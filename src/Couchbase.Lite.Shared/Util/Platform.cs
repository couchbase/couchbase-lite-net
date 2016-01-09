//
// Platform.cs
//
// Author:
//  Jim Borden  <jim.borden@couchbase.com>
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
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Couchbase.Lite.Util
{
    internal struct utsname
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] sysname;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] nodename;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] release;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] version;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] machine;
    }

    internal static class Platform
    {
        public static readonly string Name;
        public static readonly string Architecture;

        #if !__IOS__ && !__ANDROID__

        private static string GetWindowsName()
        {
            string result = string.Empty;
            var searcher = new System.Management.ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
            foreach (var os in searcher.Get())
            {
                result = os["Caption"].ToString();
                break;
            }
            return result;
        }

        private static string GetWindowsArchitecture()
        {
            string result = string.Empty;
            var searcher = new System.Management.ManagementObjectSearcher("SELECT Architecture FROM Win32_Processor");
            foreach (var cpu in searcher.Get())
            {
                var type = (ushort)cpu["Architecture"];
                switch(type) {
                    case 0:
                        result = "x86";
                        break;
                    case 5:
                        result = "ARM";
                        break;
                    case 9:
                        result = "x86_64";
                        break;
                    default:
                        result = String.Format("Rare ({0})", type);
                        break;
                }

                break;
            }
            return result;
        }

        #endif

        static Platform()
        {
            var isWindows = Path.DirectorySeparatorChar == '\\';
            if (isWindows) {
                Name = GetWindowsName();
                Architecture = GetWindowsArchitecture();
            } else {
                var buf = new utsname();
                if (uname(ref buf) != 0) {
                    Name = "Unknown";
                    Architecture = "Unknown";
                }

                Architecture = Encoding.ASCII.GetString(buf.machine.TakeWhile(x => x != 0).ToArray());
                var systemName = Encoding.ASCII.GetString(buf.sysname.TakeWhile(x => x != 0).ToArray());
                var version = Encoding.ASCII.GetString(buf.release.TakeWhile(x => x != 0).ToArray());

                if (systemName == "Darwin") {
                    systemName = "OS X";
                    var darwinVesion = Int32.Parse(version.Split('.').First());
                    version = String.Format("10.{0}", darwinVesion - 4);
                }

                Name = String.Format("{0} {1}", systemName, version);
            }
        }

        [DllImport("libc")]
        private static extern int uname(ref utsname buf);
    }
}
   