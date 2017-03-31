//
// ExceptionResolver.cs
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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal
{
    internal enum ErrorResolution
    {
        Ignore,             // Used for intentional calls to Stop
        RetryNow,           // Cleared for immediate retry (e.g. long poll finished)
        BackoffAndRetry,    // Retry according to backoff strategy
        RetryLater,         // Backoff failed, enter retry loop
        GoOffline,          // Connectivity problem, go offline
        Stop                // Meltdown, do not continue
    }

    [Flags]
    internal enum ErrorResolutionFlags
    {
        None = 0,               // No error
        Transient = 1 << 0,     // Error expected to clear up soon
        Connectivity = 1 << 1,  // Error related to connectivity
        Permanent = 1 << 2,     // Error not expected to clear up without manual intervention
        OutOfRetries = 1 << 3   // Retry strategy exhausted
    }

    internal interface IErrorResolution
    {
        ErrorResolution Resolution { get; }

        ErrorResolutionFlags ResolutionFlags { get; }

        string Status { get; } // For logging
    }

    internal sealed class ExceptionResolverOptions
    {
        internal bool Continuous { get; set; }

        internal bool HasRetries { get; set; }
    }

    internal static class ExceptionResolver
    {
        private const string Tag = nameof(ExceptionResolver);

        internal static IErrorResolution Solve(Exception e, ExceptionResolverOptions options)
        {
            if(e == null) {
                // No error occurred, keep going if continuous
                return options.Continuous ? 
                              new ErrorResolution_Impl(null, ErrorResolutionFlags.None) { Resolution = ErrorResolution.RetryNow }
                    : new ErrorResolution_Impl(null, ErrorResolutionFlags.None) { Resolution = ErrorResolution.Stop };
            }

            var resolution = Analyze(e);
            if(resolution.ResolutionFlags.HasFlag(ErrorResolutionFlags.Transient)) {
                if(!options.HasRetries) {
                    resolution.ResolutionFlags |= ErrorResolutionFlags.OutOfRetries;
                    resolution.Resolution = options.Continuous ? ErrorResolution.RetryLater : ErrorResolution.Stop;
                } else {
                    resolution.Resolution = ErrorResolution.BackoffAndRetry;
                }
            } else if(resolution.ResolutionFlags.HasFlag(ErrorResolutionFlags.Connectivity)) {
                resolution.Resolution = ErrorResolution.GoOffline;
            } else {
                resolution.Resolution = ErrorResolution.Stop;
            }

            return resolution;
        }

        internal static IErrorResolution Solve(HttpStatusCode statusCode, ExceptionResolverOptions options)
        {
            if(!IsTransientError(statusCode)) {
                return new ErrorResolution_Impl(statusCode.ToString(), ErrorResolutionFlags.Permanent) {
                    Resolution = ErrorResolution.Stop
                };
            }

            if(options.HasRetries) {
                return new ErrorResolution_Impl(statusCode.ToString(), ErrorResolutionFlags.Transient) {
                    Resolution = ErrorResolution.BackoffAndRetry
                };
            }

            if(options.Continuous) {
                return new ErrorResolution_Impl(statusCode.ToString(), ErrorResolutionFlags.Transient | ErrorResolutionFlags.OutOfRetries) {
                    Resolution = ErrorResolution.RetryLater
                };
            } else {
                return new ErrorResolution_Impl(statusCode.ToString(), ErrorResolutionFlags.Transient | ErrorResolutionFlags.OutOfRetries) {
                    Resolution = ErrorResolution.Stop
                };
            }
        }

        internal static HttpStatusCode? GetStatusCode(Exception e)
        {
            var attempt = ((HttpWebResponse)(e as WebException)?.Response)?.StatusCode;
            if(attempt.HasValue) {
                return attempt;
            }

            return (e as HttpResponseException)?.StatusCode;
        }

        private static ErrorResolution_Impl Analyze(Exception e)
        {
            string code = null;
            foreach(var exception in Misc.Flatten(e)) {
                if(exception is IOException
                    || exception is TimeoutException
                    || exception is TaskCanceledException) {
                    Log.To.Sync.V(Tag, "Rule #1: Exception is IOException, TimeoutException, or TaskCanceledException, " +
                    "ruling transient...", exception);

                    return new ErrorResolution_Impl(e.GetType().Name, ErrorResolutionFlags.Transient);
                }

                var se = exception as SocketException;
                if(se != null) {
                    switch(se.SocketErrorCode) {
                    case SocketError.AlreadyInProgress:
                    case SocketError.ConnectionAborted:
                    case SocketError.ConnectionReset:
                    case SocketError.InProgress:
                    case SocketError.Interrupted:
                    case SocketError.IOPending:
                    case SocketError.IsConnected:
                    case SocketError.NetworkReset:
                    case SocketError.OperationAborted:
                    case SocketError.ProcessLimit:
                    case SocketError.Shutdown:
                    case SocketError.SystemNotReady:
                    case SocketError.TimedOut:
                    case SocketError.TooManyOpenSockets:
                    case SocketError.TryAgain:
                    case SocketError.WouldBlock:
                        Log.To.Sync.V(Tag, "Rule #2: SocketException with error code {0}, ruling transient...", se.SocketErrorCode);
                        return new ErrorResolution_Impl(se.SocketErrorCode.ToString(), ErrorResolutionFlags.Transient);
                    case SocketError.ConnectionRefused:
                    case SocketError.HostDown:
                    case SocketError.NetworkDown:
                    case SocketError.NotConnected:
                        Log.To.Sync.V(Tag, "Rule #2: SocketException with error code {0}, ruling connectivity issue...", se.SocketErrorCode);
                        return new ErrorResolution_Impl(se.SocketErrorCode.ToString(), ErrorResolutionFlags.Connectivity);
                    default:
                        Log.To.Sync.V(Tag, "Rule #2: SocketException with error code {0}, ruling permanent...", se.SocketErrorCode);
                        return new ErrorResolution_Impl(se.SocketErrorCode.ToString(), ErrorResolutionFlags.Permanent);
                    }
                }

                var we = exception as WebException;
                if(we == null) {
                    Log.To.Sync.V(Tag, "No further information can be gained from this exception, " +
                    "attempting to find other nested exceptions...", exception);
                    if(!String.IsNullOrEmpty(code)) {
                        code = e.GetType().Name;
                    }
                    continue;
                }

                if(we.Status == WebExceptionStatus.ConnectFailure || we.Status == WebExceptionStatus.Timeout ||
                    we.Status == WebExceptionStatus.ConnectionClosed || we.Status == WebExceptionStatus.RequestCanceled ||
                   we.Status == WebExceptionStatus.NameResolutionFailure) {
                    Log.To.Sync.V(Tag, "Rule #3: Exception is WebException and status is ConnectFailure, Timeout, " +
                    "ConnectionClosed, RequestCanceled, or NameResolutionFailure, ruling transient...", we);
                    return new ErrorResolution_Impl(we.Status.ToString(), ErrorResolutionFlags.Transient);
                }

                var statusCode = GetStatusCode(we);
                if(!statusCode.HasValue) {
                    Log.To.Sync.V(Tag, "No further information can be gained from this WebException (missing response?), " +
                    "attempting to find other nested exceptions...", we);
                    code = we.Status.ToString();
                    continue;
                }

                if(IsTransientError(((HttpWebResponse)we.Response).StatusCode)) {
                    Log.To.Sync.V(Tag, "Rule #3: {0} is considered a transient error code, ruling transient...",
                        ((HttpWebResponse)we.Response).StatusCode);
                    return new ErrorResolution_Impl(statusCode.Value.ToString(), ErrorResolutionFlags.Transient);
                }
            }

            Log.To.Sync.V(Tag, "No transient exceptions found, ruling fatal...");
            return new ErrorResolution_Impl(code, ErrorResolutionFlags.Permanent);
        }

        internal static bool IsTransientError(HttpResponseMessage response)
        {
            if(response == null) {
                return false;
            }

            return IsTransientError(response.StatusCode);
        }

        internal static bool IsTransientError(HttpStatusCode status)
        {
            return status == HttpStatusCode.InternalServerError ||
                status == HttpStatusCode.BadGateway ||
                status == HttpStatusCode.ServiceUnavailable ||
                status == HttpStatusCode.GatewayTimeout ||
                status == HttpStatusCode.RequestTimeout;
        }

        private sealed class ErrorResolution_Impl : IErrorResolution
        {
            public ErrorResolution Resolution {
                get; set;
            }

            public ErrorResolutionFlags ResolutionFlags {
                get; set;
            }

            public string Status {
                get;
            }

            public ErrorResolution_Impl(string status, ErrorResolutionFlags flags)
            {
                Status = status;
                ResolutionFlags = flags;
            }
        }
    }
}

