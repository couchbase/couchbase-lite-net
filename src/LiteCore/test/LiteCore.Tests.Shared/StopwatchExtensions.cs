// 
//  StopwatchExtensions.cs
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
using System.Diagnostics;
#if !WINDOWS_UWP
using Xunit.Abstractions;
#else 
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace LiteCore.Tests.Util
{
    public static class StopwatchExt
    {
#if !WINDOWS_UWP
        public static void PrintReport(this Stopwatch st, string what, uint count, string item, ITestOutputHelper output)
#else
        public static void PrintReport(this Stopwatch st, string what, uint count, string item, TestContext output)
#endif
        {
            st.Stop();
            var ms = st.Elapsed.TotalMilliseconds;
#if !DEBUG
            output.WriteLine($"{what} took {ms:F3} ms for {count} {item}s ({{0:F3}} us/{item}, or {{1:F0}} {item}s/sec)",
            ms / (double)count * 1000.0, (double)count / ms * 1000.0);
#else
            output.WriteLine($"{what}; {count} {item}s (took {ms:F3} ms, but this is UNOPTIMIZED CODE)");
#endif
        }
    }
}