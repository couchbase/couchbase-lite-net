// 
//  C4Base.cs
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
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

using Couchbase.Lite.DI;

using LiteCore;
using LiteCore.Util;

namespace LiteCore.Interop
{
    using Couchbase.Lite.Interop;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal unsafe delegate void C4LogCallback(C4LogDomain* domain, C4LogLevel level, string message, IntPtr args);

    internal partial struct C4Error
    {
        public C4Error(C4ErrorDomain domain, int code)
        {
            this.code = code;
            this.domain = domain;
            internal_info = 0;
        }

        public C4Error(C4ErrorCode code) : this(C4ErrorDomain.LiteCoreDomain, (int)code)
        {

        }

        public C4Error(FLError code) : this(C4ErrorDomain.FleeceDomain, (int)code)
        {

        }

        public C4Error(C4NetworkErrorCode code) : this(C4ErrorDomain.NetworkDomain, (int) code)
        {
            
        }

        public override int GetHashCode()
        {
            return Hasher.Start
                .Add(code)
                .Add(domain)
                .GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is C4Error other) {
                return other.code == code && other.domain == domain;
            }

            return false;
        }
    }
}

namespace Couchbase.Lite.Interop
{
    using LiteCore.Interop;

    internal static partial class Native
    {
        public static unsafe C4LogDomain* c4log_getDomain(byte* name, bool create) =>
            Service.GetRequiredInstance<ILiteCore>().c4log_getDomain(name, create);
    }
}

// EPIC HACK: This is required for iOS callbacks, but not available in .NET Standard
// So I just reverse engineer it (Although reverse engineer is probably too strong a word)
namespace ObjCRuntime
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        #region Constructors

        [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "This attribute is only used by mtouch.exe")]
        public MonoPInvokeCallbackAttribute(Type t)
        {
        }

        #endregion
    }
}

