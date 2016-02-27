//
// PerformanceTest.cs
//
// Author:
//     Pasin Suriyentrakorn  <pasin@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Couchbase.Lite.Tests;
using Couchbase.Lite.Util;
using Couchbase.Lite.Views;
using NUnit.Framework;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class PerformanceTest : LiteTestCase
    {
        private const string TAG = "PerformanceTest";

        private bool PerformanceTestsEnabled 
        {
            get {
                return Convert.ToBoolean(GetProperty("enabled") as string);
            }
        }

        public PerformanceTest(string storageType) : base(storageType)
        {
        }

        [TestFixtureSetUp]
        protected void InitConfig()
        {
            Log.SetLogger(null);
            var mainProperties = GetAsset("perftest.properties");
            if (mainProperties != null) {
                LoadProperties(mainProperties);
            }
            mainProperties.Close();

            try {
                var localProperties = GetAsset("local-perftest.properties");
                if (localProperties != null) {
                    LoadProperties(localProperties);
                    localProperties.Close();
                }
            } catch (IOException) {
                Console.WriteLine("Error trying to read from local-perftest.properties, does this file exist?");
                throw;
            }
        }

        [TestFixtureTearDown]
        public void FixtureTearDown()
        {
            Log.SetDefaultLogger();
        }

        [Test]
        public void Test01CreateDocs()
        {
            const string TEST_NAME = "test1";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var content = CreateContent(GetSizeOfDocument(TEST_NAME));
            TimeBlock(String.Format("{0}, {1}", GetNumberOfDocuments(TEST_NAME), GetSizeOfDocument(TEST_NAME)), () =>
            {
                var success = database.RunInTransaction(() =>
                {
                    for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                        try {
                            var props = new Dictionary<string, object> {
                                { "content", content }
                            };
                            var doc = database.CreateDocument();
                            doc.PutProperties(props);
                        } catch(CouchbaseLiteException e) {
                            Console.WriteLine("Error creating document", e);
                            return false;
                        }
                    }

                    return true;
                });

                Assert.IsTrue(success);
            });
        }

        [Test]
        public void Test02CreateDocsUnoptimizedWay()
        {
            const string TEST_NAME = "test2";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var content = CreateContent(GetSizeOfDocument(TEST_NAME));
            TimeBlock(String.Format("{0}, {1}", GetNumberOfDocuments(TEST_NAME), GetSizeOfDocument(TEST_NAME)), () =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    var props = new Dictionary<string, object> {
                        { "content", content }
                    };
                    var doc = database.CreateDocument();
                    doc.PutProperties(props);
                }
            });
        }

        [Test]
        public void Test03ReadDocs()
        {
            const string TEST_NAME = "test3";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var docIds = new List<string>();
            var content = CreateContent(GetSizeOfDocument(TEST_NAME));
            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    try {
                        var props = new Dictionary<string, object> {
                            { "content", content }
                        };
                        var doc = database.CreateDocument();
                        Assert.IsNotNull(doc.PutProperties(props));
                        docIds.Add(doc.Id);
                    } catch(CouchbaseLiteException e) {
                        Console.WriteLine("Error creating document", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success);
            TimeBlock(String.Format("{0}, {1}", GetNumberOfDocuments(TEST_NAME), GetSizeOfDocument(TEST_NAME)), () =>
            {
                foreach(var docId in docIds) {
                    var doc = database.GetDocument(docId);
                    Assert.IsNotNull(doc);
                    var properties = doc.Properties;
                    Assert.IsNotNull(properties);
                    Assert.IsNotNull(properties.Get("content"));
                }
            });
        }

        [Test]
        public void Test04CreateAttachment()
        {
            const string TEST_NAME = "test4";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(CreateContent(GetSizeOfAttachment(TEST_NAME)));
            TimeBlock(String.Format("{0}, {1}", GetNumberOfDocuments(TEST_NAME), GetSizeOfAttachment(TEST_NAME)), () =>
            {
                var success = database.RunInTransaction(() =>
                {
                    try {
                        for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                            var doc = database.CreateDocument();
                            var unsaved = doc.CreateRevision();
                            unsaved.SetProperties(new Dictionary<string, object>());
                            unsaved.SetAttachment("attach", "text/plain", bytes);
                            unsaved.Save();
                        }
                    } catch(Exception e) {
                        Console.WriteLine("Document create with attachment failed", e);
                        return false;
                    }

                    return true;
                });

                Assert.IsTrue(success);
            });
        }

        [Test]
        public void Test05ReadAttachments()
        {
            const string TEST_NAME = "test5";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(CreateContent(GetSizeOfAttachment(TEST_NAME)));
            var docs = new Document[GetNumberOfDocuments(TEST_NAME)];
            var success = database.RunInTransaction(() =>
            {
                try {
                    for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                        var properties = new Dictionary<string, object> {
                            { "foo", "bar" }
                        };
                        var doc = database.CreateDocument();
                        var unsaved = doc.CreateRevision();
                        unsaved.SetProperties(properties);
                        unsaved.SetAttachment("attach", "text/plain", bytes);
                        unsaved.Save();

                        docs[i] = doc;
                    }
                } catch(Exception e) {
                    Console.WriteLine("Document create with attachment failed", e);
                    return false;
                }

                return true;
            });

            Assert.IsTrue(success);

            TimeBlock(String.Format("{0}, {1}", GetNumberOfDocuments(TEST_NAME), GetSizeOfAttachment(TEST_NAME)), () =>
            {
                foreach(var doc in docs) {
                    var att = doc.CurrentRevision.GetAttachment("attach");
                    Assert.IsNotNull(att);

                    var gotBytes = att.Content.ToArray();
                    Assert.AreEqual(GetSizeOfAttachment(TEST_NAME), gotBytes.Length);
                    LogPerformanceStats(bytes.Length, "Size");
                }
            });
        }

        [Test]
        public void Test06PushReplication()
        {
            const string TEST_NAME = "test6";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var content = CreateContent(GetSizeOfDocument(TEST_NAME));
            var attSize = GetSizeOfAttachment(TEST_NAME);
            var attachment = default(byte[]);
            if (attSize > 0) {
                attachment = Encoding.UTF8.GetBytes(CreateContent(attSize, 'b'));
            }

            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    try {
                        var properties = new Dictionary<string, object> {
                            { "content", content }
                        };
                        var document = database.CreateDocument();
                        var unsaved = document.CreateRevision();
                        unsaved.SetProperties(properties);
                        if(attachment != null) {
                            unsaved.SetAttachment("attach", "text/plain", attachment);
                        }
                        Assert.IsNotNull(unsaved.Save());
                    } catch(Exception e) {
                        Console.WriteLine("Error creating documents", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success);
            TimeBlock(String.Format("{0}, {1}, {2}", GetNumberOfDocuments(TEST_NAME), GetSizeOfDocument(TEST_NAME), 
                GetSizeOfAttachment(TEST_NAME)), () =>
            {
                var remote = GetReplicationURL();
                var repl = database.CreatePushReplication(remote);
                repl.Continuous = false;
                RunReplication(repl);
            });
        }

        [Test]
        public void Test07PullReplication()
        {
            const string TEST_NAME = "test7";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var content = CreateContent(GetSizeOfDocument(TEST_NAME));
            var attSize = GetSizeOfAttachment(TEST_NAME);
            var attachment = default(byte[]);
            if (attSize > 0) {
                attachment = Encoding.UTF8.GetBytes(CreateContent(attSize, 'b'));
            }

            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    try {
                        var properties = new Dictionary<string, object> {
                            { "content", content }
                        };
                        var document = database.CreateDocument();
                        var unsaved = document.CreateRevision();
                        unsaved.SetProperties(properties);
                        if(attachment != null) {
                            unsaved.SetAttachment("attach", "text/plain", attachment);
                        }
                        Assert.IsNotNull(unsaved.Save());
                    } catch(Exception e) {
                        Console.WriteLine("Error creating documents", e);
                        return false;
                    }
                }

                return true;
            });
            Assert.IsTrue(success, "Transaction failed");

            var remote = GetReplicationURL();
            var repl = database.CreatePushReplication(remote);
            repl.Continuous = false;
            RunReplication(repl);

            Sleep(5000);
            TimeBlock(String.Format("{0}, {1}, {2}", GetNumberOfDocuments(TEST_NAME), GetSizeOfDocument(TEST_NAME), 
                GetSizeOfAttachment(TEST_NAME)), () =>
            {
                repl = database.CreatePullReplication(remote);
                repl.Continuous = false;
                RunReplication(repl);
            });
        }

        [Test]
        public void Test08DocRevisions()
        {
            const string TEST_NAME = "test8";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var docs = new Document[GetNumberOfDocuments(TEST_NAME)];
            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    var props = new Dictionary<string, object> {
                        { "toggle", true }
                    };
                    var doc = database.CreateDocument();
                    try {
                        doc.PutProperties(props);
                        docs[i] = doc;
                    } catch(Exception e) {
                        Console.WriteLine("Document creation failed", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success);
            TimeBlock(String.Format("{0}, {1}", GetNumberOfDocuments(TEST_NAME), GetNumberOfUpdates(TEST_NAME)), () =>
            {
                foreach(var doc in docs) {
                    for(int i = 0; i < GetNumberOfUpdates(TEST_NAME); i++) {
                        var contents = new Dictionary<string, object>(doc.Properties);
                        var wasChecked = contents.GetCast<bool>("toggle");
                        contents["toggle"] = !wasChecked;
                        doc.PutProperties(contents);
                    }
                 }
            });
        }

        [Test]
        public void Test09LoadDB()
        {
            const string TEST_NAME = "test9";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var content = CreateContent(GetSizeOfDocument(TEST_NAME));
            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    try {
                        var props = new Dictionary<string, object> {
                            { "content", content }
                        };
                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                    } catch(CouchbaseLiteException e) {
                        Console.WriteLine("Error creating document", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success);
            TimeBlock(String.Format("{0}, {1}, {2}", GetNumberOfDocuments(TEST_NAME), GetSizeOfDocument(TEST_NAME), 
                GetNumberOfRounds(TEST_NAME)), () =>
            {
                for(int i = 0; i < GetNumberOfRounds(TEST_NAME); i++) {
                    Assert.DoesNotThrow(() => database.Close().Wait(15000));
                    database = manager.GetDatabase(DefaultTestDb);
                    Assert.IsNotNull(database);
                }
            });
        }

        [Test]
        public void Test10DeleteDB()
        {
            const string TEST_NAME = "test10";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var content = CreateContent(GetSizeOfDocument(TEST_NAME));
            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    try {
                        var props = new Dictionary<string, object> {
                            { "content", content }
                        };
                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                    } catch(CouchbaseLiteException e) {
                        Console.WriteLine("Error creating document", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success);
            TimeBlock(String.Format("{0}, {1}", GetNumberOfDocuments(TEST_NAME), GetSizeOfDocument(TEST_NAME)), () =>
            {
                database.Delete();
            });
        }

        [Test]
        public void Test11DeleteDocs()
        {
            const string TEST_NAME = "test11";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var docs = new Document[GetNumberOfDocuments(TEST_NAME)];
            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    var props = new Dictionary<string, object> {
                        { "toggle", true }
                    };
                    var doc = database.CreateDocument();
                    try {
                        doc.PutProperties(props);
                        docs[i] = doc;
                    } catch(CouchbaseLiteException e) {
                        Console.WriteLine("Document creation failed", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success);
            TimeBlock(String.Format("{0}, {1}", GetNumberOfDocuments(TEST_NAME), GetSizeOfDocument(TEST_NAME)), () =>
            {
                success = database.RunInTransaction(() =>
                {
                    foreach(var doc in docs) {
                        try {
                            doc.Delete();
                        } catch(Exception e) {
                            Console.WriteLine("Document delete failed", e);
                            return false;
                        }
                    }

                    return true;
                });

                Assert.IsTrue(success);
            });
        }

        [Test]
        public void Test12IndexView()
        {
            const string TEST_NAME = "test12";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var view = database.GetView("vacant");
            view.SetMapReduce((doc, emit) =>
            {
                var vacant = doc.GetCast<bool>("vacant");
                var name = doc.GetCast<string>("name");
                if(vacant && name != null) {
                    emit(name, vacant);
                }
            }, BuiltinReduceFunctions.Sum, "1.0.0");

            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    var name = String.Format("{0}{1}", "n", i);
                    var vacant = (i % 2) == 0;
                    var props = new Dictionary<string, object> {
                        { "name", name },
                        { "apt", i },
                        { "phone", 408100000 + i },
                        { "vacant", vacant }
                    };

                    var doc = database.CreateDocument();
                    try {
                        doc.PutProperties(props);
                    } catch(CouchbaseLiteException e) {
                        Console.WriteLine("Failed to create doc", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success);
            TimeBlock(GetNumberOfDocuments(TEST_NAME).ToString(), () =>
            {
                view = database.GetView("vacant");
                view.UpdateIndex();
            });
        }

        [Test]
        public void Test13QueryView()
        {
            const string TEST_NAME = "test13";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var view = database.GetView("vacant");
            view.SetMapReduce((doc, emit) =>
            {
                var vacant = doc.GetCast<bool>("vacant");
                var name = doc.GetCast<string>("name");
                if(vacant && name != null) {
                    emit(name, vacant);
                }
            }, BuiltinReduceFunctions.Sum, "1.0.0");

            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    var name = String.Format("{0}{1}", "n", i);
                    var vacant = (i % 2) == 0;
                    var props = new Dictionary<string, object> {
                        { "name", name },
                        { "apt", i },
                        { "phone", 408100000 + i },
                        { "vacant", vacant }
                    };

                    var doc = database.CreateDocument();
                    try {
                        doc.PutProperties(props);
                    } catch(CouchbaseLiteException e) {
                        Console.WriteLine("Failed to create doc", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success);
            view.UpdateIndex();

            TimeBlock(GetNumberOfDocuments(TEST_NAME).ToString(), () =>
            {
                var query = view.CreateQuery();
                query.Descending = false;
                query.MapOnly = true;
                foreach(var row in query.Run()) {
                    Assert.IsNotNull(row.Key);
                    Assert.IsNotNull(row.Value);
                }
            });
        }

        [Test]
        public void Test14ReduceView()
        {
            const string TEST_NAME = "test14";
            if (!PerformanceTestsEnabled) {
                return;
            }

            var view = database.GetView("vacant");
            view.SetMapReduce((doc, emit) =>
            {
                var vacant = doc.GetCast<bool>("vacant");
                var name = doc.GetCast<string>("name");
                if(vacant && name != null) {
                    emit(name, vacant);
                }
            }, BuiltinReduceFunctions.Sum, "1.0.0");

            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    var name = String.Format("{0}{1}", "n", i);
                    var vacant = (i % 2) == 0;
                    var props = new Dictionary<string, object> {
                        { "name", name },
                        { "apt", i },
                        { "phone", 408100000 + i },
                        { "vacant", vacant }
                    };

                    var doc = database.CreateDocument();
                    try {
                        doc.PutProperties(props);
                    } catch(CouchbaseLiteException e) {
                        Console.WriteLine("Failed to create doc", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success);
            view.UpdateIndex();
            TimeBlock(GetNumberOfDocuments(TEST_NAME).ToString(), () =>
            {
                var query = view.CreateQuery();
                query.MapOnly = false;
                var rowEnum = query.Run();
                var row = default(QueryRow);
                Assert.DoesNotThrow(() => row =rowEnum.ElementAt(0));
                Assert.IsNotNull(row.Value);
            });
        }

        [Test]
        public void Test15AllDocsQuery()
        {
            const string TEST_NAME = "test15";
            if (!PerformanceTestsEnabled) {
                return;
            }
            var content = CreateContent(GetSizeOfDocument(TEST_NAME));

            var success = database.RunInTransaction(() =>
            {
                for(int i = 0; i < GetNumberOfDocuments(TEST_NAME); i++) {
                    try {
                        var props = new Dictionary<string, object> {
                            { "content", content }
                        };
                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                    } catch(CouchbaseLiteException e) {
                        Console.WriteLine("Error when creating document", e);
                        return false;
                    }
                }

                return true;
            });

            Assert.IsTrue(success, "Failed to create documents");
            TimeBlock(String.Format("{0}, {1}", GetNumberOfDocuments(TEST_NAME), GetSizeOfDocument(TEST_NAME)), () =>
            {
                var query = database.CreateAllDocumentsQuery();
                query.AllDocsMode = AllDocsMode.AllDocs;
                foreach(var row in query.Run()) {
                    Assert.IsNotNull(row.Document);
                }
            });
        }

        private int GetSizeOfDocument(string testName)
        {
            return Convert.ToInt32(GetProperty(testName + ".sizeOfDocument"));
        }

        private int GetNumberOfDocuments(string testName)
        {
            return Convert.ToInt32(GetProperty(testName + ".numberOfDocuments"));
        }

        private int GetSizeOfAttachment(string testName)
        {
            return Convert.ToInt32(GetProperty(testName + ".sizeOfAttachment"));
        }

        private int GetNumberOfUpdates(string testName)
        {
            return Convert.ToInt32(GetProperty(testName + ".numberOfUpdates"));
        }

        private int GetNumberOfRounds(string testName)
        {
            return Convert.ToInt32(GetProperty(testName + ".numberOfRounds"));
        }

        private void TimeBlock(string comment, Action block)
        {
            var sw = Stopwatch.StartNew();
            block();
            sw.Stop();
            LogPerformanceStats(sw.ElapsedMilliseconds, comment);
        }

        private void LogPerformanceStats(long time, string comment)
        {
            Console.Out.WriteLine("PerformanceStats: {0} msec {1}", time, comment != null ? "(" + comment + ")" : String.Empty);
        }

        private string CreateContent(int size, char fill = 'a')
        {
            var chars = Enumerable.Repeat(fill, size).ToArray();
            return new string(chars, 0, chars.Length);
        }
    }
}
