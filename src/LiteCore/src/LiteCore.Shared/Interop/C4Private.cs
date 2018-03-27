//
// C4Private.cs
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

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using Couchbase.Lite.DI;

using LiteCore.Util;

namespace LiteCore.Interop
{
    internal static class NativePrivate
    {
        public static void c4log_warnOnErrors(bool warn)
        {
            if (Service.GetInstance<IRuntimePlatform>()?.OSDescription?.Contains("iOS") == true) {
                c4log_warnOnErrors_ios(warn);
            } else {
                c4log_warnOnErrors_common(warn);
            }
        }

        [DllImport(Constants.DllName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "c4log_warnOnErrors")]
        private static extern void c4log_warnOnErrors_common(bool warn);

        [DllImport(Constants.DllNameIos, CallingConvention=CallingConvention.Cdecl, EntryPoint = "c4log_warnOnErrors")]
        public static extern void c4log_warnOnErrors_ios(bool warn);
    }
}