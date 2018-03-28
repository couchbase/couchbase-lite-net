//
// Misc_native.cs
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

using System.Runtime.InteropServices;

using LiteCore;

namespace LiteCore.Interop
{
    internal sealed partial class LiteCoreImpl
    {
        public unsafe C4LogDomain* c4log_getDomain(byte* name, bool create) => Native.c4log_getDomain(name, create);

        public void c4log_warnOnErrors(bool warn) => Native.c4log_warnOnErrors(warn);
    }

    internal unsafe static partial class Native
    {
        // NOTE: Must allocate unmanaged memory via Marshal class
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern C4LogDomain* c4log_getDomain(byte* name,
            [MarshalAs(UnmanagedType.U1)] bool create);

        [DllImport(Constants.DllName, CallingConvention=CallingConvention.Cdecl)]
        public static extern void c4log_warnOnErrors(bool warn);
    }
}