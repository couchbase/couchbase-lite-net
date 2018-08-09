//
// LiteCoreException.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;

using Couchbase.Lite.Interop;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// This set of error codes applies to <see cref="CouchbaseLiteException" />,
    /// <see cref="CouchbaseNetworkException"/> and <see cref="CouchbaseWebsocketException"/>
    /// </summary>
    public enum CouchbaseLiteError
    {
        /// <summary>
        /// Internal assertion failure
        /// </summary>
        AssertionFailed = C4ErrorCode.AssertionFailed,    

        /// <summary>
        /// An unimplemented API call
        /// </summary>
        Unimplemented = C4ErrorCode.Unimplemented,

        /// <summary>
        /// Unsupported encryption algorithm
        /// </summary>
        UnsupportedEncryption = C4ErrorCode.UnsupportedEncryption,

        /// <summary>
        /// An invalid revision ID was attempted to be used to insert a document
        /// (usually because of an invalid revision ID written directly into
        /// Sync Gateway via the REST API)
        /// </summary>
        BadRevisionID = C4ErrorCode.BadRevisionID,

        /// <summary>
        /// Revision contains corrupted/unreadable data
        /// </summary>
        CorruptRevisionData = C4ErrorCode.CorruptRevisionData,

        /// <summary>
        /// Database/KeyStore is not open
        /// </summary>
        NotOpen = C4ErrorCode.NotOpen,

        /// <summary>
        /// Document not found
        /// </summary>
        NotFound = C4ErrorCode.NotFound,

        /// <summary>
        /// Document update conflict
        /// </summary>
        Conflict = C4ErrorCode.Conflict,

        /// <summary>
        /// Invalid function parameter or struct value
        /// </summary>
        InvalidParameter = C4ErrorCode.InvalidParameter,

        /// <summary>
        /// Internal unexpected C++ exception
        /// </summary>
        UnexpectedError = C4ErrorCode.UnexpectedError,

        /// <summary>
        /// Database file can't be opened; may not exist
        /// </summary>
        CantOpenFile = C4ErrorCode.CantOpenFile,

        /// <summary>
        /// File I/O error
        /// </summary>
        IOError = C4ErrorCode.IOError,

        /// <summary>
        /// Memory allocation failed (out of memory?)
        /// </summary>
        MemoryError = C4ErrorCode.MemoryError,

        /// <summary>
        /// File is not writeable
        /// </summary>
        NotWriteable = C4ErrorCode.NotWriteable,

        /// <summary>
        /// Data is corrupted
        /// </summary>
        CorruptData = C4ErrorCode.CorruptData,

        /// <summary>
        /// Database is busy / locked
        /// </summary>
        Busy = C4ErrorCode.Busy,

        /// <summary>
        /// Function cannot be called while in a transaction
        /// </summary>
        NotInTransaction = C4ErrorCode.NotInTransaction,

        /// <summary>
        /// Database can't be closed while a transaction is open
        /// </summary>
        TransactionNotClosed = C4ErrorCode.TransactionNotClosed,

        /// <summary>
        /// Operation not supported on this database
        /// </summary>
        Unsupported = C4ErrorCode.Unsupported,

        /// <summary>
        /// File is not a database or encryption key is wrong
        /// </summary>
        UnreadableDatabase = C4ErrorCode.NotADatabaseFile,

        /// <summary>
        /// Database exists but not in the format/storage requested
        /// </summary>
        WrongFormat = C4ErrorCode.WrongFormat,

        /// <summary>
        /// Encryption / Decryption error
        /// </summary>
        Crypto = C4ErrorCode.Crypto,

        /// <summary>
        /// Invalid query
        /// </summary>
        InvalidQuery = C4ErrorCode.InvalidQuery,

        /// <summary>
        /// No such index, or query requires a nonexistent index
        /// </summary>
        MissingIndex = C4ErrorCode.MissingIndex,

        /// <summary>
        /// Unknown query param name, or param number out of range
        /// </summary>
        InvalidQueryParam = C4ErrorCode.InvalidQueryParam,

        /// <summary>
        /// Unknown error from remote server
        /// </summary>
        RemoteError = C4ErrorCode.RemoteError,

        /// <summary>
        /// Database file format is older than what I can open
        /// </summary>
        DatabaseTooOld = C4ErrorCode.DatabaseTooOld,

        /// <summary>
        /// Database file format is newer than what I can open
        /// </summary>
        DatabaseTooNew = C4ErrorCode.DatabaseTooNew,

        /// <summary>
        /// Invalid document ID
        /// </summary>
        BadDocID = C4ErrorCode.BadDocID,

        /// <summary>
        /// Database can't be upgraded (might be unsupported dev version)
        /// </summary>
        CantUpgradeDatabase = C4ErrorCode.CantUpgradeDatabase,

        /// <summary>
        /// Not an actual error, but serves as the lower bound for network related
        /// errors
        /// </summary>
        NetworkBase = 5000,

        /// <summary>
        /// DNS Lookup failed
        /// </summary>
        DNSFailure = C4NetworkErrorCode.DNSFailure + NetworkBase,

        /// <summary>
        /// DNS server doesn't know the hostname
        /// </summary>
        UnknownHost = C4NetworkErrorCode.UnknownHost + NetworkBase,

        /// <summary>
        /// Socket timeout during an operation
        /// </summary>
        Timeout = C4NetworkErrorCode.Timeout + NetworkBase,

        /// <summary>
        /// The provided URL is not valid
        /// </summary>
        InvalidUrl = C4NetworkErrorCode.InvalidURL + NetworkBase,

        /// <summary>
        /// Too many HTTP redirects for the HTTP client to handle
        /// </summary>
        TooManyRedirects = C4NetworkErrorCode.TooManyRedirects + NetworkBase,

        /// <summary>
        /// Failure during TLS handshake process
        /// </summary>
        TLSHandshakeFailed = C4NetworkErrorCode.TLSHandshakeFailed + NetworkBase,

        /// <summary>
        /// The provided TLS certificate has expired
        /// </summary>
        TLSCertExpired = C4NetworkErrorCode.TLSCertExpired + NetworkBase,

        /// <summary>
        /// Cert isn't trusted for other reason
        /// </summary>
        TLSCertUntrusted = C4NetworkErrorCode.TLSCertUntrusted + NetworkBase,

        /// <summary>
        /// A required client certificate was not provided
        /// </summary>
        TLSClientCertRequired = C4NetworkErrorCode.TLSClientCertRequired + NetworkBase,

        /// <summary>
        /// Client certificate was rejected by the server
        /// </summary>
        TLSClientCertRejected = C4NetworkErrorCode.TLSClientCertRejected + NetworkBase,

        /// <summary>
        /// Self-signed cert, or unknow anchor cert
        /// </summary>
        TLSCertUnknownRoot = C4NetworkErrorCode.TLSCertUnknownRoot + NetworkBase,

        /// <summary>
        /// The client was redirected to an invalid location by the server
        /// </summary>
        InvalidRedirect = C4NetworkErrorCode.InvalidRedirect + NetworkBase,

        /// <summary>
        /// Not an actual error, but serves as the lower bound for HTTP related
        /// errors
        /// </summary>
        HTTPBase = 10000,

        /// <summary>
        /// Missing or incorrect user authentication
        /// </summary>
        HTTPAuthRequired = 10401,

        /// <summary>
        /// User doesn't have permission to access resource
        /// </summary>
        HTTPForbidden = 10403,

        /// <summary>
        /// Resource not found
        /// </summary>
        HTTPNotFound = 10404,

        /// <summary>
        /// HTTP proxy requires authentication
        /// </summary>
        HTTPProxyAuthRequired = 10407,

        /// <summary>
        /// Update conflict
        /// </summary>
        HTTPConflict = 10409,

        /// <summary>
        /// Data is too large to upload
        /// </summary>
        HTTPEntityTooLarge = 10413,

        /// <summary>
        /// Something's wrong with the server
        /// </summary>
        HTTPInternalServerError = 10500,

        /// <summary>
        /// Unimplemented server functionality
        /// </summary>
        HTTPNotImplemented = 10501,

        /// <summary>
        /// Service is down temporarily
        /// </summary>
        HTTPServiceUnavailable = 10503,

        /// <summary>
        /// Not an actual error, but serves as the lower bound for WebSocket
        /// related errors
        /// </summary>
        WebSocketBase = 11000,

        /// <summary>
        /// Peer has to close, e.g. because host app is quitting
        /// </summary>
        WebSocketGoingAway = C4WebSocketCloseCode.WebSocketCloseGoingAway + HTTPBase,

        /// <summary>
        /// Protocol violation: invalid framing data
        /// </summary>
        WebSocketProtocolError = C4WebSocketCloseCode.WebSocketCloseProtocolError + HTTPBase,

        /// <summary>
        /// Message payload cannot be handled
        /// </summary>
        WebSocketDataError = C4WebSocketCloseCode.WebSocketCloseDataError + HTTPBase,

        /// <summary>
        /// TCP socket closed unexpectedly
        /// </summary>
        WebSocketAbnormalClose = C4WebSocketCloseCode.WebSocketCloseAbnormal + HTTPBase,

        /// <summary>
        /// Unparseable WebSocket message
        /// </summary>
        WebSocketBadMessageFormat = C4WebSocketCloseCode.WebSocketCloseBadMessageFormat + HTTPBase,

        /// <summary>
        /// Message violated unspecified policy
        /// </summary>
        WebSocketPolicyError = C4WebSocketCloseCode.WebSocketClosePolicyError + HTTPBase,

        /// <summary>
        /// Message is too large for peer to handle
        /// </summary>
        WebSocketMessageTooBig = C4WebSocketCloseCode.WebSocketCloseMessageTooBig + HTTPBase,

        /// <summary>
        /// Peer doesn't provide a necessary extension
        /// </summary>
        WebSocketMissingExtension = C4WebSocketCloseCode.WebSocketCloseMissingExtension + HTTPBase,

        /// <summary>
        /// Can't fulfill request due to "unexpected condition"
        /// </summary>
        WebSocketCantFulfill = C4WebSocketCloseCode.WebSocketCloseCantFulfill + HTTPBase,

        /// <summary>
        /// Exceptions during P2P replication that are transient will be assigned this error code
        /// </summary>
        WebSocketUserTransient = C4WebSocketCustomCloseCode.WebSocketCloseUserTransient + HTTPBase,

        /// <summary>
        /// Exceptions during P2P replication that are permanent will be assigned this error code
        /// </summary>
        WebSocketUserPermanent = C4WebSocketCustomCloseCode.WebSocketCloseUserPermanent + HTTPBase
    }

    /// <summary>
    /// These are the domains into which a <see cref="CouchbaseException"/>
    /// can fall.  Each domain has one or more corresponding exception subclasses.
    /// You can trap a <see cref="CouchbaseException"/> and check its <see cref="CouchbaseException.Domain"/>
    /// to see which kind of subclass you should cast to, if desirable.  Subclasses have a fixed domain.
    /// </summary>
    public enum CouchbaseLiteErrorType
    {
        /// <summary>
        /// This error was generated by LiteCore and involves data verification,
        /// disk and network I/O, and HTTP / WebSocket statuses
        /// </summary>
        CouchbaseLite,

        /// <summary>
        /// A POSIX error code was received during operation.  For Windows this is
        /// best effort as Win32 API does not set POSIX error codes.
        /// </summary>
        POSIX,

        /// <summary>
        /// An error occurred during a SQLite operation and is being bubbled up
        /// </summary>
        SQLite,

        /// <summary>
        /// An error occurred during serialization or deserialization of data
        /// </summary>
        Fleece
    }

    /// <summary>
    /// An exception representing one of the types of exceptions that can occur
    /// during Couchbase use
    /// </summary>
    public abstract class CouchbaseException : Exception
    {
        #region Constants

        private static readonly IReadOnlyList<int> _BugReportErrors = new List<int>
        {
            (int)C4ErrorCode.AssertionFailed,
            (int)C4ErrorCode.Unimplemented,
            (int)C4ErrorCode.Unsupported,
            (int)C4ErrorCode.UnsupportedEncryption,
            (int)C4ErrorCode.BadRevisionID,
            (int)C4ErrorCode.UnexpectedError
        };

        #endregion

        #region Variables

        private delegate string ErrorMessageVisitor(C4Error err);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the domain of the error that happened (which indicates which subclass
        /// this exception will be)
        /// </summary>
        public CouchbaseLiteErrorType Domain { get; }

        /// <summary>
        /// Gets the raw error code as an integer
        /// </summary>
        public int Error { get; }
        
        internal C4Error LiteCoreError { get; }

        #endregion

        #region Constructors

        internal CouchbaseException(C4Error err) : this(err, GetMessage(err))
        {
           
        }

        internal CouchbaseException(C4Error err, string message) 
            : this(err, message, null)
        {
        }
        
        internal CouchbaseException(C4Error err, string message, Exception innerException) : base(message, innerException)
        {
            LiteCoreError = err;
            Error = MapError(err);
            Domain = MapDomain(err);
        }

        #endregion

        [NotNull]
        internal static CouchbaseException Create(C4Error err)
        {
            switch (err.domain) {
                case C4ErrorDomain.FleeceDomain:
                    return new CouchbaseFleeceException(err);
                case C4ErrorDomain.LiteCoreDomain:
                    return new CouchbaseLiteException(err);
                case C4ErrorDomain.NetworkDomain:
                    return new CouchbaseNetworkException(err);
                case C4ErrorDomain.POSIXDomain:
                    return new CouchbasePosixException(err);
                case C4ErrorDomain.SQLiteDomain:
                    return new CouchbaseSQLiteException(err);
                case C4ErrorDomain.WebSocketDomain:
                    return new CouchbaseWebsocketException(err);
                default:
                    return new CouchbaseLiteException(C4ErrorCode.UnexpectedError);
            }
        }

        #region Private Methods

        private static string GetMessage(C4Error err)
        {
            foreach (var visitor in MessageVisitors()) {
                var msg = visitor(err);
                if (msg != null) {
                    return msg;
                }
            }

            Debug.Assert(false, "Panic!  No suitable error message found");
            return null;
        }

        private static CouchbaseLiteErrorType MapDomain(C4Error err)
        {
            switch (err.domain) {
                case C4ErrorDomain.FleeceDomain:
                    return CouchbaseLiteErrorType.Fleece;
                case C4ErrorDomain.POSIXDomain:
                    return CouchbaseLiteErrorType.POSIX;
                case C4ErrorDomain.SQLiteDomain:
                    return CouchbaseLiteErrorType.SQLite;
                default:
                    return CouchbaseLiteErrorType.CouchbaseLite;
            }
        }

        private static int MapError(C4Error err)
        {
            switch (err.domain) {
                case C4ErrorDomain.NetworkDomain:
                    return err.code + (int) CouchbaseLiteError.NetworkBase;
                case C4ErrorDomain.WebSocketDomain:
                    return err.code + (int) CouchbaseLiteError.HTTPBase;
                default:
                    return err.code;
            }
        }

        [NotNull]
        [ItemNotNull]
        private static IEnumerable<ErrorMessageVisitor> MessageVisitors()
        {
            yield return VisitBugReportList;
            yield return VisitCantUpgrade;
            yield return VisitDefault;
        }

        private static string VisitBugReportList(C4Error err)
        {
            if (err.domain == C4ErrorDomain.LiteCoreDomain && _BugReportErrors.Contains(err.code)) {
                return
                    $"CouchbaseLiteException ({err.domain} / {err.code}): {Native.c4error_getMessage(err)}.  Please file a bug report at https://github.com/couchbase/couchbase-lite-net/";
            }

            return null;
        }

        private static string VisitCantUpgrade(C4Error err)
        {
            if (err.domain == C4ErrorDomain.LiteCoreDomain && err.code == (int) C4ErrorCode.CantUpgradeDatabase) {
                return
                    $"CouchbaseLiteException ({err.domain} / {err.code}): {Native.c4error_getMessage(err)}.  If the previous database version was a version produced by a production version of Couchbase Lite, then please file a bug report at https://github.com/couchbase/couchbase-lite-net/";
            }

            return null;
        }

        private static string VisitDefault(C4Error err) => $"CouchbaseLiteException ({err.domain} / {err.code}): {Native.c4error_getMessage(err)}.";

        #endregion
    }

    /// <summary>
    /// An exception that is thrown when a <see cref="CouchbaseLiteError"/> is detected.
    /// This class will always have the <see cref="CouchbaseLiteErrorType.CouchbaseLite"/>
    /// domain set.
    /// </summary>
    public sealed class CouchbaseLiteException : CouchbaseException
    {
        #region Properties

        /// <summary>
        /// Gets the error code as a <see cref="CouchbaseLiteError"/>
        /// </summary>
        public new CouchbaseLiteError Error => (CouchbaseLiteError) base.Error;

        #endregion

        #region Constructors

        internal CouchbaseLiteException(C4Error err) : base(err)
        {

        }

        internal CouchbaseLiteException(C4ErrorCode errCode) : base(new C4Error(errCode))
        {

        }

        internal CouchbaseLiteException(C4ErrorCode errCode, string message) : base(new C4Error(errCode), message)
        {

        }

        internal CouchbaseLiteException(C4ErrorCode errCode, string message, Exception innerException) : base(new C4Error(errCode), message)
        {

        }

        #endregion
    }

    /// <summary>
    /// An exception that is thrown when a Fleece error is detected.  Fleece is the
    /// library used to serialize and deserialize data.  Any error of this type is not
    /// reactable by the user and so the error codes are not enumerated.  This type of
    /// exception should be reported.  This class has a domain type of <see cref="CouchbaseLiteErrorType.Fleece"/>
    /// </summary>
    public sealed class CouchbaseFleeceException : CouchbaseException
    {
        #region Constructors

        internal CouchbaseFleeceException(C4Error err) : base(err)
        {

        }

        internal CouchbaseFleeceException(FLError errCode) : base(new C4Error(errCode))
        {

        }

        internal CouchbaseFleeceException(FLError errCode, string message) : base(new C4Error(errCode), message)
        {

        }

        #endregion
    }

    /// <summary>
    /// An exception used to indicate a SQLite operation error.  The possible errors are enumerated as
    /// <see cref="SQLiteStatus"/> for convenience.  This class has a domain type of <see cref="CouchbaseLiteErrorType.SQLite"/>
    /// </summary>
    public sealed class CouchbaseSQLiteException : CouchbaseException
    {
        #region Properties

        /// <summary>
        /// Gets the error as a <see cref="SQLiteStatus"/>
        /// </summary>
        public SQLiteStatus BaseError => (SQLiteStatus) (Error & 0xFF);

        #endregion

        #region Constructors

        internal CouchbaseSQLiteException(C4Error err) : base(err)
        {

        }

        internal CouchbaseSQLiteException(int errCode) : base(new C4Error(C4ErrorDomain.SQLiteDomain, errCode))
        {

        }

        internal CouchbaseSQLiteException(int errCode, string message) : base(new C4Error(C4ErrorDomain.SQLiteDomain, errCode), message)
        {

        }

        #endregion
    }

    /// <summary>
    /// An exception that is thrown when there is an abnormal websocket condition detected.
    /// This exception has a domain of <see cref="CouchbaseLiteErrorType.CouchbaseLite"/>.
    /// </summary>
    public sealed class CouchbaseWebsocketException : CouchbaseException
    {
        #region Properties

        /// <summary>
        /// Gets the error as a <see cref="CouchbaseLiteError"/>
        /// </summary>
        public new CouchbaseLiteError Error => (CouchbaseLiteError) base.Error;

        #endregion

        #region Constructors

        internal CouchbaseWebsocketException(C4Error err) : base(err)
        {

        }

        internal CouchbaseWebsocketException(int errCode) : base(new C4Error(C4ErrorDomain.WebSocketDomain, errCode))
        {

        }

        internal CouchbaseWebsocketException(int errCode, string message) : base(new C4Error(C4ErrorDomain.WebSocketDomain, errCode), message)
        {

        }

        #endregion
    }

    /// <summary>
    /// An exception that is thrown when there is an abnormal network condition detected.
    /// This exception has a domain of <see cref="CouchbaseLiteErrorType.CouchbaseLite"/>
    /// </summary>
    public sealed class CouchbaseNetworkException : CouchbaseException
    {
        #region Properties

        /// <summary>
        /// Gets the error as a <see cref="CouchbaseLiteError"/>
        /// </summary>
        public new CouchbaseLiteError Error => (CouchbaseLiteError) base.Error;

        #endregion

        #region Constructors

        internal CouchbaseNetworkException(HttpStatusCode httpCode) : base(new C4Error(C4ErrorDomain.WebSocketDomain, (int)httpCode))
        {

        }

        internal CouchbaseNetworkException(C4Error err) : base(err)
        {

        }

        internal CouchbaseNetworkException(C4NetworkErrorCode errCode) : base(new C4Error(errCode))
        {

        }

        internal CouchbaseNetworkException(C4NetworkErrorCode errCode, string message) : base(new C4Error(errCode), message)
        {

        }

        #endregion
    }

    /// <summary>
    /// An exception that is thrown when a POSIX error code is received during operation.
    /// This exception has a domain of <see cref="CouchbaseLiteErrorType.POSIX"/>.  The <see cref="CouchbaseException.Error"/>
    /// values are dependent on the OS being run on.  They are defined in <see cref="PosixWindows"/>,
    /// <see cref="PosixMac"/> and <see cref="PosixLinux"/>
    /// </summary>
    public sealed class CouchbasePosixException : CouchbaseException
    {
        #region Constructors

        internal CouchbasePosixException(C4Error err) : base(err)
        {

        }

        internal CouchbasePosixException(int errCode) : base(new C4Error(C4ErrorDomain.POSIXDomain, errCode))
        {

        }

        internal CouchbasePosixException(int errCode, string message) : base(new C4Error(C4ErrorDomain.POSIXDomain, errCode), message)
        {

        }

        #endregion
    }
}
