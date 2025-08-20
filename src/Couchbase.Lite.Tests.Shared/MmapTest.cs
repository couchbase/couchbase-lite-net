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
using Shouldly;
using LiteCore.Interop;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    [ImplementsTestSpec("T0006-MMap-Config", "1.0.0")]
#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.UnsupportedOSPlatform("osx")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("maccatalyst")]
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

#if MACCATALYST
            throw new SkipException("Not supported on Mac Catalyst");
#else
            var config = new DatabaseConfiguration();
            config.MmapEnabled.ShouldBeTrue();
            config.MmapEnabled.ShouldBeTrue("because the default should be true");

            config = new DatabaseConfiguration
            {
                MmapEnabled = false
            };
            config.MmapEnabled.ShouldBeFalse("because C# properties should work...");
#endif
        }

        /// <summary>
        /// Description
        ///     Test that a Database respects the mmap property
        /// Steps
        ///     1. Create a DatabaseConfiguration object and set mmapEnabled to false.
        ///     2. Create a database with the config.
        ///     3. Get the configuration object from the database and check that the mmapEnabled is false.
        ///     4. Use c4db_config2 to confirm that its config contains the kC4DB_MmapDisabled flag
        ///     5. Set the config's mmapEnabled property true
        ///     6. Create a database with the config.
        ///     7. Get the configuration object from the database and verify that mmapEnabled is true
        ///     8. Use c4db_config2 to confirm that its config doesn't contains the kC4DB_MmapDisabled flag
        /// </summary>
        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        public unsafe void TestDatabaseWithConfiguredMMap(bool useMmap)
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Not supported on macOS");

#if MACCATALYST
            throw new SkipException("Not supported on Mac Catalyst");
#else

            var config = new DatabaseConfiguration()
            {
                MmapEnabled = useMmap
            };

            Database.Delete("test", null);
            using var db = new Database("test", config);
            db.Config.MmapEnabled.ShouldBe(useMmap, "because otherwise MmapEnabled was not saved to the db's config");
            var c4db = db.c4db;
            c4db.ShouldNotBeNull();
            var nativeConfig = TestNative.c4db_getConfig2(c4db!.RawDatabase);
            var hasFlag = (nativeConfig->flags & C4DatabaseFlags.MmapDisabled) == C4DatabaseFlags.MmapDisabled;
            hasFlag.ShouldBe(!useMmap, "because the flag in LiteCore should match MmapEnabled (but flipped)");
        }
#endif
    }
}