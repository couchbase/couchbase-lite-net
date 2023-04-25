//
// C4Error_defs.cs
//
// Copyright (c) 2023 Couchbase, Inc All rights reserved.
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
    internal enum C4ErrorDomain : byte
    {
        LiteCoreDomain = 1,
        POSIXDomain,
        SQLiteDomain,
        FleeceDomain,
        NetworkDomain,
        WebSocketDomain,
        MbedTLSDomain,
        MaxErrorDomainPlus1
    }

    internal enum C4ErrorCode : int
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
        
        DeltaBaseUnknown,
        CorruptDelta,
        NumErrorCodesPlus1
    }

    internal enum C4NetworkErrorCode : int
    {
        DNSFailure = 1,
        UnknownHost,
        Timeout,
        InvalidURL,
        TooManyRedirects,
        TLSHandshakeFailed,
        TLSCertExpired,
        TLSCertUntrusted,
        TLSCertRequiredByPeer,
        TLSCertRejectedByPeer,
        TLSCertUnknownRoot,
        InvalidRedirect,
        Unknown,
        TLSCertRevoked,
        TLSCertNameMismatch,
        NetworkReset,
        ConnectionAborted,
        ConnectionReset,
        ConnectionRefused,
        NetworkDown,
        NetworkUnreachable,
        NotConnected,
        HostDown,
        HostUnreachable,
        AddressNotAvailable,
        BrokenPipe,
        UnknownInterface,
        NumNetErrorCodesPlus1
    }

	internal unsafe partial struct C4Error
    {
        public C4ErrorDomain domain;
        public int code;
        public uint internal_info;
    }
}