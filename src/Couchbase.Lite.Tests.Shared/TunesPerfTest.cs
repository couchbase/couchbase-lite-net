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
#if PERFORMANCE
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;
using FluentAssertions;
using Newtonsoft.Json;
using Test.Util;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
    public sealed class TunesPerfTest : PerfTest
    {
        private int _documentCount;
        private IList<IDictionary<string, object>> _tracks;
        private IList<string> _artists;
        private Benchmark _importBench;
        private Benchmark _updatePlayCountBench;
        private Benchmark _updateArtistsBench;
        private Benchmark _indexArtistsBench;
        private Benchmark _queryArtistsBench;
        private Benchmark _queryIndexedArtistsBench;
        private Benchmark _queryAlbumsBench;
        private Benchmark _queryIndexedAlbumsBench;
        private Benchmark _indexFTSBench;
        private Benchmark _queryFTSBench;

#if !WINDOWS_UWP
        public TunesPerfTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestPerformance()
        {
            var configuration = new DatabaseConfiguration
            {
                Directory = Path.Combine(Path.GetTempPath().Replace("cache", "files"), "CouchbaseLite")
            };

            SetOptions(configuration);
            Run();
        }

    protected override void SetUp()
        {
            var jsonData = ReadData("iTunesMusicLibrary.json");
            _tracks = JsonConvert.DeserializeObject<IList<IDictionary<string, object>>>(jsonData);
            _documentCount = _tracks.Count;
        }

        protected override void Test()
        {
            var numDocs = 0;
            var numUpdates = 0;
            var numArtists = 0;
            var numAlbums = 0;
            var numFTS = 0;

            for(int i = 0; i < 10; i++) {
                WriteLine($"Starting iteration {i + 1}...");
                EraseDB();
                numDocs = ImportLibrary();
                ReopenDB();
                numUpdates = UpdateArtistNames();
                numArtists = QueryAllArtists(_queryArtistsBench);
                numAlbums = QueryAlbums(_queryAlbumsBench);
                CreateArtistsIndex();

                var numArtists2 = QueryAllArtists(_queryIndexedArtistsBench);
                numArtists2.Should().Be(numArtists);
                var numAlbums2 = QueryAlbums(_queryIndexedAlbumsBench);
                numAlbums2.Should().Be(numAlbums);
                numFTS = FullTextSearch();
            }
        }

        private int ImportLibrary()
        {
            _importBench.Start();
            _documentCount = 0;
            Db.InBatch(() =>
            {
                foreach (var track in _tracks) {
                    var trackType = track.GetCast<string>("Track Type");
                    if (trackType != "File" && trackType != "Remote") {
                        continue;
                    }

                    var documentID = track.GetCast<string>("Persistent ID");
                    if (documentID == null) {
                        continue;
                    }

                    ++_documentCount;
                    using (var doc = new Document(documentID, track)) {
                        Db.Save(doc);
                    }
                }
            });

            _importBench.Stop();
            return _documentCount;
        }

        private int UpdateArtistNames()
        {
            _updateArtistsBench.Start();
            var count = 0;
            Db.InBatch(() =>
            {
                using (var q = Query.Select(SelectResult.Expression(Expression.Meta().ID),
                        SelectResult.Expression(Expression.Property("Artist")))
                    .From(DataSource.Database(Db))) {
                    using (var results = q.Execute()) {
                        foreach (var result in results) {
                            var artist = result.GetString(1);
                            if (artist.StartsWith("The ")) {
                                using (var doc = Db.GetDocument(result.GetString(0))) {
                                    doc.Set("Artist", artist.Substring(4));
                                    Db.Save(doc);
                                    count++;
                                }
                            }
                        }
                    }
                }
            });

            _updateArtistsBench.Stop();
            return count;
        }

        private int QueryAllArtists(Benchmark bench)
        {
            using (var q = Query.Select(SelectResult.Expression(Expression.Property("Artist")))
                .From(DataSource.Database(Db))
                .Where(Expression.Property("Artist").NotNullOrMissing()
                    .And(Expression.Property("Compilation").IsNullOrMissing()))
                .GroupBy(Expression.Property("Artist"))
                .OrderBy(Ordering.Property("Artist"))) {
                bench.Start();
                _artists = CollectQueryResults(q);
                bench.Stop();
                _artists.Count.Should().Be(1111);
                return _artists.Count;
            }
        }

        private int QueryAlbums(Benchmark bench)
        {
            var albumCount = 0;
            using (var q = Query.Select(SelectResult.Expression(Expression.Property("Album")))
                .From(DataSource.Database(Db))
                .Where(Expression.Property("Artist").EqualTo(Expression.Parameter("ARTIST")
                    .And(Expression.Property("Compilation").IsNullOrMissing())))
                .GroupBy(Expression.Property("Album"))
                .OrderBy(Ordering.Property("Album"))) {
                bench.Start();
               
                foreach (var artist in _artists) {
                    q.Parameters.Set("ARTIST", artist);
                    var albums = CollectQueryResults(q);
                    albumCount += albums.Count;
                }
            }

            bench.Stop();
            albumCount.Should().Be(1886);
            return albumCount;
        }

        private void CreateArtistsIndex()
        {
            _indexArtistsBench.Start();
            var collation = Collation.Unicode().IgnoreCase(true).IgnoreAccents(true);
            var artist = Expression.Property("Artist").Collate(collation);
            var comp = Expression.Property("Compilation");
            var index = Index.ValueIndex().On(ValueIndexItem.Expression(artist), ValueIndexItem.Expression(comp));
            Db.CreateIndex("byArtist", index);
            _indexArtistsBench.Stop();
        }

        private int FullTextSearch()
        {
            _indexFTSBench.Start();
            var nameExpr = Expression.Property("Name");
            var index = Index.FTSIndex().On(FTSIndexItem.Expression(nameExpr));
            Db.CreateIndex("nameFTS", index);
            _indexFTSBench.Stop();

            var ARTIST = Expression.Property("Artist");
            var ALBUM = Expression.Property("Album");
            var NAME = Expression.Property("Name");
            var results = new List<string>();
            using (var q = Query.Select(SelectResult.Expression(ARTIST), SelectResult.Expression(ALBUM),
                    SelectResult.Expression(NAME))
                .From(DataSource.Database(Db))
                .Where(NAME.Match("'Rock'"))
                .OrderBy(Ordering.Property("Artist"), Ordering.Property("Album"))) {
                _queryFTSBench.Start();
                using (var rows = q.Execute()) {
                    foreach (var row in rows) {
                        results.Add(row.GetString(2));
                    }
                }
            }

            _queryFTSBench.Stop();
            results.Count.Should().Be(30);
            return results.Count;
        }

        private IList<string> CollectQueryResults(IQuery query)
        {
            var results = new List<string>();
            using (var rows = query.Execute()) {
                foreach (var row in rows) {
                    results.Add(row.GetString(0));
                }
            }

            return results;
        }
    }
}
#endif