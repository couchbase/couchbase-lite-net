// 
//  CollatedQueryTest.cs
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
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using LiteCore.Interop;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace LiteCore.Tests
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public unsafe class CollatedQueryTest : QueryTestBase
    {

        protected override string JsonPath => "C/tests/data/iTunesMusicLibrary.json";

#if !WINDOWS_UWP
        public CollatedQueryTest(ITestOutputHelper output) : base(output)
        {

        }
#endif
        [Fact]
        public void TestDBQueryCollated()
        {
            RunTestVariants(() =>
            {
                CompileSelect(Json5("{WHAT: [ ['.Name'] ], " +
                    "WHERE: ['COLLATE', {'unicode': true, 'case': false, 'diacritic': false}," +
                                        "['=', ['.Artist'], 'Benoît Pioulard']]," +
                    "ORDER_BY: [ [ 'COLLATE',  {'unicode': true, 'case': false, 'diacritic': false}," +
                                                 "['.Name']] ]}"));
                var tracks = Run();
                tracks.Count.Should().Be(2);
            });
        }

        [Fact]
        public void TestDBQueryAggregateCollated()
        {
            RunTestVariants(() =>
            {
                CompileSelect(Json5("{WHAT: [ ['COLLATE', {'unicode': true, 'case': false, 'diacritic': false}, " +
                                    "['.Artist']] ], " +
                                    "DISTINCT: true, " +
                                    "ORDER_BY: [ [ 'COLLATE', {'unicode': true, 'case': false, 'diacritic': false}, " +
                                    "['.Artist']] ]}"));

                var artists = Run();
                artists.Count.Should().Be(2097, "because that is the number of distinct artists in the file");

                // Benoît Pioulard appears twice in the database, once miscapitalized as BenoÎt Pioulard.
                // Check that these got coalesced by the DISTINCT operator:
                artists[214].Should().Be("Benny Goodman");
                artists[215].Should().Be("Benoît Pioulard");
                artists[216].Should().Be("Bernhard Weiss");

                // Make sure "Zoë Keating" sorts correctly:
                artists[2082].Should().Be("ZENИTH (feat. saåad)");
                artists[2083].Should().Be("Zoë Keating");
                artists[2084].Should().Be("Zola Jesus");
            });
        }
    }
}
