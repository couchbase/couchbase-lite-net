// 
//  PathsQueryTest.cs
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
#if !CBL_NO_EXTERN_FILES
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
    public unsafe class PathsQueryTest : QueryTestBase
    {
        protected override string JsonPath => "C/tests/data/paths.json";

#if !WINDOWS_UWP
        public PathsQueryTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestDBQueryAnyWithPaths()
        {
            RunTestVariants(() =>
            {
                // For https://github.com/couchbase/couchbase-lite-core/issues/238
                Compile(Json5("['ANY','path',['.paths'],['=',['?path','city'],'San Jose']]"));
                Run().Should().BeEquivalentTo(new[] {"0000001"});

                Compile(Json5("['ANY','path',['.paths'],['=',['?path.city'],'San Jose']]"));
                Run().Should().BeEquivalentTo(new[] { "0000001" });

                Compile(Json5("['ANY','path',['.paths'],['=',['?path','city'],'Palo Alto']]"));
                Run().Should().BeEquivalentTo(new[] { "0000001", "0000002" });
            });
        }
    }
}
#endif