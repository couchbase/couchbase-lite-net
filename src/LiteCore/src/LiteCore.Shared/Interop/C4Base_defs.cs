//
// C4Base_defs.cs
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
using System.Threading;

using LiteCore.Util;

namespace LiteCore.Interop
{
#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum C4NetworkErrorCode : int
    {
        DNSFailure = 1,
        UnknownHost,
        Timeout,
        InvalidURL,
        TooManyRedirects,
        TLSHandshakeFailed,
        TLSCertExpired,
        TLSCertUntrusted,
        TLSClientCertRequired,
        TLSClientCertRejected,
        TLSCertUnknownRoot,
        InvalidRedirect,
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum C4ErrorCode : int
    {
        AssertionFailed = 1,
        Unimplemented,
        UnsupportedEncryption,
        BadRevisionID,
        CorruptRevisionData,
        NotOpen,
        NotFound,
        Conflict,
        InvalidParameter,
        UnexpectedError,
        CantOpenFile,
        IOError,
        MemoryError,
        NotWriteable,
        CorruptData,
        Busy,
        NotInTransaction,
        TransactionNotClosed,
        Unsupported,
        NotADatabaseFile,
        WrongFormat,
        Crypto,
        InvalidQuery,
        MissingIndex,
        InvalidQueryParam,
        RemoteError,
        DatabaseTooOld,
        DatabaseTooNew,
        BadDocID,
        CantUpgradeDatabase,
        NumErrorCodesPlus1
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum C4ErrorDomain : uint
    {
        LiteCoreDomain = 1,
        POSIXDomain,
        SQLiteDomain,
        FleeceDomain,
        NetworkDomain,
        WebSocketDomain,
        MaxErrorDomainPlus1
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum C4LogLevel : sbyte
    {
        Debug,
        Verbose,
        Info,
        Warning,
        Error,
        None
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe partial struct C4Error
    {
        public C4ErrorDomain domain;
        public int code;
        public int internal_info;
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe partial struct C4Slice
    {
        public void* buf;
        private UIntPtr _size;

        public ulong size
        {
            get {
                return _size.ToUInt64();
            }
            set {
                _size = (UIntPtr)value;
            }
        }
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe struct C4LogDomain
    {
    }
}