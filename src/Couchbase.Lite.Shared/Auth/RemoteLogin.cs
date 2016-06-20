//
//  RemoteLogin.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Auth
{
    // Logs into a server asynchronously using an Authorizer
    internal sealed class RemoteLogin
    {
        private const string Tag = nameof(RemoteLogin);

        private readonly Uri _remoteUrl;
        private readonly RemoteSession _session;
        private TaskCompletionSource<bool> _tcs;
        private static readonly HashSet<RemoteLogin> _activeAttempts = new HashSet<RemoteLogin>();

        public RemoteLogin(Uri remoteUrl, RemoteSession session)
        {
            _remoteUrl = remoteUrl;
            _session = session;
        }

        public Task AttemptLogin()
        {
            var tcs = _tcs;
            if(tcs != null) {
                return _tcs.Task;
            }

            tcs = new TaskCompletionSource<bool>();
            _tcs = tcs;
            _activeAttempts.Add(this);
            _session.Authenticator.RemoteUrl = _remoteUrl;
            CheckSession();
            return tcs.Task;
        }

        private void CheckSession()
        {
            if(_session.Authenticator != null && _session.Authenticator is ISessionCookieAuthorizer) {
                CheckSessionAtPath("/_session");
            } else {
                Login();
            }
        }

        private void CheckSessionAtPath(string sessionPath)
        {
            _session.SendAsyncRequest(HttpMethod.Get, sessionPath, null, (result, e) => {
                if(e != null) {
                    // If not at /db/_session, try CouchDB location /_session
                    var statusCode = Misc.GetStatusCode(e);
                    if(statusCode.HasValue && statusCode.Value == HttpStatusCode.NotFound &&
                    sessionPath == "_session") {
                        CheckSessionAtPath("/_session");
                        return;
                    } else if(statusCode == HttpStatusCode.Unauthorized) {
                        Login();
                    } else {
                        Log.To.Sync.I(Tag, "{0} session check failed: {1}", this, e);
                        OnFinished(e);
                    }
                } else {
                    var userName = result?.AsDictionary<string, object>()?.Get("userCtx")?.AsDictionary<string, object>()?.GetCast<string>("name");
                    if(userName != null) {
                        // Found a login session!
                        Log.To.Sync.I(Tag, "{0}: Active session, logged in as '{1}'", this, new SecureLogString(userName, LogMessageSensitivity.PotentiallyInsecure));
                        OnFinished(null);
                    } else {
                        // No current login session, so continue to regular login:
                        Login();
                    }
                }
            });
        }

        private void Login()
        {
            var loginAuth = _session.Authenticator as ILoginAuthorizer;
            var login = loginAuth?.LoginRequest();
            if(login == null) {
                Log.To.Sync.I(Tag, "{0}: {1} has no login parameters, so skipping login", this, _session.Authenticator);
                OnFinished(null);
                return;
            }

            var method = login[0] as string;
            var loginPath = login[1] as string;
            var loginParameters = login.Count >= 3 ? login[2] : null;
            Log.To.Sync.I(Tag, "{0} logging in with {1} at {2}", this, _session.Authenticator.GetType(), new SecureLogString(loginPath, LogMessageSensitivity.Insecure));

            Action<Exception> finishingBlock = (e) =>
            {
                if(e != null) {
                    Log.To.Sync.W(Tag, "{0} login failed, stopping replication process...", this);
                    OnFinished(e);
                } else {
                    Log.To.Sync.I(Tag, "{0} successfully logged in!", this);
                    OnFinished(null);
                }
            };

            var resultMessage = default(HttpRequestMessage);
            resultMessage = _session.SendAsyncRequest(new HttpMethod(method), loginPath, loginParameters, (result, e) => {
                loginAuth.ProcessLoginResponse(result?.AsDictionary<string, object>(), resultMessage?.Headers, e, (loginAgain, contError) =>
                {
                    if(loginAgain) {
                        Login();
                    } else {
                        finishingBlock(contError);
                    }
                });
            });
        }

        private void OnFinished(Exception e)
        {
            Misc.SafeNull(ref _tcs, tcs =>
            {
                if(e != null) {
                    tcs.SetException(e);
                } else {
                    tcs.SetResult(true);
                }
                _activeAttempts.Remove(this);
            });
        }
    }
}
