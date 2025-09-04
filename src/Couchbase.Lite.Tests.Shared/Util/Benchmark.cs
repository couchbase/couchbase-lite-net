//
//  Benchmark.cs
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
using System.Diagnostics;
using System.Linq;
using Xunit.Abstractions;

namespace Test.Util;

internal sealed class Benchmark(ITestOutputHelper output)
{
    private readonly Stopwatch _st = new();
    private readonly List<TimeSpan> _times = [];

    public TimeSpan Elapsed => _st.Elapsed;

    public void Start()
    {
        _st.Reset();
        _st.Start();
    }

    public TimeSpan Stop()
    {
        _st.Stop();
        var t = Elapsed;
        _times.Add(t);
        return t;
    }

    public void Sort() => _times.Sort();

    public TimeSpan Median()
    {
        Sort();
        return _times[_times.Count / 2];
    }

    public TimeSpan Average()
    {
        Sort();
        var n = _times.Count;
        var skip = n / 10;
        var total = _times.Skip(skip).Sum(t => t.Ticks);

        return TimeSpan.FromTicks(total / (n - 2 * skip));
    }

    public TimeSpan StdDev()
    {
        var avg = Average().TotalMilliseconds;
        var n = _times.Count;
        var skip = n / 10;
        var total = _times.Skip(skip).Sum(t => Math.Pow(t.TotalMilliseconds - avg, 2.0));

        return TimeSpan.FromMilliseconds(Math.Sqrt(total / (n - 2.0 * skip)));
    }

    public Tuple<TimeSpan, TimeSpan> Range()
    {
        Sort();
        return Tuple.Create(_times.First(), _times.Last());
    }

    public void Reset() => _times.Clear();

    public void PrintReport(double scale = 1.0, string? items = null)
    {
        var r = Range();
        var timeScales = new[] { "sec", "ms", "us", "ns" };
        var avg = Average().TotalSeconds;
        var scaleName = default(string);
        for(uint i = 0; i < timeScales.Length; i++) {
            scaleName = timeScales[i];
            if(avg * scale > 1.0) {
                break;
            }

            scale *= 1000;
        }

        if(items != null) {
            scaleName = $"{scaleName}/{items}";
        }

        var line = $"Range: {r.Item1.TotalSeconds * scale:F3} ... {r.Item2.TotalSeconds * scale:F3} {scaleName}, median: {Median().TotalSeconds * scale:F3}, std dev: {StdDev().TotalSeconds * scale:G3}";
        output.WriteLine(line);
    }
}