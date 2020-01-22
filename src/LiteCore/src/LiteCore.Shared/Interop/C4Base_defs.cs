//
// C4Base_defs.cs
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
using System.Threading;

using LiteCore.Util;

namespace LiteCore.Interop
{
    internal enum C4ErrorDomain : uint
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
        TLSClientCertRequired,
        TLSClientCertRejected,
        TLSCertUnknownRoot,
        InvalidRedirect,
        Unknown,
        TLSCertRevoked,
        TLSCertNameMismatch,
        NumNetErrorCodesPlus1
    }

    internal enum C4LogLevel : sbyte
    {
        Debug,
        Verbose,
        Info,
        Warning,
        Error,
        None
    }

    internal unsafe struct C4ExtraInfo
    {
        public void* pointer;
        private IntPtr _destructor;

        public C4ExtraInfoDestructor destructor
        {
            get => Marshal.GetDelegateForFunctionPointer<C4ExtraInfoDestructor>(_destructor);
            set => _destructor = Marshal.GetFunctionPointerForDelegate(value);
        }
    }
    

	internal unsafe partial struct C4BlobKey
    {
        public fixed byte bytes[20];
    }

	internal unsafe partial struct C4Address
    {
    }

	internal unsafe struct C4BlobStore
    {
    }

	internal unsafe struct C4Cert
    {
    }

	internal unsafe struct C4Database
    {
    }

	internal unsafe struct C4DatabaseObserver
    {
    }

	internal unsafe partial struct C4Document
    {
    }

	internal unsafe struct C4DocumentObserver
    {
    }

	internal unsafe struct C4DocEnumerator
    {
    }

	internal unsafe struct C4KeyPair
    {
    }

	internal unsafe struct C4Listener
    {
    }

	internal unsafe struct C4Query
    {
    }

	internal unsafe partial struct C4QueryEnumerator
    {
    }

	internal unsafe struct C4QueryObserver
    {
    }

	internal unsafe partial struct C4RawDocument
    {
    }

	internal unsafe struct C4ReadStream
    {
    }

	internal unsafe struct C4Replicator
    {
    }

	internal unsafe partial struct C4Socket
    {
    }

	internal unsafe partial struct C4SocketFactory
    {
    }

	internal unsafe struct C4WriteStream
    {
    }

	internal unsafe partial struct C4Error
    {
        public C4ErrorDomain domain;
        public int code;
        public int internal_info;
    }

	internal unsafe struct C4LogDomain
    {
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
}