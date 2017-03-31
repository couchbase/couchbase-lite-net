//
// MiscTest.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
/*
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Net.Http;

using Couchbase.Lite.Auth;
using Couchbase.Lite.Store;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Couchbase.Lite.Storage.ForestDB;
using System.IO;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Linq;
using System.Collections;
using Couchbase.Lite.Internal;
using System.Net;

namespace Couchbase.Lite
{

    public class MiscTest : LiteTestCase
    {
        const string Tag = "MiscTest";

        public MiscTest(string storageType) : base(storageType) {}

        [Test]
        public void TestExceptionResolution()
        {
            foreach(var continuous in new[] { false, true }) {
                foreach(var hasRetries in new[] { false, true }) {
                    var transientFlags = ErrorResolutionFlags.Transient;
                    if(!hasRetries) {
                        transientFlags |= ErrorResolutionFlags.OutOfRetries;
                    }

                    var se = new SocketException((int)SocketError.ConnectionRefused);
                    var resolution = CheckResolution(se, continuous, hasRetries, ErrorResolutionFlags.Connectivity);

                    se = new SocketException((int)SocketError.ConnectionReset);
                    resolution = CheckResolution(se, continuous, hasRetries, transientFlags);

                    se = new SocketException((int)SocketError.AddressFamilyNotSupported);
                    resolution = CheckResolution(se, continuous, hasRetries, ErrorResolutionFlags.Permanent);

                    var we = new WebException("Foo", WebExceptionStatus.ConnectFailure);
                   

                    resolution = CheckResolution(we, continuous, hasRetries, transientFlags);
                    resolution = CheckResolution(new NullReferenceException(), continuous, hasRetries, ErrorResolutionFlags.Permanent);
                    resolution = CheckResolution(new IOException(), continuous, hasRetries, transientFlags);
                    resolution = CheckResolution(new TimeoutException(), continuous, hasRetries, transientFlags);

                    for(var status = HttpStatusCode.BadRequest; status < HttpStatusCode.RequestTimeout; status++) {
                        CheckResolution(status, continuous, hasRetries, ErrorResolutionFlags.Permanent);
                    }

                    CheckResolution(HttpStatusCode.RequestTimeout, continuous, hasRetries, transientFlags);

                    for(var status = HttpStatusCode.Conflict; status < HttpStatusCode.ExpectationFailed; status++) {
                        CheckResolution(status, continuous, hasRetries, ErrorResolutionFlags.Permanent);
                    }

                    CheckResolution(HttpStatusCode.UpgradeRequired, continuous, hasRetries, ErrorResolutionFlags.Permanent);
                    CheckResolution(HttpStatusCode.BadGateway, continuous, hasRetries, transientFlags);
                    CheckResolution(HttpStatusCode.ServiceUnavailable, continuous, hasRetries, transientFlags);
                    CheckResolution(HttpStatusCode.GatewayTimeout, continuous, hasRetries, transientFlags);
                    CheckResolution(HttpStatusCode.InternalServerError, continuous, hasRetries, transientFlags);
                    CheckResolution(HttpStatusCode.NotImplemented, continuous, hasRetries, ErrorResolutionFlags.Permanent);
                    CheckResolution(HttpStatusCode.HttpVersionNotSupported, continuous, hasRetries, ErrorResolutionFlags.Permanent);

                }
            }
        }

        private static IErrorResolution CheckResolution(Exception e, bool continuous, bool hasRetries, 
                                                        ErrorResolutionFlags inputFlags) 
        {
            var actual = ExceptionResolver.Solve(e, new ExceptionResolverOptions {
                Continuous = continuous,
                HasRetries = hasRetries
            });

            CheckResolution(actual, continuous, hasRetries, inputFlags);
            return actual;
        }

        private static IErrorResolution CheckResolution(HttpStatusCode statusCode, bool continuous, bool hasRetries,
                                                        ErrorResolutionFlags inputFlags)
        {
            var actual = ExceptionResolver.Solve(statusCode, new ExceptionResolverOptions {
                Continuous = continuous,
                HasRetries = hasRetries
            });

            CheckResolution(actual, continuous, hasRetries, inputFlags);
            return actual;
        }

        private static void CheckResolution(IErrorResolution resolution, bool continuous, bool hasRetries,
                                                        ErrorResolutionFlags inputFlags)
        {
            if(inputFlags.HasFlag(ErrorResolutionFlags.Transient)) {
                if(continuous) {
                    if(hasRetries) {
                        Assert.AreEqual(ErrorResolution.BackoffAndRetry, resolution.Resolution);
                    } else {
                        Assert.AreEqual(ErrorResolution.RetryLater, resolution.Resolution);
                    }
                } else {
                    if(hasRetries) {
                        Assert.AreEqual(ErrorResolution.BackoffAndRetry, resolution.Resolution);
                    } else {
                        Assert.AreEqual(ErrorResolution.Stop, resolution.Resolution);
                    }
                }
            } else if(inputFlags == ErrorResolutionFlags.Connectivity) {
                Assert.AreEqual(ErrorResolution.GoOffline, resolution.Resolution);
            } else {
                Assert.AreEqual(ErrorResolution.Stop, resolution.Resolution);
            }

            Assert.AreEqual(inputFlags, resolution.ResolutionFlags);
        }

        [Test]
        public void TestRoundTripDateTimeOffset()
        {
            var expectedTime = new DateTimeOffset (2016, 01, 01, 12, 0, 0, new TimeSpan (-9, 0, 0));
            var expectedTime2 = expectedTime.LocalDateTime;
            var props = new Dictionary<string, object> {
                { "text", "This is text" },
                { "time", expectedTime }
            };
            var json = Manager.GetObjectMapper().WriteValueAsString(props);
            var deserialized = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(json);
            var resultObj = deserialized.Get("time");
            Assert.IsInstanceOf(typeof(DateTime), resultObj);
            Assert.AreEqual(expectedTime2, resultObj);

            ManagerOptions.SerializationSettings = new JsonSerializationSettings { DateTimeHandling = DateTimeHandling.UseDateTimeOffset };
            json = Manager.GetObjectMapper().WriteValueAsString(props);
            deserialized = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(json);
            resultObj = deserialized.Get("time");
            Assert.IsInstanceOf(typeof(DateTimeOffset), resultObj);
            Assert.AreEqual(expectedTime, resultObj);
            Assert.AreEqual(expectedTime.Offset, ((DateTimeOffset)resultObj).Offset);
        }

        [Test]
        public void TestExceptionEnumerable()
        {
            var innerException = new SocketException();
            var nextException = new HttpRequestException("Socket exception", innerException);
            var otherNextException = new CouchbaseLiteException();
            var aggregate = new AggregateException("OMG", nextException, otherNextException);

            CollectionAssert.AreEqual(new Exception[] { innerException, nextException, otherNextException  }, 
                new ExceptionEnumerable(aggregate).ToArray());
        }


        [Test]
        public void TestNetworkAvailabilityChanged()
        {
            var countdown = 2;
            var handler = new NetworkReachabilityManager();
            handler.StatusChanged += (sender, args) => {
                if(args.Status == NetworkReachabilityStatus.Reachable) {
                    countdown -= 1;
                }
            };

            handler.InvokeNetworkChangeEvent(NetworkReachabilityStatus.Unreachable);
            handler.InvokeNetworkChangeEvent(NetworkReachabilityStatus.Reachable);
            handler.InvokeNetworkChangeEvent(NetworkReachabilityStatus.Reachable);
            Assert.AreEqual(0, countdown);
        }

        [Test]
        public void TestServerVersionParsing()
        {
            var oldVersion = new RemoteServerVersion("Couchbase Sync Gateway/1.1.0");
            Assert.IsTrue(oldVersion.IsSyncGateway);
            Assert.AreEqual("Couchbase Sync Gateway", oldVersion.Name);
            Assert.AreEqual("1.1.0", oldVersion.Version);
            Assert.AreEqual(String.Empty, oldVersion.Branch);
            Assert.AreEqual(String.Empty, oldVersion.Commit);

            var nonSGVersion = new RemoteServerVersion("CouchDB/1.6.1");
            Assert.IsFalse(nonSGVersion.IsSyncGateway);
            Assert.AreEqual("CouchDB", nonSGVersion.Name);
            Assert.AreEqual("1.6.1", nonSGVersion.Version);
            Assert.AreEqual(String.Empty, nonSGVersion.Branch);
            Assert.AreEqual(String.Empty, nonSGVersion.Commit);

            var newVersion = new RemoteServerVersion("Couchbase Sync Gateway/1.2 branch/fix/server_header commit/5bfcf79");
            Assert.IsTrue(newVersion.IsSyncGateway);
            Assert.AreEqual("Couchbase Sync Gateway", newVersion.Name);
            Assert.AreEqual("1.2", newVersion.Version);
            Assert.AreEqual("fix/server_header", newVersion.Branch);
            Assert.AreEqual("5bfcf79", newVersion.Commit);
        }

        [Test]
        public void TestFacebookAuthorizer()
        {
            const string token = "pyrzqxgl";
            var site = new Uri("https://example.com/database");
            const string email = "jimbo@example.com";

            // Register and retrieve the sample token:
            var auth = new FacebookAuthorizer(email);
            auth.RemoteUrl = site;
            Assert.IsTrue(FacebookAuthorizer.RegisterAccessToken(token, email, site));
            var gotToken = auth.GetToken();
            Assert.AreEqual(token, gotToken);

            // Try a variant form
            var auth2 = new FacebookAuthorizer(email);
            auth2.RemoteUrl = new Uri("HttpS://example.com:443/some/other/path");
            gotToken = auth2.GetToken();
            Assert.AreEqual(token, gotToken);
            CollectionAssert.AreEqual(new ArrayList { "POST", "_facebook", new Dictionary<string, object> {
                ["access_token"] = token
            } }, auth.LoginRequest());
        }

        [Test]
        public void TestUnquoteString()
        {
            string testString = "attachment; filename=\"attach\"";
            string expected = "attachment; filename=attach";
            string result = Misc.UnquoteString(testString);
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestForestDBViewNameEscaping()
        {
            var invalidName = "#@vuName!!/crazy:��";
            var escapedName = ForestDBViewStore.ViewNameToFilename(invalidName);
            Assert.AreEqual("@23@40vuName@21@21@2fcrazy@3a��.viewindex", escapedName);

            var unescapedName = ForestDBViewStore.FileNameToViewName(escapedName);
            Assert.AreEqual(invalidName, unescapedName);
        }

        [Test]
        public void TestTransientRetryHandler()
        {
            Assert.Inconclusive("Need to implement a scriptable http service, like Square's MockWebServer.");

            // Arrange
            var handler = new TransientErrorRetryHandler(new HttpClientHandler(), new ExponentialBackoffStrategy(2));
            var client = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/foo");

            // Act
            //HttpResponseMessage response = null;

            try {
                client.SendAsync(request).ContinueWith(t => {
                    Assert.Pass();
                });
            } catch (HttpRequestException e) {
                Console.WriteLine("Transient exception not handled {0}", e);
                Assert.Fail("Transient exception not handled");
            }
        }
    }
}
