# Couchbase Lite .NET

Couchbase Lite is a lightweight embedded NoSQL database that has built-in sync to larger backend structures, such as Couchbase Server.

This is the source repo of Couchbase Lite C#. The main supported platforms are .NET 6 Desktop (Linux will run anywhere, but is most tested on CentOS and Ubuntu), .NET 7 iOS, .NET 7 Android, .NET 7 Mac Catalyst, UWP, Xamarin iOS, and Xamarin Android.

## Documentation Overview

* [Official Documentation](https://docs.couchbase.com/couchbase-lite/current/index.html)
* API References - See the release notes for each release on this repo

## Getting Started

The following starter code demonstrates the basic concepts of the library:

```c#

using System;
using Couchbase.Lite;
using Couchbase.Lite.Query;
using Couchbase.Lite.Sync;

// Get the database (and create it if it doesn't exist)
var database = new Database("mydb");
var collection = database.GetDefaultCollection();

// Create a new document (i.e. a record) in the database
var id = default(string);
using var createdDoc = new MutableDocument();
createdDoc.SetFloat("version", 2.0f)
    .SetString("type", "SDK");

// Save it to the database
collection.Save(createdDoc);
id = createdDoc.Id;

// Update a document
using var doc = collection.GetDocument(id);
using var mutableDoc = doc.ToMutable();
createdDoc.SetString("language", "C#");
collection.Save(createdDoc);

using var docAgain = collection.GetDocument(id);
Console.WriteLine($"Document ID :: {docAgain.Id}");
Console.WriteLine($"Learning {docAgain.GetString("language")}");


// Create a query to fetch documents of type SDK
// i.e. SELECT * FROM database WHERE type = "SDK"
using var query = QueryBuilder.Select(SelectResult.All())
    .From(DataSource.Collection(collection))
    .Where(Expression.Property("type").EqualTo(Expression.String("SDK")));

// Alternatively, using SQL++, with _ referring to the database
using var sqlppQuery = collection.CreateQuery("SELECT * FROM _ WHERE type = 'SDK'");

// Run the query
var result = query.Execute();
Console.WriteLine($"Number of rows :: {result.AllResults().Count}");

// Create replicator to push and pull changes to and from the cloud
var targetEndpoint = new URLEndpoint(new Uri("ws://localhost:4984/getting-started-db"));
var replConfig = new ReplicatorConfiguration(targetEndpoint);
replConfig.AddCollection(database.GetDefaultCollection());

// Add authentication
replConfig.Authenticator = new BasicAuthenticator("john", "pass");

// Create replicator
var replicator = new Replicator(replConfig);
replicator.AddChangeListener((sender, args) =>
{
    if (args.Status.Error != null) {
        Console.WriteLine($"Error :: {args.Status.Error}");
    }
});

replicator.Start();

// Later, stop and dispose the replicator *before* closing/disposing the database
```