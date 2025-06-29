﻿//
//  PerfTest.cs
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

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Shouldly;
using Test.Util;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public abstract class PerfTest
    {
        private ITestOutputHelper _output;
        public static string ResourceDir;
        private DatabaseConfiguration _dbConfiguration;
        private string _dbName;

        public Database Db { get; private set; }

        #if NET6_0_OR_GREATER && !CBL_NO_VERSION_CHECK && !NET_ANDROID && !__IOS__ && !WINUI
        static PerfTest()
        {
            Couchbase.Lite.Support.NetDesktop.CheckVersion();
        }
        #endif

        protected PerfTest(ITestOutputHelper output)
        {
            _output = output;
            Database.Log.Custom = new XunitLogger(output) { Level = LogLevel.Info };
        }

        protected void SetOptions(DatabaseConfiguration dbConfiguration)
        {
            _dbConfiguration = dbConfiguration;
            _dbName = "perfdb";
            Database.Delete(_dbName, _dbConfiguration.Directory);
        }

        protected string ReadData(string resourceName)
        {
            var path = Path.Combine(ResourceDir, resourceName);
            return File.ReadAllText(path);
        }

        protected void OpenDB()
        {
            _dbName.ShouldNotBeNull("because otherwise we cannot open the database");
            Db.ShouldBeNull("because otherwise we are trying to reopen the database incorrectly");
            Db = new Database(_dbName, _dbConfiguration);
        }

        protected void ReopenDB()
        {
            Db?.Dispose();
            Db = null;
            OpenDB();
        }

        protected void EraseDB()
        {
            Db?.Dispose();
            Db = null;
            Database.Delete(_dbName, _dbConfiguration.Directory);
            OpenDB();
        }

        protected virtual void SetUp()
        {
            Database.Log.Console.Level = LogLevel.None;
            Database.Log.File.Level = LogLevel.None;
        }

        protected abstract void Test();

        protected virtual void TearDown()
        {
            Database.Log.Console.Level = LogLevel.Warning;
            Database.Log.File.Level = LogLevel.Info;
            Db?.Dispose();
            Db = null;
            Database.Log.Custom = null;
        }

        protected void Run()
        {
            WriteLine($"===== {GetType().Name} =====");
            SetUp();
            try {
                Test();
            } finally {
                TearDown();
            }
        }

        protected void WriteLine(string line)
        {
            _output.WriteLine(line);
        }

        protected void Measure(uint count, string unit, Action a)
        {
            var b = new Benchmark(_output);
            const int reps = 10;
            for(int i = 0; i < reps; i++) {
                EraseDB();
                b.Start();
                a();
                var t = b.Stop();
                WriteLine($"{t}");
            }

            b.PrintReport();
            if(count > 1) {
                b.PrintReport(1.0 / count, unit);
            }
        }
    }
}
