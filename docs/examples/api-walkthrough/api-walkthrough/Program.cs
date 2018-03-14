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
using System.Linq;
using System.Threading;

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
            var database = new Database("my-database", config);

            // create document
            using (var newTask = new MutableDocument()) {
                newTask.SetString("type", "task");
                newTask.SetString("owner", "todo");
                newTask.SetDate("createdAt", DateTimeOffset.UtcNow);
                database.Save(newTask);

                // mutate document
                newTask.SetString("name", "Apples");
                database.Save(newTask);

                // typed accessors
                newTask.SetDate("createdAt", DateTimeOffset.UtcNow);
                var date = newTask.GetDate("createdAt");

                // database transaction
                database.InBatch(() =>
                {
                    for (int i = 0; i < 10; i++) {
                        using (var doc = new MutableDocument()) {
                            doc.SetString("type", "user");
                            doc.SetString("name", $"user {i}");
                            database.Save(doc);
                            Console.WriteLine($"saved user document {doc.GetString("name")}");
                        }
                    }
                });

                // blob
                var bytes = File.ReadAllBytes("avatar.jpg");
                var blob = new Blob("image/jpg", bytes);
                newTask.SetBlob("avatar", blob);
                database.Save(newTask);
                var taskBlob = newTask.GetBlob("avatar");
                var data = taskBlob.Content;
            }

            // query
            var query = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
            .From(DataSource.Database(database))
            .Where(Expression.Property("type").EqualTo(Expression.String("user"))
		   .And(Expression.Property("admin").EqualTo(Expression.Boolean(false))));

            var rows = query.Execute();
            foreach (var row in rows) {
                Console.WriteLine($"doc ID :: ${row.GetString(0)}");
            }
            

            // live query
            var token = query.AddChangeListener((sender, e) => {
                Console.WriteLine($"Number of rows :: {e.Results.Count()}");
            });

            using (var newDoc = new MutableDocument()) {
                newDoc.SetString("type", "user");
                newDoc.SetBoolean("admin", false);
                database.Save(newDoc);
            }

            // fts example
            // insert documents
            var tasks = new[] { "buy groceries", "play chess", "book travels", "buy museum tickets" };
            foreach (string task in tasks)
            {
                using (var doc = new MutableDocument()) {
                    doc.SetString("type", "task").SetString("name", task); // Chaining is possible
                    database.Save(doc);
                }
            }

            // create Index
            var index = IndexBuilder.FullTextIndex(FullTextIndexItem.Property("name"));
            database.CreateIndex("byName", index);

            using (var ftsQuery = QueryBuilder.Select(SelectResult.Expression(Meta.ID).As("id"))
                .From(DataSource.Database(database))
                .Where(FullTextExpression.Index("byName").Match("'buy'"))) {

                var ftsRows = ftsQuery.Execute();
                foreach (var row in ftsRows) {
                    var doc = database.GetDocument(row.GetString("id")); // Use alias instead of index
                    Console.WriteLine(
                        $"document properties {JsonConvert.SerializeObject(doc.ToDictionary(), Formatting.Indented)}");
                }
            }
            
            /*
             * Tested with SG 2.0 https://www.couchbase.com/downloads
             * Config file:
             * {
				  "databases": {
				    "db": {
				      "server":"walrus:",
				      "users": {
				        "GUEST": {"disabled": false, "admin_channels": ["*"]}
				      }
				    }
				  }
				}
             */
            var url = new Uri("ws://localhost:4984/db");
            var replConfig = new ReplicatorConfiguration(database, new URLEndpoint(url));
            var replication = new Replicator(replConfig);
            replication.Start();

            // replication change listener
            replication.AddChangeListener((sender, e) => {
                if (e.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    Console.WriteLine("Replication has completed.");
                }
            });

            Console.ReadLine();

            // This is important to do because otherwise the native connection
            // won't be released until the next garbage collection
            query.RemoveChangeListener(token);
            query.Dispose();
            replication.Stop();

            while (replication.Status.Activity != ReplicatorActivityLevel.Stopped) {
                Console.WriteLine("Waiting for replicator to stop...");
                Thread.Sleep(1000);
            }

            database.Dispose();
        }

        #endregion
    }
}