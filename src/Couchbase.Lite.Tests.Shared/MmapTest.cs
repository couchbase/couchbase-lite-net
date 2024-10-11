//
//  MmapTest.cs
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
using LiteCore.Interop;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

#if !__IOS__

namespace Test
{
    [ImplementsTestSpec("T0006-MMap-Config", "1.0.0")]
#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.UnsupportedOSPlatform("osx")]
#endif
    public sealed class MmapTest : TestCase
    {
        public MmapTest(ITestOutputHelper output) : base(output)
        {

        }

        /// <summary>
        /// Test that the mmap default value is as expected and that it's setter and getter work.
        /// Steps
        ///     1. Create a DatabaseConfiguration object.
        ///     2. Get and check the value of the property: it should be true unless the OS is macOS.
        ///     3. Set the mmap property true
        ///     4. Get the config mmap property and verify that it is true
        ///     5. Set the mmap property false
        ///     6. Get the config mmap property and verify that it is false
        /// </summary>
        [SkippableFact]
        public unsafe void TestDefaultMMapConfig()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Not supported on macOS");

            var config = new DatabaseConfiguration();
            config.MmapEnabled.Should().BeTrue();
            config.FullSync.Should().BeFalse("because the default should be true");

            config.FullSync = false;
            config.FullSync.Should().BeFalse("because C# properties should work...");

            config.FullSync = true;
            config.FullSync.Should().BeTrue("because C# properties should work...");
        }

        /// <summary>
        /// Description
        ///     Test that a Database respects the mmap property
        /// Steps
        ///     1. Create a DatabaseConfiguration object and set mmap to false
        ///     2. Create a database with the config
        ///     3. Get the configuration object from the Database and verify that mmap is false
        ///     4. Use c4db_config2(perhaps necessary only for this test) to confirm that its config does not contain the kC4DB_MMap(tenative) flag
        ///     5. Set the config's mmap property true.
        ///     6. Create a database with the config
        ///         - For macOS, the database should be failed to create.
        ///         - For nonMacOS, the database should be successfully created.
        ///     7. Get the configuration object from the Database and verify that mmap is true
        ///     8. Use c4db_config2 to confirm that its config contains the kC4DB_MMap flag
        /// </summary>
        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        public unsafe void TestDatabaseWithConfiguredMMap(bool useMmap)
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Not supported on macOS");

            var config = new DatabaseConfiguration()
            {
                MmapEnabled = useMmap
            };

            Database.Delete("test", null);
            using var db = new Database("test", config);
            var c4db = db.c4db;
            var nativeConfig = TestNative.c4db_getConfig2(c4db);
            var hasFlag = (nativeConfig->flags & C4DatabaseFlags.MmapDisabled) == C4DatabaseFlags.MmapDisabled;
            hasFlag.Should().Be(!useMmap, "because the flag in LiteCore should match MmapEnabled (but flipped)");
        }
    }
}

#endif