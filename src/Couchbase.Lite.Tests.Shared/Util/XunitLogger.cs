//
//  XunitLogger.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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

#if !WINDOWS_UWP
using Couchbase.Lite.Logging;
using Xunit.Abstractions;

namespace Test.Util
{
    internal sealed class XunitLogger : DefaultLogger
    {
        private readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output) : base(false)
        {
            _output = output;
        }

        protected override void PerformWrite(string final)
        {
            _output.WriteLine(final);
        }
    }
}
#else
using Couchbase.Lite.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.Util
{
    internal sealed class MSTestLogger : DefaultLogger
    {
        private readonly TestContext _output;

        public MSTestLogger(TestContext output) : base(false)
        {
            _output = output;
        }

        protected override void PerformWrite(string final)
        {
            _output.WriteLine(final);
        }
    }
}
#endif