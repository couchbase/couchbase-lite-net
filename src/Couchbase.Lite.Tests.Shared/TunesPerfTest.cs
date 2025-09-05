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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;
using Shouldly;

using Test.Util;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public sealed class TunesPerfTest(ITestOutputHelper output) : PerfTest(output)
    {
        private int _documentCount;
        private List<IDictionary<string, object?>> _tracks = [];
        private IList<string> _artists = new List<string>();
        private readonly Benchmark _importBench = new(output);
        private readonly Benchmark _updateArtistsBench = new(output);
        private readonly Benchmark _indexArtistsBench = new(output);
        private readonly Benchmark _queryArtistsBench = new(output);
        private readonly Benchmark _queryIndexedArtistsBench = new(output);
        private readonly Benchmark _queryAlbumsBench = new(output);
        private readonly Benchmark _queryIndexedAlbumsBench = new(output);
        private readonly Benchmark _indexFTSBench = new(output);
        private readonly Benchmark _queryFTSBench = new(output);

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
            _tracks = new();

            TestCase.ReadFileByLines("C/tests/data/iTunesMusicLibrary.json", line =>
            {
                _tracks.Add(DataOps.ParseTo<IDictionary<string, object>>(line)!);
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
                numArtists2.ShouldBe(numArtists);
                var numAlbums2 = QueryAlbums(_queryIndexedAlbumsBench);
                numAlbums2.ShouldBe(numAlbums);
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
            Db.ShouldNotBeNull().InBatch(() =>
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
                    using var doc = new MutableDocument(documentID, track);
                    DefaultCollection.Save(doc);
                }
            });

            _importBench.Stop();
            return _documentCount;
        }

        private int UpdateArtistNames()
        {
            _updateArtistsBench.Start();
            var count = 0;
            Db.ShouldNotBeNull().InBatch(() =>
            {
                using var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID),
                        SelectResult.Expression(Expression.Property("Artist")))
                    .From(DataSource.Collection(DefaultCollection));
                var results = q.Execute();
                foreach (var result in results) {
                    var artist = result.GetString(1);
                    if (artist?.StartsWith("The ") != true) {
                        continue;
                    }
                    
                    using var doc = DefaultCollection.GetDocument(result.GetString(0).ShouldNotBeNull());
                    using var mutableDoc = doc.ShouldNotBeNull().ToMutable();
                    mutableDoc.SetString("Artist", artist.Substring(4));
                    DefaultCollection.Save(mutableDoc);
                    count++;
                }
            });

            _updateArtistsBench.Stop();
            return count;
        }

        private int QueryAllArtists(Benchmark bench)
        {
            var collation = Collation.Unicode().IgnoreCase(true).IgnoreAccents(true).Locale("en");

            using var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("Artist")))
                .From(DataSource.Collection(DefaultCollection))
                .Where(Expression.Property("Artist").IsValued()
                    .And(Expression.Property("Compilation").IsNotValued()))
                .GroupBy(Expression.Property("Artist").Collate(collation))
                .OrderBy(Ordering.Expression(Expression.Property("Artist").Collate(collation)));
            bench.Start();
            _artists = CollectQueryResults(q);
            bench.Stop();
            _artists.Count.ShouldBe(1111);
            return _artists.Count;
        }

        private int QueryAlbums(Benchmark bench)
        {
            var albumCount = 0;
            var collation = Collation.Unicode().IgnoreCase(true).IgnoreAccents(true);

            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("Album")))
                .From(DataSource.Collection(DefaultCollection))
                   .Where(Expression.Property("Artist").Collate(collation).EqualTo(Expression.Parameter("ARTIST"))
                    .And(Expression.Property("Compilation").IsNotValued()))
                .GroupBy(Expression.Property("Album").Collate(collation))
                   .OrderBy(Ordering.Expression(Expression.Property("Album").Collate(collation)))) {
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
            albumCount.ShouldBe(1886);
            return albumCount;
        }

        private void CreateArtistsIndex()
        {
            _indexArtistsBench.Start();
            var collation = Collation.Unicode().IgnoreCase(true).IgnoreAccents(true);

            var artist = Expression.Property("Artist").Collate(collation);
            var comp = Expression.Property("Compilation");
            var index = IndexBuilder.ValueIndex(ValueIndexItem.Expression(artist), ValueIndexItem.Expression(comp));
            DefaultCollection.CreateIndex("byArtist", index);
            _indexArtistsBench.Stop();
        }

        private int FullTextSearch()
        {
            _indexFTSBench.Start();
            var index = IndexBuilder.FullTextIndex(FullTextIndexItem.Property("Name")).SetLanguage("en");

            DefaultCollection.CreateIndex("nameFTS", index);
            _indexFTSBench.Stop();

            var collate1 = Collation.Unicode().IgnoreAccents(true).IgnoreCase(true).Locale("en");
            var collate2 = Collation.Unicode().IgnoreAccents(true).IgnoreCase(true).Locale("en");

            var artist = Expression.Property("Artist");
            var album = Expression.Property("Album");
            var name = Expression.Property("Name");
            var results = new List<string>();
            using (var q = QueryBuilder.Select(SelectResult.Expression(artist), SelectResult.Expression(album),
                    SelectResult.Expression(name))
                .From(DataSource.Collection(DefaultCollection))
                   .Where(FullTextFunction.Match(Expression.FullTextIndex("nameFTS"),"'Rock'"))
                   .OrderBy(Ordering.Expression(Expression.Property("Artist").Collate(collate1)),
                            Ordering.Expression(Expression.Property("Album").Collate(collate2)))) {
                _queryFTSBench.Start();
                var rows = q.Execute();
                results.AddRange(rows.Select(row => row.GetString(2).ShouldNotBeNull()));
            }

            _queryFTSBench.Stop();
            results.Count.ShouldBe(30);
            return results.Count;
        }

        private static IList<string> CollectQueryResults(IQuery query)
        {
            var rows = query.Execute();
            return rows.Select(row => row.GetString(0).ShouldNotBeNull()).ToList();
        }
    }
}
#endif