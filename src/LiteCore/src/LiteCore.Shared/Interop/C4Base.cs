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

    internal unsafe partial struct C4Slice : IEnumerable<byte>
    {
        public static readonly C4Slice Null = new C4Slice(null, 0);

        public C4Slice(void* buf, ulong size)
        {
            this.buf = buf;
            _size = new UIntPtr(size);
        }

        public static C4Slice Constant(string input)
        {
            return (C4Slice)FLSlice.Constant(input);
        }

        public static C4Slice Allocate(string input)
        {
            return (C4Slice)FLSlice.Allocate(input);
        }

        public static void Free(C4Slice slice)
        {
            FLSlice.Free((FLSlice)slice);
            slice.buf = null;
            slice.size = 0;
        }

        private bool Equals(C4Slice other)
        {
            return Native.c4SliceEqual(this, other);
        }

        private bool Equals(string other)
        {
            var c4Str = new C4String(other);
            return Equals(c4Str.AsC4Slice());
        }

        public string CreateString()
        {
            if(buf == null) {
                return null;
            }

            return Encoding.UTF8.GetString((byte*) buf, (int) size);
        }

        public byte[] ToArrayFast()
        {
            if(buf == null) {
                return null;
            }

            var tmp = new IntPtr(buf);
            var bytes = new byte[size];
            Marshal.Copy(tmp, bytes, 0, bytes.Length);
            return bytes;
        }

        public static explicit operator C4SliceResult(C4Slice input)
        {
            return new C4SliceResult(input.buf, input.size);
        }

        public static explicit operator FLSlice(C4Slice input)
        {
            return new FLSlice(input.buf, input.size);
        }

        public static explicit operator FLSliceResult(C4Slice input)
        {
            return new FLSliceResult(input.buf, input.size);
        }

#pragma warning disable 1591

        public override string ToString()
        {
            return String.Format("C4Slice[\"{0}\"]", CreateString());
        }

        public override bool Equals(object obj)
        {
            if(obj is C4Slice) {
                return Equals((C4Slice)obj);
            }

            var str = obj as string;
            return str != null && Equals(str);
        }

        public override int GetHashCode()
        {
            unchecked {
                int hash = 17;

                hash = hash * 23 + (int)size;
                var ptr = (byte*)buf;
                if(ptr != null) {
                    hash = hash * 23 + ptr[size - 1];
                }

                return hash;
            }
        }

        public IEnumerator<byte> GetEnumerator()
        {
            return new C4SliceEnumerator(buf, (int)size);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
#pragma warning restore 1591
    }

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "These objects are meant to mimick their C counterparts exactly when possible")]
    internal unsafe struct C4SliceResult : IDisposable
    {
        public void* buf;
        private UIntPtr _size;

        public C4SliceResult(void* buf, ulong size)
        {
            this.buf = buf;
            _size = (UIntPtr)size;
        }

        public ulong size
        {
            get => _size.ToUInt64();
            set => _size = (UIntPtr)value;
        }

        public static explicit operator C4Slice(C4SliceResult input)
        {
            return new C4Slice(input.buf, input.size);
        }

        public static explicit operator FLSlice(C4SliceResult input)
        {
            return new FLSlice(input.buf, input.size);
        }

        public static explicit operator FLSliceResult(C4SliceResult input)
        {
            return new FLSliceResult(input.buf, input.size);
        }

        public void Dispose()
        {
            Native.c4slice_free(this);
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

