//
// C4Listener_native.cs
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

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4listener_free(C4Listener* listener);

        public static bool c4listener_shareDB(C4Listener* listener, string name, C4Database* db)
        {
            using(var name_ = new C4String(name)) {
                return NativeRaw.c4listener_shareDB(listener, name_.AsFLSlice(), db);
            }
        }

        public static bool c4listener_unshareDB(C4Listener* listener, string name)
        {
            using(var name_ = new C4String(name)) {
                return NativeRaw.c4listener_unshareDB(listener, name_.AsFLSlice());
            }
        }

        public static string c4db_URINameFromPath(string path)
        {
            using(var path_ = new C4String(path)) {
                using(var retVal = NativeRaw.c4db_URINameFromPath(path_.AsFLSlice())) {
                    return ((FLSlice)retVal).CreateString();
                }
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4listener_shareDB(C4Listener* listener, FLSlice name, C4Database* db);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4listener_unshareDB(C4Listener* listener, FLSlice name);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4db_URINameFromPath(FLSlice path);


    }
}
