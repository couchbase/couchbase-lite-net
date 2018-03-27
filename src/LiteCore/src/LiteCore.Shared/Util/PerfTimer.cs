//
//  PerfTimer.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using JetBrains.Annotations;

namespace LiteCore.Util
{
    /// <summary>
    /// Simple utility for timing arbitrary logic and listing hotspots
    /// </summary>
    public static class PerfTimer
    {
        #region Constants

        [NotNull]
        private static readonly ConcurrentDictionary<string, LinkedList<PerfEvent>> _EventMap = 
            new ConcurrentDictionary<string, LinkedList<PerfEvent>>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts an overall run (memory measurement)
        /// </summary>
        public static void StartRun()
        {
            GC.GetTotalMemory(false);
        }

        /// <summary>
        /// Starts a new performance event
        /// </summary>
        /// <param name="name">The name for the event</param>
        [Conditional("PERF_TESTING")]
        public static void StartEvent(string name)
        {
            var evt = new PerfEvent(name);
            var list = _EventMap.GetOrAdd(name, new LinkedList<PerfEvent>());
            list.AddLast(evt);
            evt.StartTiming();
        }

        /// <summary>
        /// Stops the performance event with the given name and records its time
        /// </summary>
        /// <param name="name">The name of the event to stop</param>
        [Conditional("PERF_TESTING")]
        public static void StopEvent(string name)
        {
            var list = _EventMap[name];
            var evt = list.Last.Value;
            evt.StopTiming();
        }

        /// <summary>
        /// Writes the statistics about all of the recorded performance events
        /// using the given handler
        /// </summary>
        /// <param name="handler">The handler used to write the performance events</param>
        [Conditional("PERF_TESTING")]
        public static void WriteStats(Action<string> handler)
        {
            if(handler == null) {
                return;
            }

            var summaryDict = new SortedDictionary<double, string>();
            foreach(var pair in _EventMap) {
                var average = pair.Value.Average(x => x.Elapsed.TotalMilliseconds);
                summaryDict[average] = pair.Key;
            }

            var unitMap = new List<string> { "ms", "μs", "ns" };
            foreach(var pair in summaryDict.Reverse()) {
                var time = pair.Key;
                var index = 0;
                while(index < 3 && time < 1) {
                    index++;
                    time *= 1000.0;
                }
                handler($"{pair.Value} => Average {time}{unitMap[index]}");
            }
        }

        #endregion
    }

    internal sealed class PerfEvent
    {
        #region Variables

        [NotNull]private readonly Stopwatch _sw = new Stopwatch();
        private long _startMemory;
        private long _endMemory;
        [NotNull]private readonly int[] _startCollections= { 0, 0, 0 };
        [NotNull]private readonly int[] _endCollections = { 0, 0, 0 };

        #endregion

        #region Properties

        internal TimeSpan Elapsed => _sw.Elapsed;

        internal int[] Collections => new[]
        {
            _endCollections[0] - _startCollections[0],
            _endCollections[1] - _startCollections[1],
            _endCollections[2] - _startCollections[2]
        };

        internal long GcHeapDelta => _endMemory - _startMemory;

        internal string Name { get; }

        #endregion

        #region Constructors

        internal PerfEvent(string name)
        {
            Name = name;
        }

        #endregion

        #region Internal Methods

        internal void StartTiming()
        {
            _sw.Start();
            _startMemory = GC.GetTotalMemory(false);
            _startCollections[0] = GC.CollectionCount(0);
            _startCollections[1] = GC.CollectionCount(1);
            _startCollections[2] = GC.CollectionCount(2);
        }

        internal void StopTiming()
        {
            _sw.Stop();
            _endMemory = GC.GetTotalMemory(false);
            _endCollections[0] = GC.CollectionCount(0);
            _endCollections[1] = GC.CollectionCount(1);
            _endCollections[2] = GC.CollectionCount(2);
        }

        #endregion
    }
}
