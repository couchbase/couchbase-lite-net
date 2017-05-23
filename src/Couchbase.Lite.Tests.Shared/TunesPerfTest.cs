//
//  TunesPerfTest.cs
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
#if false
using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using FluentAssertions;
using Newtonsoft.Json;
using Test.Util;

namespace Test
{
    public sealed class TunesPerfTest : PerfTest
    {
        private int _documentCount;
        private IList<IDictionary<string, object>> _tracks;
        private Benchmark _importBench;
        private Benchmark _updatePlayCountBench;
        private Benchmark _queryArtistsBench;
        private Benchmark _indexArtistsBench;
        private Benchmark _queryIndexedArtistsBench;

        public TunesPerfTest(DatabaseConfiguration configuration) : base(configuration)
        {

        }

        protected override void SetUp()
        {
            var jsonData = ReadData("iTunesMusicLibrary.json");
            _tracks = JsonConvert.DeserializeObject<IList<IDictionary<string, object>>>(jsonData);
            _documentCount = _tracks.Count;
        }

        protected override void Test()
        {
            uint numDocs = 0;
            uint numPlayCounts = 0;
            uint numArtists = 0;
            for(int i = 0; i < 10; i++) {
                WriteLine($"Starting iteration {i + 1}...");
                EraseDB();
                numDocs = ImportLibrary();
                ReopenDB();
                //numPlayCounts = UpdatePlayCounts();
                CreateArtistsIndex();
                QueryAllArtists(_queryIndexedArtistsBench);
            }
        }

        private int ImportLibrary()
        {
            _importBench.Start();
            var keysToCopy = new[] { "Name", "Artist", "Album", "Genre", "Year", "Total Time", "Track Number", "Compilation" };
            _documentCount = 0;
            var ok = Db.ActionQueue.DispatchSync(() =>
            {
                return Db.InBatch(() =>
                {
                    foreach(var track in _tracks) {
                        var trackType = track.GetCast<string>("Track Type");
                        if(trackType != "File" && trackType != "Remote") {
                            continue;
                        }

                        var documentID = track.GetCast<string>("Persistent ID");
                        if(documentID == null) {
                            continue;
                        }

                        var props = new Dictionary<string, object>();
                        foreach(var key in keysToCopy) {
                            var value = track.Get(key);
                            if(value != null) {
                                props[key] = value;
                            }
                        }
                        ++_documentCount;
                        var doc = Db[documentID];
                        doc.ActionQueue.DispatchSync(() => 
                        {
                            doc.Properties = props;
                            doc.Save();
                        });
                    }

                    return true;
                });
            });

            _importBench.Stop();
            ok.Should().BeTrue("because otherwise the batch operation failed");
            return _documentCount;
        }

        private uint UpdatePlayCounts()
        {
            _updatePlayCountBench.Start();
            var count = 0;
            var ok = Db.ActionQueue.DispatchSync(() =>
            {
                return Db.InBatch(() =>
                {
                    return true;
                });
            });

            return 0;
        }
    }
}
#endif