﻿//
//  Status.cs
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
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;

using Couchbase.Lite.Internal.Logging;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite
{
    internal static class Status
    {
        #region Constants

        private const string Tag = nameof(Status);

        #endregion

        #region Public Methods

        public static unsafe void ConvertNetworkError(Exception e, C4Error* outError)
        {
            var c4err = new C4Error(C4ErrorDomain.LiteCoreDomain, (int)C4ErrorCode.UnexpectedError);
            var message = default(string);
            foreach (var inner in FlattenedExceptions(e)) {
                if (c4err.code != (int)C4ErrorCode.UnexpectedError || c4err.domain != C4ErrorDomain.LiteCoreDomain) {
                    break;
                }

                switch (inner) {
                    case CouchbaseException ce:
                        c4err = ce.LiteCoreError;
                        message = ce.Message;
                        break;
                    case SocketException se:
                        switch (se.SocketErrorCode) {
                            case SocketError.HostNotFound:
                                message = se.Message;
                                c4err.domain = C4ErrorDomain.NetworkDomain;
                                c4err.code = (int)C4NetworkErrorCode.UnknownHost;
                                break;
                            case SocketError.HostUnreachable:
                                message = se.Message;
                                c4err.domain = C4ErrorDomain.NetworkDomain;
                                c4err.code = (int)C4NetworkErrorCode.DNSFailure;
                                break;
                            case SocketError.TimedOut:
                                message = se.Message;
                                c4err.domain = C4ErrorDomain.NetworkDomain;
                                c4err.code = (int)C4NetworkErrorCode.Timeout;
                                break;
                            case SocketError.ConnectionAborted:
                            case SocketError.ConnectionReset:
                            case SocketError.Shutdown:
                                message = se.Message;
                                c4err.domain = C4ErrorDomain.POSIXDomain;
                                c4err.code = PosixBase.GetCode(nameof(PosixWindows.ECONNRESET));
                                break;
                            case SocketError.NetworkUnreachable:
                                message = se.Message;
                                c4err.domain = C4ErrorDomain.POSIXDomain;
                                c4err.code = PosixBase.GetCode(nameof(PosixWindows.ENETRESET));
                                break;
                            case SocketError.ConnectionRefused:
                                message = se.Message;
                                c4err.domain = C4ErrorDomain.POSIXDomain;
                                c4err.code = PosixBase.GetCode(nameof(PosixWindows.ECONNREFUSED));
                                break;
                            case SocketError.NetworkDown:
                                message = se.Message;
                                c4err.domain = C4ErrorDomain.POSIXDomain;
                                c4err.code = PosixBase.GetCode(nameof(PosixWindows.ENETDOWN));
                                break;
                        }

                        break;
                    case IOException ie:
                        if (ie.Message == "The handshake failed due to an unexpected packet format.") {
                            message = ie.Message;
                            c4err.domain = C4ErrorDomain.NetworkDomain;
                            c4err.code = (int) C4NetworkErrorCode.TLSHandshakeFailed;
                        }
                    #if __IOS__
                        if (ie.Message == "Connection closed.") {
                        //AppleTlsContext.cs
                        //case SslStatus.ClosedAbort:
                        //  throw new IOException("Connection closed.");
                        message = ie.Message;
                            c4err.domain = C4ErrorDomain.POSIXDomain;
                            c4err.code = PosixBase.GetCode(nameof(PosixWindows.ECONNRESET));
                        }
                    #endif
                        break;
                    case AuthenticationException ae:
                        if (ae.Message == "The certificate does not terminate in a trusted root CA.") {
                            message = ae.Message;
                            c4err.domain = C4ErrorDomain.NetworkDomain;
                            c4err.code = (int) C4NetworkErrorCode.TLSCertUnknownRoot;
                        } else {
                            message = ae.Message;
                            c4err.domain = C4ErrorDomain.NetworkDomain;
                            c4err.code = (int) C4NetworkErrorCode.TLSCertUntrusted;
                        }

                        break;
                }
            }

            if (c4err.code == (int) C4ErrorCode.UnexpectedError &&
                c4err.domain == C4ErrorDomain.LiteCoreDomain) {
                WriteLog.To.Database.W(Tag, $"No mapping for {e.GetType().Name}; interpreting as 'UnexpectedError'");
                if(message == null && e is AggregateException ae) {
                    message = ae.InnerExceptions.Select(x => x.Message).Aggregate((x, y) => $"{x}; {y}");
                }
            }

            // Use the message of the top-level exception because it will print out nested ones too
            *outError = Native.c4error_make(c4err.domain, c4err.code, message ?? e.Message);
        }

        #endregion

        #region Private Methods

        private static IEnumerable<Exception> FlattenedExceptions([NotNull]Exception top)
        {
            if (top is AggregateException ae) {
                foreach (var inner in ae.InnerExceptions) {
                    foreach (var nested in FlattenedExceptions(inner)) {
                        yield return nested;
                    }
                }
            }

            if (top.InnerException != null) {
                foreach (var inner in FlattenedExceptions(top.InnerException)) {
                    yield return inner;
                }
            }

            yield return top;
        }

        #endregion
    }
}