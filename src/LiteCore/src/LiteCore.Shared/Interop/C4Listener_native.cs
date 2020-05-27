//
// C4Listener_native.cs
//
// Copyright (c) 2020 Couchbase, Inc All rights reserved.
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
using System.Linq;
using System.Runtime.InteropServices;

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4ListenerAPIs c4listener_availableAPIs();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Listener* c4listener_start(C4ListenerConfig* config, C4Error* error);

        public static bool c4listener_shareDB(C4Listener* listener, string name, C4Database* db, C4Error* outError)
        {
            using(var name_ = new C4String(name)) {
                return NativeRaw.c4listener_shareDB(listener, name_.AsFLSlice(), db, outError);
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4listener_unshareDB(C4Listener* listener, C4Database* db, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableArray* c4listener_getURLs(C4Listener* listener, C4Database* db);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort c4listener_getPort(C4Listener* listener);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4listener_getConnectionStatus(C4Listener* listener, uint* connectionCount, uint* activeConnectionCount);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4listener_shareDB(C4Listener* listener, FLSlice name, C4Database* db, C4Error* outError);


    }
}
