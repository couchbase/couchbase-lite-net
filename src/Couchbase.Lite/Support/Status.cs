//
//  CouchbaseLiteException.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using System.Net.Sockets;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// A list of statuses indicating various results and/or errors for Couchbase Lite
    /// operations
    /// </summary>
    public enum StatusCode
    {
        /// <summary>
        /// Unknown result (should not be used)
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// A required dependency injection class is missing
        /// </summary>
        MissingDependency = 1,

        /// <summary>
        /// For internal use
        /// </summary>
        Reserved = 0,

        /// <summary>
        /// The requested action is not allowed to be executed by any user (HTTP compliant)
        /// </summary>
        Forbidden = 403,

        /// <summary>
        /// The requested item does not appear to exist (HTTP compliant)
        /// </summary>
        NotFound = 404
    }

    internal static class Status
    {
        public static unsafe void ConvertError(Exception e, C4Error* outError)
        {
            C4Error c4err = new C4Error(C4ErrorCode.RemoteError);
            switch (e) {
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

                default:
                    //HACK: System.Net.Security not available on current UWP, so it can't be used
                    if (e.GetType().Name == "AuthenticationException") {
                        if (e.Message == "The remote certificate is invalid according to the validation procedure.") {
                            c4err.domain = C4ErrorDomain.NetworkDomain;
                            c4err.code = (int) C4NetworkErrorCode.TLSCertUntrusted;
                        }
                    }

                    break;
            }

            *outError = Native.c4error_make(c4err.domain, c4err.code, e.Message);
        }
    }
}