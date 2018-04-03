//
//  TunesPerfTest.cs
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
using System.Globalization;
using System.IO;
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Logging;
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
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
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
            _importBench = new Benchmark(output);
            _updatePlayCountBench = new Benchmark(output);
            _updateArtistsBench = new Benchmark(output);
            _indexArtistsBench = new Benchmark(output);
            _queryArtistsBench = new Benchmark(output);
            _queryIndexedArtistsBench = new Benchmark(output);
            _queryAlbumsBench = new Benchmark(output);
            _queryIndexedAlbumsBench = new Benchmark(output);
            _indexFTSBench = new Benchmark(output);
            _queryFTSBench = new Benchmark(output);
        }
#else
        public override Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext
        {
            get => base.TestContext;
            set {
                base.TestContext = value;
                _importBench = new Benchmark(value);
                _updatePlayCountBench = new Benchmark(value);
                _updateArtistsBench = new Benchmark(value);
                _indexArtistsBench = new Benchmark(value);
                _queryArtistsBench = new Benchmark(value);
                _queryIndexedArtistsBench = new Benchmark(value);
                _queryAlbumsBench = new Benchmark(value);
                _queryIndexedAlbumsBench = new Benchmark(value);
                _indexFTSBench = new Benchmark(value);
                _queryFTSBench = new Benchmark(value);
            }
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
            _tracks = new List<IDictionary<string, object>>();
            var settings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };

            TestCase.ReadFileByLines("C/tests/data/iTunesMusicLibrary.json", line =>
            {
                _tracks.Add(JsonConvert.DeserializeObject<IDictionary<string, object>>(line, settings));
                return true;
            });

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

            WriteLine("");
            WriteLine("");
            WriteLine($"Import {numDocs:D5} docs");
            _importBench.PrintReport();
            _importBench.PrintReport(1.0 / numDocs, "doc");
            WriteLine($"Update {numUpdates:D4} docs");
            _updateArtistsBench.PrintReport();
            _updateArtistsBench.PrintReport(1.0 / numUpdates, "update");
            WriteLine($"Query {numArtists:D4} artists");
            _queryArtistsBench.PrintReport();
            _queryArtistsBench.PrintReport(1.0 / numArtists, "row");
            WriteLine($"Query {numAlbums:D4} albums");
            _queryAlbumsBench.PrintReport();
            _queryAlbumsBench.PrintReport(1.0 / numArtists, "artist");
            WriteLine("Index by artist");
            _indexArtistsBench.PrintReport();
            _indexArtistsBench.PrintReport(1.0 / numDocs, "doc");
            WriteLine("Re-query artists");
            _queryIndexedArtistsBench.PrintReport();
            _queryIndexedArtistsBench.PrintReport(1.0 / numArtists, "row");
            WriteLine("Re-query albums");
            _queryIndexedAlbumsBench.PrintReport();
            _queryIndexedAlbumsBench.PrintReport(1.0 / numArtists, "artist");
            WriteLine("FTS Indexing");
            _indexFTSBench.PrintReport();
            _indexFTSBench.PrintReport(1.0 / numDocs, "doc");
            WriteLine("FTS Query");
            _queryFTSBench.PrintReport();
            _queryFTSBench.PrintReport(1.0 / numFTS, "row");
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
                    using (var doc = new MutableDocument(documentID, track)) {
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
                using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID),
                        SelectResult.Expression(Expression.Property("Artist")))
                    .From(DataSource.Database(Db))) {
                    var results = q.Execute();
                    foreach (var result in results) {
                        var artist = result.GetString(1);
                        if (artist?.StartsWith("The ") == true) {
                            using (var doc = Db.GetDocument(result.GetString(0)))
                            using (var mutableDoc = doc.ToMutable()){
                                mutableDoc.SetString("Artist", artist.Substring(4));
                                Db.Save(mutableDoc);
                                count++;
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
            var collation = Collation.Unicode().IgnoreCase(true).IgnoreAccents(true).Locale("en");

            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("Artist")))
                .From(DataSource.Database(Db))
                .Where(Expression.Property("Artist").NotNullOrMissing()
                    .And(Expression.Property("Compilation").IsNullOrMissing()))
                .GroupBy(Expression.Property("Artist").Collate(collation))
                   .OrderBy(Ordering.Expression(Expression.Property("Artist").Collate(collation)))) {
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
            // Workaround for https://github.com/couchbase/couchbase-lite-net/issues/1012
            var whereCollation = Collation.Unicode().IgnoreCase(true).IgnoreAccents(true);
            var collation = Collation.Unicode().IgnoreCase(true).IgnoreAccents(true);
            var collation2 = Collation.Unicode().IgnoreCase(true).IgnoreAccents(true);

            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("Album")))
                .From(DataSource.Database(Db))
                   .Where(Expression.Property("Artist").Collate(whereCollation).EqualTo(Expression.Parameter("ARTIST"))
                    .And(Expression.Property("Compilation").IsNullOrMissing()))
                .GroupBy(Expression.Property("Album").Collate(collation))
                   .OrderBy(Ordering.Expression(Expression.Property("Album").Collate(collation2)))) {
                bench.Start();
               
                foreach (var artist in _artists) {
                    var parameters = new Parameters();
                    parameters.SetString("ARTIST", artist);
                    q.Parameters = parameters;
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
            var index = IndexBuilder.ValueIndex(ValueIndexItem.Expression(artist), ValueIndexItem.Expression(comp));
            Db.CreateIndex("byArtist", index);
            _indexArtistsBench.Stop();
        }

        private int FullTextSearch()
        {
            _indexFTSBench.Start();
            var index = IndexBuilder.FullTextIndex(FullTextIndexItem.Property("Name")).SetLanguage("en");

            Db.CreateIndex("nameFTS", index);
            _indexFTSBench.Stop();

            var collate1 = Collation.Unicode().IgnoreAccents(true).IgnoreCase(true).Locale("en");
            var collate2 = Collation.Unicode().IgnoreAccents(true).IgnoreCase(true).Locale("en");

            var ARTIST = Expression.Property("Artist");
            var ALBUM = Expression.Property("Album");
            var NAME = Expression.Property("Name");
            var results = new List<string>();
            using (var q = QueryBuilder.Select(SelectResult.Expression(ARTIST), SelectResult.Expression(ALBUM),
                    SelectResult.Expression(NAME))
                .From(DataSource.Database(Db))
                   .Where(FullTextExpression.Index("nameFTS").Match("'Rock'"))
                   .OrderBy(Ordering.Expression(Expression.Property("Artist").Collate(collate1)),
                            Ordering.Expression(Expression.Property("Album").Collate(collate2)))) {
                _queryFTSBench.Start();
                var rows = q.Execute();
                foreach (var row in rows) {
                    results.Add(row.GetString(2));
                }
            }

            _queryFTSBench.Stop();
            results.Count.Should().Be(30);
            return results.Count;
        }

        private IList<string> CollectQueryResults(IQuery query)
        {
            var results = new List<string>();
            var rows = query.Execute();
            foreach (var row in rows) {
                results.Add(row.GetString(0));
            }

            return results;
        }
    }
}
#endif