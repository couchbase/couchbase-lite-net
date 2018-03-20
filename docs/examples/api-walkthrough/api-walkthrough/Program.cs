// 
// Program.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Sync;
using Newtonsoft.Json;

using SkiaSharp;

namespace api_walkthrough
{
    class Program
    {
        private static Database _Database;
        private static Replicator _Replicator;
        private static ListenerToken _ListenerToken;
        private static bool _NeedsExtraDocs;

        #region Private Methods

        private static void CreateNewDatabase()
        {
            // # tag::new-database[]
            var db = new Database("my-database");
            // # end::new-database[]

            _Database = db;
        }

        private static void ChangeLogging()
        {
            // # tag::logging[]
            Database.SetLogLevel(LogDomain.Replicator, LogLevel.Verbose);
            Database.SetLogLevel(LogDomain.Query, LogLevel.Verbose);
            // # end::logging[]
        }

        private static void LoadPrebuilt()
        {
            // # tag::prebuilt-database[]
            // Note: Getting the path to a database is platform-specific.  For .NET Core / .NET Framework this
            // can be a simple filesystem path.  For UWP, you will need to get the path from your assets.  For
            // iOS you need to get the path from the main bundle.  For Android you need to extract it from your
            // assets to a temporary directory and then pass that path.
            var path = Path.Combine(Environment.CurrentDirectory, "travel-sample.cblite2" + Path.DirectorySeparatorChar);
            if (!Database.Exists("travel-sample", null)) {
                _NeedsExtraDocs = true;
                Database.Copy(path, "travel-sample", null);
            }
            // # end::prebuilt-database[]

            _Database.Close();
            _Database = new Database("travel-sample");
        }

        private static void CreateDocument()
        {
            var db = _Database;
            // # tag::initializer[]
            using (var newTask = new MutableDocument("xyz")) {
                newTask.SetString("type", "task")
                    .SetString("owner", "todo")
                    .SetDate("createdAt", DateTimeOffset.UtcNow);

                db.Save(newTask);
            }
            // # end::initializer[]
        }

        private static void UpdateDocument()
        {
            var db = _Database;
            // # tag::update-document[]
            using(var document = db.GetDocument("xyz"))
            using (var mutableDocument = document.ToMutable()) {
                mutableDocument.SetString("name", "apples");
                db.Save(mutableDocument);
            }
            // # end::update-document[]
        }

        private static void UseTypedAccessors()
        {
            using (var newTask = new MutableDocument()) {
                // # tag::date-getter[]
                newTask.SetValue("createdAt", DateTimeOffset.UtcNow);
                var date = newTask.GetDate("createdAt");
                // # end::date-getter[]

                Console.WriteLine(date);
            }
        }

        private static void DoBatchOperation()
        {
            var db = _Database;
            // # tag::batch[]
            db.InBatch(() =>
            {
                for (var i = 0; i < 10; i++) {
                    using (var doc = new MutableDocument()) {
                        doc.SetString("type", "user");
                        doc.SetString("name", $"user {i}");
                        doc.SetBoolean("admin", false);
                        db.Save(doc);
                        Console.WriteLine($"Saved user document {doc.GetString("name")}");
                    }
                }
            });
            // # end::batch[]
        }

        private static void UseBlob()
        {
            var db = _Database;
            using (var newTask = new MutableDocument()) {
                // # tag::blob[]
                // Note: Reading the data is implementation dependent, as with prebuilt databases
                var image = File.ReadAllBytes("avatar.jpg");
                var blob = new Blob("image/jpeg", image);
                newTask.SetBlob("avatar", blob);
                db.Save(newTask);
                // # end::blob[]
                
                var taskBlob = newTask.GetBlob("avatar");
                using (var bitmap = SKBitmap.Decode(taskBlob.ContentStream)) {
                    Console.WriteLine($"Bitmap dimensions: {bitmap.Width} x {bitmap.Height} ({bitmap.BytesPerPixel} bytes per pixel)");
                }
            }
        }

        private static void CreateIndex()
        {
            var db = _Database;

            // # tag::query-index[]
            // For value types, this is optional but provides performance enhancements
            var index = IndexBuilder.ValueIndex(
                ValueIndexItem.Expression(Expression.Property("type")),
                ValueIndexItem.Expression(Expression.Property("name")));
            db.CreateIndex("TypeNameIndex", index);
            // # end::query-index[]
        }

        private static void SelectMeta()
        {
            Console.WriteLine("Select Meta");
            var db = _Database;

            // # tag::query-select-meta[]
            using (var query = QueryBuilder.Select(
                    SelectResult.Expression(Meta.ID),
                    SelectResult.Property("type"), 
                    SelectResult.Property("name"))
                .From(DataSource.Database(db))) {
                foreach (var result in query.Execute()) {
                    Console.WriteLine($"Document ID :: {result.GetString("id")}");
                    Console.WriteLine($"Document Name :: {result.GetString("name")}");
                }
            }
            // # end::query-select-meta[]
        }

        private static void SelectAll()
        {
            var db = _Database;

            // # tag::query-select-all[]
            using (var query = QueryBuilder.Select(SelectResult.All())
                .From(DataSource.Database(db))) {
                // All user properties will be available here
            }
            // # end::query-select-all[]
        }

        private static void SelectWhere()
        {
            Console.WriteLine("Where");
            var db = _Database;

            // # tag::query-where[]
            using (var query = QueryBuilder.Select(SelectResult.All())
                .From(DataSource.Database(db))
                .Where(Expression.Property("type").EqualTo(Expression.String("hotel")))
                .Limit(Expression.Int(10))) {
                foreach (var result in query.Execute()) {
                    var dict = result.GetDictionary(db.Name);
                    Console.WriteLine($"Document Name :: {dict?.GetString("name")}");
                }
            }
            // # end::query-where[]
        }

        private static void UseCollectionOperators()
        {
            Console.WriteLine("Collection Operators");
            var db = _Database;

            // # tag::query-collection-operator[]
            using (var query = QueryBuilder.Select(
                    SelectResult.Expression(Meta.ID),
                    SelectResult.Property("name"),
                    SelectResult.Property("public_likes"))
                .From(DataSource.Database(db))
                .Where(Expression.Property("type").EqualTo(Expression.String("hotel"))
                    .And(ArrayFunction.Contains(Expression.Property("public_likes"),
                        Expression.String("Armani Langworth"))))) {
                foreach (var result in query.Execute()) {
                    var publicLikes = result.GetArray("public_likes");
                    var jsonString = JsonConvert.SerializeObject(publicLikes);
                    Console.WriteLine($"Public Likes :: {jsonString}");
                }
            }
            // # end::query-collection-operator[]
        }

        private static void SelectLike()
        {
            Console.WriteLine("Like");
            var db = _Database;

            // # tag::query-like-operator[]
            using (var query = QueryBuilder.Select(
                    SelectResult.Expression(Meta.ID),
                    SelectResult.Property("name"))
                .From(DataSource.Database(db))
                .Where(Expression.Property("type").EqualTo(Expression.String("landmark"))
                    .And(Expression.Property("name").Like(Expression.String("Royal Engineers Museum"))))
                .Limit(Expression.Int(10))) {
                foreach (var result in query.Execute()) {
                    Console.WriteLine($"Name Property :: {result.GetString("name")}");
                }
            }
            // # end::query-like-operator[]
        }

        private static void SelectWildcardLike()
        {
            Console.WriteLine("Wildcard Like");
            var db = _Database;

            // # tag::query-like-operator-wildcard-match[]
            using (var query = QueryBuilder.Select(
                    SelectResult.Expression(Meta.ID),
                    SelectResult.Property("name"))
                .From(DataSource.Database(db))
                .Where(Expression.Property("type").EqualTo(Expression.String("landmark"))
                    .And(Expression.Property("name").Like(Expression.String("Eng%e%"))))
                .Limit(Expression.Int(10))) {
                foreach (var result in query.Execute()) {
                    Console.WriteLine($"Name Property :: {result.GetString("name")}");
                }
            }
            // # end::query-like-operator-wildcard-match[]
        }

        private static void SelectWildcardCharacterLike()
        {
            Console.WriteLine("Wildchard Characters");
            var db = _Database;

            // # tag::query-like-operator-wildcard-character-match[]
            using (var query = QueryBuilder.Select(
                    SelectResult.Expression(Meta.ID),
                    SelectResult.Property("name"))
                .From(DataSource.Database(db))
                .Where(Expression.Property("type").EqualTo(Expression.String("landmark"))
                    .And(Expression.Property("name").Like(Expression.String("Royal Eng____rs Museum"))))
                .Limit(Expression.Int(10))) {
                foreach (var result in query.Execute()) {
                    Console.WriteLine($"Name Property :: {result.GetString("name")}");
                }
            }
            // # end::query-like-operator-wildcard-character-match[]
        }

        private static void SelectRegex()
        {
            Console.WriteLine("Regex");
            var db = _Database;

            // # tag::query-regex-operator[]
            using (var query = QueryBuilder.Select(
                    SelectResult.Expression(Meta.ID),
                    SelectResult.Property("name"))
                .From(DataSource.Database(db))
                .Where(Expression.Property("type").EqualTo(Expression.String("landmark"))
                    .And(Expression.Property("name").Regex(Expression.String("\\bEng.*e\\b"))))
                .Limit(Expression.Int(10))) {
                foreach (var result in query.Execute()) {
                    Console.WriteLine($"Name Property :: {result.GetString("name")}");
                }
            }
            // # end::query-regex-operator[]
        }

        private static void SelectJoin()
        {
            Console.WriteLine("Join");
            var db = _Database;

            // # tag::query-join[]
            using (var query = QueryBuilder.Select(
                    SelectResult.Expression(Expression.Property("name").From("airline")),
                    SelectResult.Expression(Expression.Property("callsign").From("airline")),
                    SelectResult.Expression(Expression.Property("destinationairport").From("route")),
                    SelectResult.Expression(Expression.Property("stops").From("route")),
                    SelectResult.Expression(Expression.Property("airline").From("route")))
                .From(DataSource.Database(db).As("airline"))
                .Join(Join.InnerJoin(DataSource.Database(db).As("route"))
                    .On(Meta.ID.From("airline").EqualTo(Expression.Property("airlineid").From("route"))))
                .Where(Expression.Property("type").From("route").EqualTo(Expression.String("route"))
                    .And(Expression.Property("type").From("airline").EqualTo(Expression.String("airline")))
                    .And(Expression.Property("sourceairport").From("route").EqualTo(Expression.String("RIX"))))) {
                foreach (var result in query.Execute()) {
                    Console.WriteLine($"Name Property :: {result.GetString("name")}");
                }
            }
            // # end::query-join[]
        }

        private static void GroupBy()
        {
            Console.WriteLine("GroupBy");
            var db = _Database;

            // # tag::query-groupby[]
            using (var query = QueryBuilder.Select(
                    SelectResult.Expression(Function.Count(Expression.All())),
                    SelectResult.Property("country"),
                    SelectResult.Property("tz"))
                .From(DataSource.Database(db))
                .Where(Expression.Property("type").EqualTo(Expression.String("airport"))
                    .And(Expression.Property("geo.alt").GreaterThanOrEqualTo(Expression.Int(300))))
                .GroupBy(Expression.Property("country"), Expression.Property("tz"))) {
                foreach (var result in query.Execute()) {
                    Console.WriteLine(
                        $"There are {result.GetInt("$1")} airports in the {result.GetString("tz")} timezone located in {result.GetString("country")} and above 300 ft");
                }
            }
            // # end::query-groupby[]
        }

        private static void OrderBy()
        {
            Console.WriteLine("OrderBy");
            var db = _Database;

            // # tag::query-orderby[]
            using (var query = QueryBuilder.Select(
                    SelectResult.Expression(Meta.ID),
                    SelectResult.Property("title"))
                .From(DataSource.Database(db))
                .Where(Expression.Property("type").EqualTo(Expression.String("hotel")))
                .OrderBy(Ordering.Property("title").Ascending())
                .Limit(Expression.Int(10))) {
                foreach (var result in query.Execute()) {
                    Console.WriteLine($"Title :: {result.GetString("title")}");
                }
            }
            // # end::query-orderby[]
        }

        private static void CreateFullTextIndex()
        {
            var db = _Database;
            if (_NeedsExtraDocs) {
                var tasks = new[] { "buy groceries", "play chess", "book travels", "buy museum tickets" };
                foreach (var task in tasks) {
                    using (var doc = new MutableDocument()) {
                        doc.SetString("type", "task");
                        doc.SetString("name", task);
                        db.Save(doc);
                    }
                }
            }

            // # tag::fts-index[]
            var index = IndexBuilder.FullTextIndex(FullTextIndexItem.Property("name")).IgnoreAccents(false);
            db.CreateIndex("nameFTSIndex", index);
            // # end::fts-index[]
        }

        private static void FullTextSearch()
        {
            Console.WriteLine("Full text search");
            var db = _Database;

            // # tag::fts-query[]
            var whereClause = FullTextExpression.Index("nameFTSIndex").Match("'buy'");
            using (var query = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Database(db))
                .Where(whereClause)) {
                foreach (var result in query.Execute()) {
                    Console.WriteLine($"Document id {result.GetString(0)}");
                }
            }
            // # end::fts-query[]
        }
        private static void StartReplication()
        {
            var db = _Database;

            /*
             * This requires Sync Gateway running with the following config, or equivalent:
             *
             * {
             *     "log":["*"],
             *     "databases": {
             *         "db": {
             *             "server":"walrus:",
             *             "users": {
             *                 "GUEST": {"disabled": false, "admin_channels": ["*"] }
             *             }
             *         }
             *     }
             * }
             */

            // # tag::replication[]
            // Note: Android emulator needs to use 10.0.2.2 for localhost (10.0.3.2 for GenyMotion)
            var url = new Uri("ws://localhost:4984/db");
            var target = new URLEndpoint(url);
            var config = new ReplicatorConfiguration(db, target)
            {
                ReplicatorType = ReplicatorType.Pull
            };

            var replicator = new Replicator(config);
            replicator.Start();
            // # end::replication[]

            _Replicator = replicator;
        }

        private static void VerboseReplicatorLogging()
        {
            // # tag::replication-logging[]
            Database.SetLogLevel(LogDomain.Replicator, LogLevel.Verbose);
            Database.SetLogLevel(LogDomain.Network, LogLevel.Verbose);
            // # end::replication-logging[]
        }

        private static void SetupReplicatorListener()
        {
            var replicator = _Replicator;

            // # tag::replication-status[]
            replicator.AddChangeListener((sender, args) =>
            {
                if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    Console.WriteLine("Replication stopped");
                }
            });
            // # end::replication-status[]
        }

        private static void SetupReplicatorErrorListener()
        {
            // This can be done in the SetupReplicatorListener method
            // But it is separate so that we can have two documentation entries

            var replicator = _Replicator;

            // # tag::replication-error-handling[]
            replicator.AddChangeListener((sender, args) =>
            {
                if (args.Status.Error != null) {
                    Console.WriteLine($"Error :: {args.Status.Error}");
                }
            });
            // # end::replication-error-handling[]
        }

        private static void DatabaseReplica()
        {
            var db = _Database;
            using (var database2 = new Database("backup")) {
                // EE feature: This code will not compile on the community edition
                // # tag::database-replica[]
                var targetDatabase = new DatabaseEndpoint(database2);
                var config = new ReplicatorConfiguration(db, targetDatabase)
                {
                    ReplicatorType = ReplicatorType.Push
                };

                var replicator = new Replicator(config);
                replicator.Start();
                // # end::database-replica[]

                _Replicator?.Stop();
                _Replicator = replicator;
            }
        }

        private static void PinCertificate()
        {
            // Note: No certificate is included here, so this code is for show only
            var url = new Uri("wss://localhost:4984/db");
            var target = new URLEndpoint(url);
            var db = _Database;

            // # tag::certificate-pinning[]
            var certificate = new X509Certificate2("cert.cer");
            var config = new ReplicatorConfiguration(db, target)
            {
                PinnedServerCertificate = certificate
            };
            // # end::certificate-pinning[]
        }

        static void Main(string[] args)
        {
            // This only needs to be done once for whatever platform the executable is running
            // (UWP, iOS, Android, or desktop)
            Couchbase.Lite.Support.NetDesktop.Activate();

            CreateNewDatabase();
            CreateDocument();
            UpdateDocument();
            UseTypedAccessors();
            DoBatchOperation();
            UseBlob();
            SelectMeta();
            
            LoadPrebuilt();
            CreateIndex();
            SelectWhere();
            UseCollectionOperators();
            SelectLike();
            SelectWildcardLike();
            SelectWildcardCharacterLike();
            SelectRegex();
            SelectJoin();
            GroupBy();
            OrderBy();

            CreateFullTextIndex();
            FullTextSearch();
            StartReplication();
            SetupReplicatorListener();

            _Replicator.Stop();
            while (_Replicator.Status.Activity != ReplicatorActivityLevel.Stopped) {
                // Database cannot close until replicators are stopped
                Console.WriteLine($"Waiting for replicator to stop (currently {_Replicator.Status.Activity})...");
                Thread.Sleep(200);
            }

            _Database.Close();
        }

        #endregion
    }
}