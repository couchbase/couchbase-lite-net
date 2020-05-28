//
//  P2PTest.cs
//
//  Copyright (c) 2020 Couchbase, Inc All rights reserved.
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
using System.Text;
using Xunit.Abstractions;

using Test.Util;
#if COUCHBASE_ENTERPRISE
using Couchbase.Lite.P2P;
#endif

#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
using Couchbase.Lite;
using System.Threading;
using FluentAssertions;
using LiteCore.Interop;
using Couchbase.Lite.Sync;
using System.Linq;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class P2PTest : TestCase
    {
        const ushort WSPort = 4984;
        const ushort WSSPort = 4985;

        private static int Counter;
        private Database _otherDB;

        URLEndpointListener _listener;
        URLEndpointListenerConfiguration _config;
#if !WINDOWS_UWP
        public P2PTest(ITestOutputHelper output) : base(output)
#else
        public P2PTest()
#endif
        {
            ReopenDB();
            var nextCounter = Interlocked.Increment(ref Counter);
            Database.Delete($"otherdb{nextCounter}", Directory);
            _otherDB = OpenDB($"otherdb{nextCounter}");
        }

#if COUCHBASE_ENTERPRISE

        [Fact]
        public void TestPort()
        {
            int exCnt = 0;
            //init a listener
            _config = new URLEndpointListenerConfiguration(_otherDB);
            _config.Port = WSPort;
            _config.DisableTLS = true;

            _listener = new URLEndpointListener(_config);
            _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener has not yet started.");

            try {
                //start the listener
                _listener.Start();
            } catch {
                exCnt++;
            } finally {
                exCnt.Should().Be(0, "Because listener start should work without exception thrown.");
                _listener.Port.Should().Be(WSPort);
                //stop the listener
                _listener.Stop();
                _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener is stopped.");
            }
        }

        [Fact]
        public void TestEmptyPort()
        {
            int exCnt = 0;
            //init a listener
            _config = new URLEndpointListenerConfiguration(_otherDB);
            _config.Port = 0;
            _config.DisableTLS = true;

            _listener = new URLEndpointListener(_config);
            _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener has not yet started.");

            try {
                //start the listener
                _listener.Start();
            } catch {
                exCnt++;
            } finally {
                exCnt.Should().Be(0, "Because listener start should work without exception thrown.");
                _listener.Port.Should().NotBe(0);
                //stop the listener
                _listener.Stop();
                _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener is stopped.");
            }

        }

        [Fact]
        public void TestBusyPort()
        {
            CouchbasePosixException expectedException = null;
            var listener = ListenerWithTLS(false, null);
            var config = new URLEndpointListenerConfiguration(_otherDB);
            config.Port = listener.Config.Port;
            config.DisableTLS = true;
            var listener1 = new URLEndpointListener(config);

            try {
                listener1.Start();
            } catch (CouchbasePosixException ex) {
                expectedException = ex;
            } finally {
                listener.Stop();
                listener1.Stop();
            }

            expectedException.Domain.Should().Be(CouchbaseLiteErrorType.POSIX);
            expectedException.Error.Should().Be(PosixBase.GetCode(nameof(PosixWindows.EADDRINUSE)));
        }

        [Fact]
        public void TestUrls()
        {
            int exCnt = 0;
            var config = new URLEndpointListenerConfiguration(_otherDB);
            var listener = new URLEndpointListener(config);
            listener.Urls.Count.Should().Be(0);

            try {
                listener.Start();
            } catch {
                exCnt++;
            } finally {
                exCnt.Should().Be(0, "Because listener start should work without exception thrown.");
                listener.Urls.Count.Should().NotBe(0);
                listener.Stop();
                listener.Urls.Count.Should().Be(0);
            }
        }

        [Fact]
        public void TestStatus()
        {
            int exCnt = 0;
            HashSet<ulong> maxConnectionCount = new HashSet<ulong>(),
                maxActiveCount = new HashSet<ulong>();

            _config = new URLEndpointListenerConfiguration(_otherDB);
            _config.Port = WSPort;
            _config.DisableTLS = true;

            //init a listener
            _listener = new URLEndpointListener(_config);
            _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because the listener has not yet started.");
            _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because the listener has not yet started.");

            try {
                //start the listener
                _listener.Start();
            } catch {
                exCnt++;
            } finally {
                exCnt.Should().Be(0, "Because listener start should work without exception thrown.");
                _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because no client connection has been established.");
                _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because no client connection has been established.");

                using (var doc1 = new MutableDocument("doc1"))
                using (var doc2 = new MutableDocument("doc2")) {
                    doc1.SetString("name", "Sam");
                    Db.Save(doc1);
                    doc2.SetString("name", "Mary");
                    _otherDB.Save(doc2);
                }

                var targetEndpoint = new URLEndpoint(new Uri($"{_listener.Urls[0]}".Replace("http", "ws")));
                var config = new ReplicatorConfiguration(Db, targetEndpoint);
                using (var repl = new Replicator(config)) {
                    var waitAssert = new WaitAssert();
                    var token = repl.AddChangeListener((sender, args) =>
                    {
                        waitAssert.RunConditionalAssert(() =>
                        {
                            maxConnectionCount.Add(_listener.Status.ConnectionCount);
                            maxActiveCount.Add(_listener.Status.ActiveConnectionCount);

                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    repl.Start();
                    try {
                        waitAssert.WaitForResult(TimeSpan.FromSeconds(100));
                    } finally {
                        repl.RemoveChangeListener(token);
                    }
                }

                maxConnectionCount.Max().Should().Be(1);
                maxActiveCount.Max().Should().Be(1);

                //stop the listener
                _listener.Stop();
                _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because the connection is stopped.");
                _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because the connection is stopped.");
            }
        }

        private URLEndpointListener ListenerWithTLS(bool tls, ListenerAuthenticator auth)
        {
            int exCnt = 0;
            var config = new URLEndpointListenerConfiguration(_otherDB);
            config.Port = tls ? WSSPort : WSPort;
            config.DisableTLS = !tls;
            config.Authenticator = auth;

            var listener = new URLEndpointListener(config);

            try {
                //start the listener
                listener.Start();
            } catch {
                exCnt++;
            }

            if (exCnt == 0)
                return listener;
            else
                return null;
        }

#endif

        protected override void Dispose(bool disposing)
        {
            Exception ex = null;

            base.Dispose(disposing);
            var name = _otherDB?.Name;
            _otherDB?.Dispose();
            _otherDB = null;

            var success = Try.Condition(() =>
            {
                try {
                    if (name != null) {
                        Database.Delete(name, Directory);
                    }
                } catch (Exception e) {
                    ex = e;
                    return false;
                }

                return true;
            }).Times(5).Delay(TimeSpan.FromSeconds(1)).WriteProgress(WriteLine).Go();

            if (!success) {
                throw ex;
            }
        }

    }
}

