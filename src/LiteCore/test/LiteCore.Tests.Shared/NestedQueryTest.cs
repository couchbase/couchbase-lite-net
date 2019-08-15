// 
//  NestedQueryTest.cs
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
    public unsafe class NestedQueryTest : QueryTestBase
    {
        protected override string JsonPath => "C/tests/data/nested.json";

#if !WINDOWS_UWP
        public NestedQueryTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestDBQueryAnyNested()
        {
            RunTestVariants(() => {
                Compile(Json5("['ANY', 'Shape', ['.', 'shapes'], ['=', ['?', 'Shape', 'color'], 'red']]"));
                Run().Should().Equal(new[] { "0000001", "0000003" }, "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestQueryParserErrorMessages()
        {
            RunTestVariants(() =>
            {
                C4Error err;
                _query = Native.c4query_new(Db, "[\"=\"]", &err);
                ((long) _query).Should().Be(0, "because the query string was invalid");
                err.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                err.code.Should().Be((int) C4ErrorCode.InvalidQuery);
                var msg = Native.c4error_getMessage(err);
                msg.Should().Be("Wrong number of arguments to =");
            });
        }
    }
}
#endif