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

namespace Couchbase.Lite
{
    public class LogTest : LiteTestCase, ILogger
    {
        private const string TAG = "LogTest";

        private Action<Log.LogLevel, string, string> _logCallback;

        public LogTest(string storageType) : base(storageType)
        {
        }

        [TestFixtureSetUp]
        public void OneTimeSetup()
        {
            Log.SetLogger(this);
        }

        [TestFixtureTearDown]
        public void OneTimeTearDown()
        {
            Log.Level = Log.LogLevel.Normal;
            Log.SetDefaultLogger();
        }

        [TestCase(Log.LogLevel.None, Result=0)]
        [TestCase(Log.LogLevel.Error, Result=1)]
        [TestCase(Log.LogLevel.Warning, Result=1)]
        [TestCase(Log.LogLevel.Info, Result=1)]
        #if DEBUG
        [TestCase(Log.LogLevel.All, Result=5)]
        [TestCase(Log.LogLevel.Debug, Result=1)]
        #else
        [TestCase(Log.LogLevel.All, Result=4)]
        [TestCase(Log.LogLevel.Debug, Result=0)]
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

            Log.V(TAG, "TEST");
            Log.D(TAG, "TEST");
            Log.I(TAG, "TEST");
            Log.W(TAG, "TEST");
            Log.E(TAG, "TEST");

            return count;
        }

        [TestCase(Log.LogLevel.None, Result=0)]
        [TestCase(Log.LogLevel.Error, Result=1)]
        [TestCase(Log.LogLevel.Warning, Result=1)]
        [TestCase(Log.LogLevel.Info, Result=1)]
        #if DEBUG
        [TestCase(Log.LogLevel.All, Result=5)]
        [TestCase(Log.LogLevel.Debug, Result=1)]
        #else
        [TestCase(Log.LogLevel.All, Result=4)]
        [TestCase(Log.LogLevel.Debug, Result=0)]
        #endif
        public int TestOverrideLogLevel(Log.LogLevel inputLevel)
        {
            var count = 0;
            Log.Level = Log.LogLevel.None;
            Log.To.Database.Level = inputLevel;
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
            DoCallback(Log.LogLevel.Info, tag, msg);
        }

        public void I(string tag, string msg, Exception tr)
        {
            DoCallback(Log.LogLevel.Info, tag, msg);
        }

        public void I(string tag, string format, params object[] args)
        {
            DoCallback(Log.LogLevel.Info, tag, String.Format(format, args));
        }

        public void W(string tag, string msg)
        {
            DoCallback(Log.LogLevel.Warning, tag, msg);
        }

        public void W(string tag, Exception tr)
        {
            throw new NotImplementedException();
        }

        public void W(string tag, string msg, Exception tr)
        {
            DoCallback(Log.LogLevel.Warning, tag, msg);
        }

        public void W(string tag, string format, params object[] args)
        {
            DoCallback(Log.LogLevel.Warning, tag, String.Format(format, args));
        }

        public void E(string tag, string msg)
        {
            DoCallback(Log.LogLevel.Error, tag, msg);
        }

        public void E(string tag, string msg, Exception tr)
        {
            DoCallback(Log.LogLevel.Error, tag, msg);
        }

        public void E(string tag, string format, params object[] args)
        {
            DoCallback(Log.LogLevel.Error, tag, String.Format(format, args));
        }

        #endregion
    }
}

