//
// LogTest.cs
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
using Couchbase.Lite.Util;
using NUnit.Framework;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using System.Linq;
using Couchbase.Lite.Revisions;

namespace Couchbase.Lite
{
    public class LogTest : LiteTestCase, ILogger
    {
        private const string TAG = "LogTest";

        private Action<Log.LogLevel, string, string> _logCallback;

        public LogTest(string storageType) : base(storageType)
        {
        }

        protected override void SetUp()
        {
            base.SetUp();
            Log.SetLogger(this);
        }

        protected override void TearDown()
        {
            Log.Level = Log.LogLevel.Base;
            Log.Disabled = false;
            Log.SetDefaultLogger();

            base.TearDown();
        }

        [Test]
        public void TestLogDisabled()
        {
            try {
                var count = TestLogLevel(Log.LogLevel.Debug);
                Assert.AreNotEqual(0, count);

                Log.Disabled = true;
                count = TestLogLevel(Log.LogLevel.Debug);
                Assert.AreEqual(0, count);
            } finally {
                Log.Disabled = false;
            }
        }

        [TestCase(Log.LogLevel.None, Result=0)]
        [TestCase(Log.LogLevel.Base, Result=3)]
        #if DEBUG
        [TestCase(Log.LogLevel.Debug, Result=5)]
        #else
        [TestCase(Log.LogLevel.Debug, Result=4)]
        #endif
        public int TestLogLevel(Log.LogLevel inputLevel)
        {
            var count = 0;
            Log.Level = inputLevel;
            _logCallback = (level, tag, msg) =>
            {
                Console.WriteLine("Received {0} message from {1}", level, tag);
                if(tag == TAG) {
                    count++;
                }

                Assert.IsTrue(Log.Level.HasFlag(level));
            };

            Log.To.NoDomain.V(TAG, "TEST");
            Log.To.NoDomain.D(TAG, "TEST");
            Log.To.NoDomain.I(TAG, "TEST");
            Log.To.NoDomain.W(TAG, "TEST");
            Log.To.NoDomain.E(TAG, "TEST");

            return count;
        }

        [TestCase(Log.LogLevel.None, Result=0)]
        [TestCase(Log.LogLevel.Base, Result=3)]
        #if DEBUG
        [TestCase(Log.LogLevel.Debug, Result=5)]
        #else
        [TestCase(Log.LogLevel.Debug, Result=4)]
        #endif
        public int TestOverrideLogLevel(Log.LogLevel inputLevel)
        {
            var count = 0;
            Log.Level = Log.LogLevel.None;
            Log.Domains.Database.Level = inputLevel;
            _logCallback = (level, tag, msg) =>
            {
                Console.WriteLine("Received {0} message from {1}", level, tag);
                if(tag == String.Format("DATABASE ({0})", TAG)) {
                    count++;
                }

                Assert.IsTrue(inputLevel.HasFlag(level));
            };

            Log.To.Database.V(TAG, "TEST");
            Log.To.Database.D(TAG, "TEST");
            Log.To.Database.I(TAG, "TEST");
            Log.To.Database.W(TAG, "TEST");
            Log.To.Database.E(TAG, "TEST");

            return count;
        }

        [Test]
        public void TestSecureLogging()
        {
            var auth = new BasicAuthenticator("jim", "borden");
            var rev = new RevisionInternal("sensitive", "1-abcdef".AsRevID(), false);
            var lastMessage = default(string);
            _logCallback = (level, tag, msg) =>
            {
                lastMessage = msg;
            };

            Log.ScrubSensitivity = LogScrubSensitivity.NoInsecure;
            Log.To.NoDomain.I(TAG, "{0}", auth);
            Assert.AreEqual("[BasicAuthenticator (<redacted>:<redacted>)]", lastMessage);

            Log.To.NoDomain.I(TAG, "{0}", rev);
            Assert.AreEqual("{<redacted> #1-abcdef}", lastMessage);

            Log.ScrubSensitivity = LogScrubSensitivity.PotentiallyInsecureOK;
            Log.To.NoDomain.I(TAG, "{0}", auth);
            Assert.AreEqual("[BasicAuthenticator (jim:<redacted>)]", lastMessage);

            Log.To.NoDomain.I(TAG, "{0}", rev);
            Assert.AreEqual("{sensitive #1-abcdef}", lastMessage);

            Log.ScrubSensitivity = LogScrubSensitivity.AllOK;
            Log.To.NoDomain.I(TAG, "{0}", auth);
            Assert.AreEqual("[BasicAuthenticator (jim:borden)]", lastMessage);
        }

        [Test]
        public void TestLogGrouping()
        {
            Log.Domains.Group(Log.Domains.Database, Log.Domains.Query).Level = Log.LogLevel.Verbose;
            Assert.AreEqual(Log.LogLevel.Verbose, Log.Domains.Database.Level);
            Assert.AreEqual(Log.LogLevel.Verbose, Log.Domains.Query.Level);

            Log.Domains.All.Level = Log.LogLevel.Base;
            Assert.AreEqual(Log.LogLevel.Base, Log.Domains.Database.Level);
            Assert.AreEqual(Log.LogLevel.Base, Log.Domains.Query.Level);

            Log.Domains.Except(Log.Domains.Sync).Level = Log.LogLevel.Verbose;
            foreach (var logger in Log.Domains.All) {
                var castLogger = (DomainLogger)logger;
                if (castLogger.Domain == "SYNC") {
                    Assert.AreEqual(Log.LogLevel.Base, logger.Level);
                } else {
                    Assert.AreEqual(Log.LogLevel.Verbose, logger.Level);
                }
            }

            Assert.DoesNotThrow(() => Log.Domains.Group().Level = Log.LogLevel.Base);
            Assert.DoesNotThrow(() => Log.Domains.Except().Level = Log.LogLevel.Base);
        }

        private void DoCallback(Log.LogLevel level, string tag, string msg)
        {
            if (_logCallback != null) {
                _logCallback(level, tag, msg);
            }
        }

        #region ILogger

        public void V(string tag, string msg)
        {
            DoCallback(Log.LogLevel.Verbose, tag, msg);
        }

        public void V(string tag, string msg, Exception tr)
        {
            DoCallback(Log.LogLevel.Verbose, tag, msg);
        }

        public void V(string tag, string format, params object[] args)
        {
            DoCallback(Log.LogLevel.Verbose, tag, String.Format(format, args));
        }

        public void D(string tag, string msg)
        {
            DoCallback(Log.LogLevel.Debug, tag, msg);
        }

        public void D(string tag, string msg, Exception tr)
        {
            DoCallback(Log.LogLevel.Debug, tag, msg);
        }

        public void D(string tag, string format, params object[] args)
        {
            DoCallback(Log.LogLevel.Debug, tag, String.Format(format, args));
        }

        public void I(string tag, string msg)
        {
            DoCallback(Log.LogLevel.Base, tag, msg);
        }

        public void I(string tag, string msg, Exception tr)
        {
            DoCallback(Log.LogLevel.Base, tag, msg);
        }

        public void I(string tag, string format, params object[] args)
        {
            DoCallback(Log.LogLevel.Base, tag, String.Format(format, args));
        }

        public void W(string tag, string msg)
        {
            DoCallback(Log.LogLevel.Base, tag, msg);
        }

        public void W(string tag, Exception tr)
        {
            throw new NotImplementedException();
        }

        public void W(string tag, string msg, Exception tr)
        {
            DoCallback(Log.LogLevel.Base, tag, msg);
        }

        public void W(string tag, string format, params object[] args)
        {
            DoCallback(Log.LogLevel.Base, tag, String.Format(format, args));
        }

        public void E(string tag, string msg)
        {
            DoCallback(Log.LogLevel.Base, tag, msg);
        }

        public void E(string tag, string msg, Exception tr)
        {
            DoCallback(Log.LogLevel.Base, tag, msg);
        }

        public void E(string tag, string format, params object[] args)
        {
            DoCallback(Log.LogLevel.Base, tag, String.Format(format, args));
        }

        #endregion
    }
}

