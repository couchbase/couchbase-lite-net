// 
//  DatabaseTracker.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
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
#if true
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Couchbase.Lite.Support
{
    internal static class DatabaseTracker
    {
        #region Constants

        private static readonly Dictionary<string, List<string>> Calls = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, int> Count = new Dictionary<string, int>();

        #endregion

        #region Public Methods

        public static void OpenDatabase(string path)
        {
            if (!Calls.ContainsKey(path)) {
                Calls[path] = new List<string>();
                Count[path] = 0;
            }

            var trace = Environment.StackTrace.Replace("\r", "").Split('\n');
            var linesToUse = trace.Skip(4).TakeWhile(x => x.Contains("couchbase"));
            Calls[path].Add(String.Join(Environment.NewLine, linesToUse));
            Count[path]++;
        }

        public static void CloseDatabase(string path)
        {
            if (!Calls.ContainsKey(path)) {
                Calls[path] = new List<string>();
                Count[path] = 1;
            }

            var trace = Environment.StackTrace.Replace("\r", "").Split('\n');
            var linesToUse = trace.Skip(4).TakeWhile(x => x.Contains("couchbase"));
            Calls[path].Add(String.Join(Environment.NewLine, linesToUse));
            Count[path]--;
        }

        public static void Report(string path, TextWriter writer = null)
        {
            var actualWriter = writer ?? Console.Error;
            if (!Calls.ContainsKey(path)) {
                actualWriter.WriteLine($"No report found for {path}");
            }

            actualWriter.WriteLine($"Report for {path} (open count {Count[path]}):");
            foreach (var call in Calls[path]) {
                actualWriter.WriteLine(call);
                actualWriter.WriteLine();
            }
        }

        public static void Reset(string path)
        {
            if (Calls.ContainsKey(path)) {
                Calls[path].Clear();
                Count[path] = 0;
            }
        }

        #endregion
    }
}
#endif