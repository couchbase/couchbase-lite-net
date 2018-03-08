//
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;
using Couchbase.Lite.Logging;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite
{
    internal static class Status
    {
        private const string Tag = nameof(Status);

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

        public static unsafe void ConvertNetworkError(Exception e, C4Error* outError)
        {
            var c4err = new C4Error(C4ErrorDomain.WebSocketDomain, (int)C4WebSocketCloseCode.WebSocketCloseAbnormal);
            foreach (var inner in FlattenedExceptions(e)) {
                if (c4err.code != (int)C4WebSocketCloseCode.WebSocketCloseAbnormal || c4err.domain != C4ErrorDomain.WebSocketDomain) {
                    break;
                }

                switch (inner) {
                    case SocketException se:
                        switch (se.SocketErrorCode) {
                            case SocketError.HostNotFound:
                                c4err.domain = C4ErrorDomain.NetworkDomain;
                                c4err.code = (int)C4NetworkErrorCode.UnknownHost;
                                break;
                            case SocketError.HostUnreachable:
                                c4err.domain = C4ErrorDomain.NetworkDomain;
                                c4err.code = (int)C4NetworkErrorCode.DNSFailure;
                                break;
                            case SocketError.TimedOut:
                                c4err.domain = C4ErrorDomain.NetworkDomain;
                                c4err.code = (int)C4NetworkErrorCode.Timeout;
                                break;
                            case SocketError.ConnectionAborted:
                            case SocketError.ConnectionReset:
                                c4err.domain = C4ErrorDomain.POSIXDomain;
                                c4err.code = (int)PosixStatus.CONNRESET;
                                break;
                            case SocketError.ConnectionRefused:
                                c4err.domain = C4ErrorDomain.POSIXDomain;
                                c4err.code = (int)PosixStatus.CONNREFUSED;
                                break;
                        }

                        break;
                    case IOException ie:
                        if (ie.Message == "The handshake failed due to an unexpected packet format.") {
                            c4err.domain = C4ErrorDomain.NetworkDomain;
                            c4err.code = (int) C4NetworkErrorCode.TLSHandshakeFailed;
                        }
                        break;
                    case AuthenticationException ae:
                        if (ae.Message == "The remote certificate is invalid according to the validation procedure.") {
                            c4err.domain = C4ErrorDomain.NetworkDomain;
                            c4err.code = (int) C4NetworkErrorCode.TLSCertUntrusted;
                        }

                        break;
                }
            }

            if (c4err.code == (int) C4WebSocketCloseCode.WebSocketCloseAbnormal &&
                c4err.domain == C4ErrorDomain.WebSocketDomain) {
                Log.To.Couchbase.W(Tag, $"No mapping for {e.GetType().Name}; interpreting as WebSocketAbnormal");
            }

            *outError = Native.c4error_make(c4err.domain, c4err.code, e.Message);
        }
    }
}