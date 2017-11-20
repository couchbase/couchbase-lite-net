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
using Couchbase.Lite;
using Couchbase.Lite.Query;
using Couchbase.Lite.Sync;
using Newtonsoft.Json;

namespace api_walkthrough
{
    class Program
    {
        #region Private Methods

        static void Main(string[] args)
        {
            // This only needs to be done once for whatever platform the executable is running
            // (UWP, iOS, Android, or desktop)
            Couchbase.Lite.Support.NetDesktop.Activate();

            // create database
            var config = new DatabaseConfiguration();
            config.ConflictResolver = new ExampleConflictResolver();
            var database = new Database("my-database", config);

            // create document
            var newTask = new MutableDocument();
            newTask.Set("type", "task");
            newTask.Set("owner", "todo");
            newTask.Set("createdAt", DateTimeOffset.UtcNow);
            database.Save(newTask);

            // mutate document
            newTask.Set("name", "Apples");
            database.Save(newTask);

            // typed accessors
            newTask.Set("createdAt", DateTimeOffset.UtcNow);
            var date = newTask.GetDate("createdAt");

            // database transaction
            database.InBatch(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    using (var doc = new MutableDocument()) {
                        doc.Set("type", "user");
                        doc.Set("name", $"user {i}");
                        using (var saved = database.Save(doc)) {
                            Console.WriteLine($"saved user document {saved.GetString("name")}");
                        }
                    }
                }
            });

            // blob
            var bytes = File.ReadAllBytes("avatar.jpg");
            var blob = new Blob("image/jpg", bytes);
            newTask.Set("avatar", blob);
            database.Save(newTask);
            var taskBlob = newTask.GetBlob("avatar");
            var data = taskBlob.Content;
            newTask.Dispose();

            // query
            var query = Query.Select(SelectResult.Expression(Expression.Meta().ID))
            .From(DataSource.Database(database))
            .Where(Expression.Property("type").EqualTo("user")
		   .And(Expression.Property("admin").EqualTo(false)));

            var rows = query.Run();
            foreach (var row in rows)
            {
                Console.WriteLine($"doc ID :: ${row.GetString(0)}");
            }

            // live query
            var liveQuery = query.ToLive();
            liveQuery.Changed += (sender, e) => {
                Console.WriteLine($"Number of rows :: {e.Rows.Count}");
            };
            liveQuery.Run();
            using (var newDoc = new MutableDocument()) {
                newDoc.Set("type", "user");
                newDoc.Set("admin", false);
                database.Save(newDoc);
            }

            // fts example
            // insert documents
            var tasks = new[] { "buy groceries", "play chess", "book travels", "buy museum tickets" };
            foreach (string task in tasks)
            {
                using (var doc = new MutableDocument()) {
                    doc.Set("type", "task").Set("name", task); // Chaining is possible
                    database.Save(doc);
                }
            }

            // create Index
            var index = Index.FTSIndex().On(FTSIndexItem.Expression(Expression.Property("name")));
            database.CreateIndex("byName", index);

            var ftsQuery = Query.Select(SelectResult.Expression(Expression.Meta().ID).As("id"))
		    .From(DataSource.Database(database))
		    .Where(Expression.Property("name").Match("'buy'"));

            var ftsRows = ftsQuery.Run();
            foreach (var row in ftsRows) {
                var doc = database.GetDocument(row.GetString("id")); // Use alias instead of index
                Console.WriteLine($"document properties {JsonConvert.SerializeObject(doc.ToDictionary(), Formatting.Indented)}");
            }

            // create conflict
            /*
			 * 1. Create a document twice with the same ID (the document will have two conflicting revisions).
			 * 2. Upon saving the second revision, the ExampleConflictResolver's resolve method is called.
			 * The `theirs` ReadOnlyDocument in the conflict resolver represents the current rev and `mine` is what's being saved.
			 * 3. Read the document after the second save operation and verify its property is as expected.
			 * The conflict resolver will have deleted the obsolete revision.
			 */
            using (var theirs = new MutableDocument("buzz"))
            using (var mine = new MutableDocument("buzz")) {
                theirs.Set("status", "theirs");
                mine.Set("status", "mine");
                database.Save(theirs);
                database.Save(mine);
            }
            
            var conflictResolverResult = database.GetDocument("buzz");
            Console.WriteLine($"conflictResolverResult doc.status ::: {conflictResolverResult.GetString("status")}");

            // replication (Note: Linux / Mac requires .NET Core 2.0+ due to
            // https://github.com/dotnet/corefx/issues/8768)
			/*
             * Tested with SG 1.5 https://www.couchbase.com/downloads
             * Config file:
             * {
				  "databases": {
				    "db": {
				      "server":"walrus:",
				      "users": {
				        "GUEST": {"disabled": false, "admin_channels": ["*"]}
				      },
				      "unsupported": {
				        "replicator_2":true
				      }
				    }
				  }
				}
             */
            var url = new Uri("blip://localhost:4984/db");
            var replConfig = new ReplicatorConfiguration(database, url);
            var replication = new Replicator(replConfig);
            replication.Start();

            // replication change listener
            replication.StatusChanged += (sender, e) => {
                if (e.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    Console.WriteLine("Replication has completed.");
                }
            };

            Console.ReadLine();

            // This is important to do because otherwise the native connection
            // won't be released until the next garbage collection
            database.Dispose();
        }

        #endregion
    }
}