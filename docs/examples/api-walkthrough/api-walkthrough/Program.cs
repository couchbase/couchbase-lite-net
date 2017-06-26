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

namespace api_walkthrough
{
    class Program
    {
        #region Private Methods

        static void Main(string[] args)
        {
            // This only needs to be done once for whatever platform the executable is running
            // (UWP, iOS, Android, or desktop)
            Couchbase.Lite.Support.NetDestkop.Activate();

            // create database
            var database = new Database("my-database");

            // create document
            var newTask = new Document();
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
                    var doc = new Document();
                    doc.Set("type", "user");
                    doc.Set("name", $"user {i}");
                    database.Save(doc);
                    Console.WriteLine($"saved user document {doc.GetString("name")}");
                }
            });

            // blob
            var bytes = File.ReadAllBytes("avatar.jpg");
            var blob = new Blob("image/jpg", bytes);
            newTask.Set("avatar", blob);
            database.Save(newTask);
            var taskBlob = newTask.GetBlob("avatar");
            var data = taskBlob.Content;

            // query
            var query = QueryFactory.Select()
            .From(DataSourceFactory.Database(database))
            .Where(ExpressionFactory.Property("type").EqualTo("user")
		   .And(ExpressionFactory.Property("admin").EqualTo(false)));

            var rows = query.Run();
            foreach (var row in rows)
            {
                Console.WriteLine($"doc ID :: ${row.DocumentID}");
            }

            // live query
            var liveQuery = query.ToLive();
            liveQuery.Changed += (sender, e) => {
                Console.WriteLine($"Number of rows :: {e.Rows.Count}");
            };
            liveQuery.Run();
            var newDoc = new Document();
            newDoc.Set("type", "user");
            newDoc.Set("admin", false);
            database.Save(newDoc);

            // fts example
            // insert documents
            var tasks = new[] { "buy groceries", "play chess", "book travels", "buy museum tickets" };
            foreach (string task in tasks)
            {
                using (var doc = new Document()) {
                    doc.Set("type", "task").Set("name", task); // Chaining is possible
                    database.Save(doc);
                }
            }

            // create Index
            database.CreateIndex(new[] { "name" }, IndexType.FullTextIndex, null);

            var ftsQuery = QueryFactory.Select()
		    .From(DataSourceFactory.Database(database))
		    .Where(ExpressionFactory.Property("name").Match("'buy'"));

            var ftsRows = ftsQuery.Run();
            foreach (var row in ftsRows)
            {
                Console.WriteLine($"document properties {row.Document.ToDictionary()}");
            }

            // replication
            var url = new Uri("blip://localhost:4984/db");
            var config = new ReplicatorConfiguration(database, url);
            var replication = new Replicator(config);
            replication.Start();

            // replication change listener
            replication.StatusChanged += (object sender, ReplicationStatusChangedEventArgs e) => {
                Console.WriteLine(replication.Status.Activity);
            };

            Console.ReadLine();

            // This is important to do because otherwise the native connection
            // won't be released until the next garbage collection
            database.Dispose();
        }

        #endregion
    }
}