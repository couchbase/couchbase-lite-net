//
//  SQLiteOptionsTest.cs
//
//  Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite;
using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Test
{

    [ImplementsTestSpec("T0003-SQLite-Options", "2.0.0")]
    public class SQLiteOptionsTest : TestCase
    {
        public SQLiteOptionsTest(ITestOutputHelper output) : base(output)
        {

        }

        /// <summary>
        /// 1. TestSQLiteFullSyncConfig
        /// Description
        ///     Test that the FullSync default is as expected and that it's setter and getter work.

        /// Steps
        ///     1. Create a DatabaseConfiguration object.
        ///     2. Get and check the value of the FullSync property: it should be false.
        ///     3. Set the FullSync property true
        ///     4. Get the config FullSync property and verify that it is true
        ///     5. Set the FullSync property false
        ///     6. Get the config FullSync property and verify that it is false
        /// </summary>
        [Fact]
        public unsafe void TestSQLiteFullSyncConfig()
        {
            var config = new DatabaseConfiguration();
            config.FullSync.Should().BeFalse("because the default should be false");

            config.FullSync = true;
            config.FullSync.Should().BeTrue("because C# properties should work...");

            config.FullSync = false;
            config.FullSync.Should().BeFalse("because C# properties should work...");
        }

        /// <summary>
        /// Description
        ///     Test that a Database respects the FullSync property

        /// Steps
        ///     1. Create a DatabaseConfiguration object and set Full Sync false
        ///     2. Create a database with the config
        ///     3. Get the configuration object from the Database and verify that FullSync is false
        ///     4. Use c4db_config2(perhaps necessary only for this test) to confirm that its config does not contain the kC4DB_DiskSyncFull flag
        ///     5. Set the config's FullSync property true
        ///     6. Create a database with the config
        ///     7. Get the configuration object from the Database and verify that FullSync is true
        ///     8. Use c4db_config2 to confirm that its config contains the kC4DB_DiskSyncFull flag
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public unsafe void TestDBWithFullSync(bool useFullSync)
        {
            var config = new DatabaseConfiguration()
            {
                FullSync = useFullSync
            };

            Database.Delete("test", null);
            using var db = new Database("test", config);
            var c4db = db.c4db;
            c4db.Should().NotBeNull("because the database is in use");
            var nativeConfig = TestNative.c4db_getConfig2(c4db!.RawDatabase);
            var hasFlag = (nativeConfig->flags & C4DatabaseFlags.DiskSyncFull) == C4DatabaseFlags.DiskSyncFull;
            hasFlag.Should().Be(useFullSync, "because the flag in LiteCore should match FullSync");
        }
    }

    internal unsafe static partial class TestNative
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4DatabaseConfig2* c4db_getConfig2(C4Database* database);
    }
}