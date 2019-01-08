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

	internal unsafe partial struct C4Error
    {
        public C4ErrorDomain domain;
        public int code;
        public int internal_info;
    }

	internal unsafe struct C4LogFileOptions
    {
        public C4LogLevel log_level;
        public FLSlice base_path;
        public long max_size_bytes;
        public int max_rotate_count;
        private byte _use_plaintext;
        public FLSlice header;

        public bool use_plaintext
        {
            get {
                return Convert.ToBoolean(_use_plaintext);
            }
            set {
                _use_plaintext = Convert.ToByte(value);
            }
        }
    }

	internal unsafe struct C4LogDomain
    {
    }
}