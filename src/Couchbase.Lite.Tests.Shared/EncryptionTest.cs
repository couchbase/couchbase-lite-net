//
//  EncryptionTest.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Couchbase.Lite.Store;
using Couchbase.Lite.Util;
using NUnit.Framework;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class EncryptionTest : LiteTestCase
    {
        private SymmetricKey _letmein;
        private SymmetricKey _letmeout;
        private SymmetricKey _wrong;
        private const string TAG = "EncryptionTest";

        public EncryptionTest(string storageType) : base(storageType) {}


        [TestFixtureSetUp]
        public void OneTimeSetUp()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine("Generating keys for test, this might take a while...");
            _letmein = SymmetricKey.Create("letmein");
            Console.WriteLine("Keys completed (1/3)");
            _letmeout = SymmetricKey.Create("letmeout");
            Console.WriteLine("Keys completed (2/3)");
            _wrong = SymmetricKey.Create("wrong");
            Console.WriteLine("Keys completed (3/3)");
            sw.Stop();
            Console.WriteLine("Created three keys in {0}ms", sw.ElapsedMilliseconds);
        }

        [Test]
        public void TestUnencryptedDB()
        {
            // Create unencrypted DB:
            var options = new DatabaseOptions();
            options.Create = true;
            var seekrit = manager.OpenDatabase("seekrit", options);
            Assert.IsNotNull(seekrit, "Failed to create db");
            CreateDocumentWithProperties(seekrit, new Dictionary<string, object> {
                { "answer", 42 }
            });

            Assert.DoesNotThrow(() => seekrit.Close().Wait(15000));

            options.EncryptionKey = _wrong;
            var e = Assert.Throws<CouchbaseLiteException>(() => seekrit = manager.OpenDatabase("seekrit", options), 
                "Shouldn't have been able to reopen encrypted db with wrong password");
            Assert.AreEqual(StatusCode.Unauthorized, e.Code);

            seekrit.ChangeEncryptionKey(null);
            options.EncryptionKey = null;
            seekrit = manager.OpenDatabase("seekrit", options);
            Assert.IsNotNull(seekrit, "Failed to reopen db");
            Assert.AreEqual(1, seekrit.GetDocumentCount());
        }

        [Test]
        public void TestEncryptedDB()
        {
            var options = new DatabaseOptions();
            options.Create = true;
            options.EncryptionKey = _letmein;
            var seekrit = default(Database);
            Assert.DoesNotThrow(() => seekrit = manager.OpenDatabase("seekrit", options),
                "Failed to create encrypted DB");
            CreateDocumentWithProperties(seekrit, new Dictionary<string, object> { { "answer", 42 } });
            Assert.DoesNotThrow(() => seekrit.Close().Wait(15000));

            // Try to reopen without the password (fails):
            options.EncryptionKey = null;
            var e = Assert.Throws<CouchbaseLiteException>(() => seekrit = manager.OpenDatabase("seekrit", options),
                "Shouldn't have been able to reopen encrypted db with wrong password");
            Assert.AreEqual(StatusCode.Unauthorized, e.Code);

            // Try to reopen with wrong password (fails):
            options.EncryptionKey = _wrong;
            e = Assert.Throws<CouchbaseLiteException>(() => seekrit = manager.OpenDatabase("seekrit", options),
                "Shouldn't have been able to reopen encrypted db with wrong password");
            Assert.AreEqual(StatusCode.Unauthorized, e.Code);

            // Reopen with correct password:
            options.EncryptionKey = _letmein;
            seekrit = manager.OpenDatabase("seekrit", options);
            Assert.IsNotNull(seekrit, "Failed to reopen encrypted db");
            Assert.AreEqual(1, seekrit.GetDocumentCount());
            seekrit.Dispose();
        }

        [Test]
        public void TestDeleteEncryptedDatabase()
        {
            var options = new DatabaseOptions();
            options.Create = true;
            options.EncryptionKey = _letmein;
            var seekrit = default(Database);
            Assert.DoesNotThrow(() => seekrit = manager.OpenDatabase("seekrit", options),
                "Failed to create encrypted DB");
            CreateDocumentWithProperties(seekrit, new Dictionary<string, object> { { "answer", 42 } });

            // Delete db; this also unregisters its password:
            Assert.DoesNotThrow(seekrit.Delete);

            // Re-create database:
            options.EncryptionKey = null;
            Assert.DoesNotThrow(() => seekrit = manager.OpenDatabase("seekrit", options),
                "Failed to re-create formerly encrypted db");
            Assert.AreEqual(0, seekrit.GetDocumentCount());
            Assert.DoesNotThrow(() => seekrit.Close().Wait(15000));

            // Make sure it doesn't need a password now:
            Assert.DoesNotThrow(() => seekrit = manager.OpenDatabase("seekrit", options),
                "Failed to re-create formerly encrypted db");
            Assert.AreEqual(0, seekrit.GetDocumentCount());
            Assert.DoesNotThrow(() => seekrit.Close().Wait(15000));

            // Make sure old password doesn't work:
            options.EncryptionKey = _letmein;
            var e = Assert.Throws<CouchbaseLiteException>(() => seekrit = manager.OpenDatabase("seekrit", options),
                        "Password opened unencrypted db or unexpected exception occurred!");
            Assert.AreEqual(StatusCode.Unauthorized, e.Code);
        }

        [Test]
        public void TestCompactEncryptedDatabase()
        {
            var options = new DatabaseOptions();
            options.Create = true;
            options.EncryptionKey = _letmein;
            var seekrit = default(Database);
            Assert.DoesNotThrow(() => seekrit = manager.OpenDatabase("seekrit", options),
                "Failed to create encrypted DB");

            // Create a doc and then update it:
            var doc = CreateDocumentWithProperties(seekrit, new Dictionary<string, object> { { "answer", 42 } });
            doc.Update(rev =>
            {
                var props = rev.UserProperties;
                props["foo"] = 84;
                rev.SetUserProperties(props);
                return true;
            });

            // Compact:
            Assert.IsTrue(seekrit.Compact());

            doc.Update(rev =>
            {
                var props = rev.UserProperties;
                props["foo"] = 85;
                rev.SetUserProperties(props);
                return true;
            });

            // Close and re-open:
            Assert.DoesNotThrow(() => seekrit.Close().Wait(15000), "Close failed");
            Assert.DoesNotThrow(() => seekrit = manager.OpenDatabase("seekrit", options), "Failed to reopen encrypted db");
            Assert.AreEqual(1, seekrit.GetDocumentCount());
        }

        [Test]
        public void TestEncryptedAttachments()
        {
            var options = new DatabaseOptions();
            options.Create = true;
            options.EncryptionKey = _letmein;
            var seekrit = default(Database);
            Assert.DoesNotThrow(() => seekrit = manager.OpenDatabase("seekrit", options),
                "Failed to create encrypted DB");

            // Save a doc with an attachment:
            var doc = seekrit.GetDocument("att");
            var body = Encoding.UTF8.GetBytes("This is a test attachment!");
            var rev = doc.CreateRevision();
            rev.SetAttachment("att.txt", "text/plain; charset=utf-8", body);
            var savedRev = rev.Save();
            Assert.IsNotNull(savedRev, "Saving doc failed");

            // Read the raw attachment file and make sure it's not cleartext:
            var digest = savedRev.GetProperty("_attachments").AsDictionary<string, object>().Get("att.txt")
                .AsDictionary<string, object>().GetCast<string>("digest");
            Assert.IsNotNull(digest);
            var attKey = default(BlobKey);
            Assert.DoesNotThrow(() => attKey = new BlobKey(digest));
            var path = seekrit.Attachments.PathForKey(attKey);
            var raw = File.ReadAllBytes(path);
            Assert.IsNotNull(raw);
            Assert.AreNotEqual(raw, body, "Oops, attachment was not encrypted");
            seekrit.Dispose();
        }

        [Test]
        public void TestRekey()
        {
            // First run the encrypted-attachments test to populate the db:
            TestEncryptedAttachments();
            var options = new DatabaseOptions();
            options.Create = true;
            options.EncryptionKey = _letmein;

            var seekrit = default(Database);
            Assert.DoesNotThrow(() => seekrit = manager.OpenDatabase("seekrit", options),
                "Failed to create encrypted DB");
            CreateDocuments(seekrit, 100);
            var view = seekrit.GetView("vu");
            view.SetMap((doc, emit) => { if(doc.ContainsKey("sequence")) { emit(doc["sequence"], null); }}, "1");
            var query = view.CreateQuery();
            Assert.AreEqual(100, query.Run().Count);
            Assert.DoesNotThrow(() => seekrit.ChangeEncryptionKey(_letmeout), "Error changing encryption key");

            // Close & reopen seekrit:
            var dbName = seekrit.Name;
            Assert.DoesNotThrow(() => seekrit.Close().Wait(15000), "Couldn't close seekrit");
            seekrit = null;
            options.EncryptionKey = _letmeout;
            var seekrit2 = default(Database);
            Assert.DoesNotThrow(() => seekrit2 = manager.OpenDatabase(dbName, options));
            seekrit = seekrit2;

            // Check the document and its attachment:
            var savedRev = seekrit.GetDocument("att").CurrentRevision;
            Assert.IsNotNull(savedRev);
            var att = savedRev.GetAttachment("att.txt");
            Assert.IsNotNull(att);
            var body = Encoding.UTF8.GetBytes("This is a test attachment!");
            Assert.AreEqual(body, att.Content);

            view = seekrit.GetExistingView("vu");
            Assert.IsNotNull(view);
            view.SetMap((doc, emit) => { if(doc.ContainsKey("sequence")) { emit(doc["sequence"], null); }}, "1");
            query = view.CreateQuery();
            query.IndexUpdateMode = IndexUpdateMode.Never; // Ensure that the previous results survived

            // Check that the view survived:
            Assert.AreEqual(100, query.Run().Count);
            seekrit.Dispose();
        }  }
}

