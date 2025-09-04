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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Sync;

using LiteCore.Interop;

namespace Couchbase.Lite;

internal static class Status
{
    private const string Tag = nameof(Status);

    public static unsafe void ConvertNetworkError(Exception e, C4Error* outError)
    {
        var c4Err = new C4Error(C4ErrorDomain.LiteCoreDomain, (int)C4ErrorCode.UnexpectedError);
        var message = default(string);
        foreach (var inner in FlattenedExceptions(e)) {
            if (c4Err.code != (int)C4ErrorCode.UnexpectedError || c4Err.domain != C4ErrorDomain.LiteCoreDomain) {
                break;
            }

            switch (inner) {
                case CouchbaseException ce:
                    c4Err = ce.LiteCoreError;
                    message = ce.Message;
                    break;
                case TlsCertificateException tlse:
                    message = tlse.Message;
                    c4Err.domain = C4ErrorDomain.NetworkDomain;
                    c4Err.code = (int)tlse.Code;
                    break;
                case SocketException se:
                    switch (se.SocketErrorCode) {
                        case SocketError.HostNotFound:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.UnknownHost;
                            break;
                        case SocketError.TimedOut:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.Timeout;
                            break;
                        case SocketError.ConnectionAborted:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.ConnectionAborted;
                            break;
                        case SocketError.ConnectionReset:
                        case SocketError.Shutdown:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.ConnectionReset;
                            break;
                        case SocketError.NetworkUnreachable:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.NetworkUnreachable;
                            break;
                        case SocketError.ConnectionRefused:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.ConnectionRefused;
                            break;
                        case SocketError.NetworkDown:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.NetworkDown;
                            break;
                        case SocketError.AddressNotAvailable:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.AddressNotAvailable;
                            break;
                        case SocketError.NetworkReset:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.NetworkReset;
                            break;
                        case SocketError.NotConnected:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.NotConnected;
                            break;
                        case SocketError.HostDown:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.HostDown;
                            break;
                        case SocketError.HostUnreachable:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.HostUnreachable;
                            break;
                        case SocketError.SocketError:
                            message = se.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.Unknown;
                            break;
                    }

                    break;
                case IOException ie:
                    if (ie.Message == "The handshake failed due to an unexpected packet format.") {
                        message = ie.Message;
                        c4Err.domain = C4ErrorDomain.NetworkDomain;
                        c4Err.code = (int) C4NetworkErrorCode.TLSHandshakeFailed;
                    }

#if CBL_PLATFORM_APPLE
                        if (ie.Message == "Connection closed.") {
                        //AppleTlsContext.cs
                        //case SslStatus.ClosedAbort:
                        //  throw new IOException("Connection closed.");
                        message = ie.Message;
                            c4Err.domain = C4ErrorDomain.NetworkDomain;
                            c4Err.code = (int)C4NetworkErrorCode.ConnectionReset;
                        }
#endif
                    break;
            }
        }

        if (c4Err is { code: (int) C4ErrorCode.UnexpectedError, domain: C4ErrorDomain.LiteCoreDomain }) {
            WriteLog.To.Database.W(Tag, $"No mapping for {e.GetType().Name}; interpreting as 'UnexpectedError'");
            if(message == null && e is AggregateException ae) {
                message = ae.InnerExceptions.Select(x => x.Message).Aggregate((x, y) => $"{x}; {y}");
            }
        }

        // Use the message of the top-level exception because it will print out nested ones too
        *outError = Native.c4error_make(c4Err.domain, c4Err.code, message ?? e.Message);
    }

    private static IEnumerable<Exception> FlattenedExceptions(Exception top)
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
}