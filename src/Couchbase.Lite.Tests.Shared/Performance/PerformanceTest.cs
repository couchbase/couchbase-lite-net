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
using System.Threading;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
    public class PerformanceTest : PerformanceTestCase
    {
        public PerformanceTest()
        {
        }

        [Test]
        public void Test01CreateDocs()
        {
            RunTest("Test01CreateDocs", (parameters) => 
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);
                var docSize = Convert.ToInt32(parameters[DOCSIZE_KEY]);

                var props = CreateTestProperties(docSize);

                var stopwatch = Stopwatch.StartNew();

                database.RunInTransaction(() =>
                {
                    for (var i = 0; i < numDocs; i++)
                    {
                        Document document = database.CreateDocument();
                        document.PutProperties(props);
                    }
                    return true;
                });

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test02CreateDocsUnoptimizedWay()
        {
            RunTest("Test02CreateDocsUnoptimizedWay", (parameters) => 
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);
                var docSize = Convert.ToInt32(parameters[DOCSIZE_KEY]);

                var props = CreateTestProperties(docSize);

                var stopwatch = Stopwatch.StartNew();

                for (var i = 0; i < numDocs; i++)
                {
                    Document document = database.CreateDocument();
                    document.PutProperties(props);
                }

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test03CreateDocsWithAttachments()
        {
            RunTest("Test03CreateDocsWithAttachments", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);
                var docSize = Convert.ToInt32(parameters[DOCSIZE_KEY]);

                var sb = new StringBuilder();
                for (var i = 0; i < docSize; i++)
                {
                    sb.Append('1');
                }
                var bytes = Encoding.ASCII.GetBytes(sb.ToString());

                var props = new Dictionary<string, object>() {
                    { "k", "v" }
                };

                var stopwatch = Stopwatch.StartNew();

                for (var i = 0; i < numDocs; i++)
                {
                    var document = database.CreateDocument();
                    document.PutProperties(props);

                    var unsavedRev = document.CurrentRevision.CreateRevision();
                    unsavedRev.SetAttachment("test_attachment", "text/plain", bytes);
                    var rev = unsavedRev.Save();
                    Assert.IsNotNull(rev);
                }

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test06PullReplication()
        {
            RunTest("Test06PullReplication", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);
                var docSize = Convert.ToInt32(parameters[DOCSIZE_KEY]);

                var props = CreateTestProperties(docSize);
                var docIdTimestamp = DateTime.UtcNow.ToMillisecondsSinceEpoch().ToString();
                for (var i = 0; i < numDocs; i++)
                {
                    var docId = String.Format("doc{0}-{0}", i, docIdTimestamp);
                    AddDocToSyncGateway(docId, new Dictionary<string, object>(props), 
                        "attachment.png", "image/png");
                    Thread.Sleep(1 * 1000);
                }

                var stopwatch = Stopwatch.StartNew();

                var remote = GetReplicationURL();
                var repl = database.CreatePullReplication(remote);
                repl.Continuous = false;
                repl.CreateTarget |= !IsTestingAgainstSyncGateway();

                RunReplication(repl);

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test07PushReplication()
        {
            RunTest("Test07PushReplication", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);
                var docSize = Convert.ToInt32(parameters[DOCSIZE_KEY]);

                var props = CreateTestProperties(docSize);
                var docIdTimestamp = DateTime.UtcNow.ToMillisecondsSinceEpoch().ToString();
                for (var i = 0; i < numDocs; i++)
                {
                    var docId = String.Format("doc{0}-{0}", i, docIdTimestamp);
                    AddDoc(docId, new Dictionary<string, object>(props), 
                        "attachment.png", "image/png");
                }

                var stopwatch = Stopwatch.StartNew();

                var remote = GetReplicationURL();
                var repl = database.CreatePushReplication(remote);
                repl.Continuous = false;
                repl.CreateTarget |= !IsTestingAgainstSyncGateway();

                RunReplication(repl);

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test08DocRevisions()
        {
            RunTest("Test08DocRevisions", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);
                var docSize = Convert.ToInt32(parameters[DOCSIZE_KEY]);

                var docs = new Document[numDocs];
                var props = CreateTestProperties(docSize);
                props["toggle"] = true;

                database.RunInTransaction(() =>
                {
                    for (var i = 0; i < docs.Length; i++)
                    {
                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                        docs[i] = doc;
                    }

                    return true;
                });

                var stopwatch = Stopwatch.StartNew();

                for (var j = 0; j < docs.Length; j++) {
                    Document doc = docs[j];
                    var contents = new Dictionary<string, object>(doc.Properties);
                    contents["toggle"] = !(Boolean)contents["toggle"];
                    doc.PutProperties(contents);
                }

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test09LoadDB()
        {
            RunTest("Test09LoadDB", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);
                var docSize = Convert.ToInt32(parameters[DOCSIZE_KEY]);

                var docs = new Document[numDocs];
                database.RunInTransaction(() =>
                {
                    var props = CreateTestProperties(docSize);
                    for (var i = 0; i < docs.Length; i++)
                    {
                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                        docs[i] = doc;
                    }

                    return true;
                });

                var stopwatch = Stopwatch.StartNew();

                database.Close();
                manager.Close();

                var path = new DirectoryInfo(GetServerPath() + "/tests");
                manager = new Manager(path, Manager.DefaultOptions);
                database = manager.GetDatabase(DefaultTestDb);

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test10DeleteDB()
        {
            RunTest("Test10DeleteDB", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);
                var docSize = Convert.ToInt32(parameters[DOCSIZE_KEY]);

                var docs = new Document[numDocs];
                database.RunInTransaction(() =>
                {
                    var props = CreateTestProperties(docSize);
                    for (var i = 0; i < docs.Length; i++)
                    {
                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                        docs[i] = doc;
                    }

                    return true;
                });

                var stopwatch = Stopwatch.StartNew();

                database.Delete();

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test11DeleteDocs()
        {
            RunTest("Test11DeleteDocs", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);
                var docSize = Convert.ToInt32(parameters[DOCSIZE_KEY]);

                var docs = new Document[numDocs];
                database.RunInTransaction(() =>
                {
                    var props = CreateTestProperties(docSize);
                    for (var i = 0; i < docs.Length; i++)
                    {
                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                        docs[i] = doc;
                    }

                    return true;
                });

                var stopwatch = Stopwatch.StartNew();

                database.RunInTransaction(() =>
                {
                    for (int i = 0; i < docs.Length; i++) {
                        Document doc = docs[i];
                        doc.Delete();
                    }
                    return true;
                });

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test12IndexView()
        {
            RunTest("Test12IndexView", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);

                // Prepare
                var view = database.GetView("vacant");

                view.SetMapReduce((document, emit) =>
                {
                    var vacant = (Boolean)document["vacant"];
                    var name = (String)document["name"];
                    if (vacant && name != null)
                    {
                        emit(name, vacant);
                    }
                }, (keys, values, rereduce) => View.TotalValues(values.ToList()), "1.0");

                database.RunInTransaction(() =>
                {
                    for (var i = 0; i < numDocs; i++)
                    {
                        var name = String.Format("n{0}", i);
                        var vacant = ((i + 2) % 2) == 0;
                        var props = new Dictionary<string, object>()
                        {
                            {"name", name},
                            {"apt", i},
                            {"phone", 408100000 + i},
                            {"vacant", vacant}
                        };

                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                    }

                    return true;
                });

                // Test
                var stopwatch = Stopwatch.StartNew();

                view.UpdateIndex();

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test13QueryView()
        {
            RunTest("Test13QueryView", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);

                // Prepare
                var view = database.GetView("vacant");

                view.SetMapReduce((document, emit) =>
                {
                    var vacant = (Boolean)document["vacant"];
                    var name = (String)document["name"];
                    if (vacant && name != null)
                    {
                        emit(name, vacant);
                    }
                }, (keys, values, rereduce) => View.TotalValues(values.ToList()), "1.0");

                database.RunInTransaction(() =>
                {
                    for (var i = 0; i < numDocs; i++)
                    {
                        var name = String.Format("n{0}", i);
                        var vacant = ((i + 2) % 2) == 0;
                        var props = new Dictionary<string, object>()
                        {
                            {"name", name},
                            {"apt", i},
                            {"phone", 408100000 + i},
                            {"vacant", vacant}
                        };

                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                    }

                    return true;
                });

                // Test
                var stopwatch = Stopwatch.StartNew();

                var query = database.GetView("vacant").CreateQuery();
                query.Descending = false;
                query.MapOnly = true;
                var rows = query.Run();

                foreach(var row in rows)
                {
                    var key = (string)row.Key;
                    var value = (Boolean)row.Value;

                    Assert.IsNotNull(key);
                    Assert.IsTrue(value == true || value == false);
                }

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }

        [Test]
        public void Test14ReduceView()
        {
            RunTest("Test14ReduceView", (parameters) =>
            {
                var numDocs = Convert.ToInt32(parameters[NUMDOCS_KEY]);

                var view = database.GetView("vacant");

                view.SetMapReduce((document, emit) =>
                {
                    var vacant = (Boolean)document["vacant"];
                    var name = (String)document["name"];
                    if (vacant && name != null)
                    {
                        emit(name, vacant);
                    }
                }, (keys, values, rereduce) => View.TotalValues(values.ToList()), "1.0");

                database.RunInTransaction(() =>
                {
                    for (var i = 0; i < numDocs; i++)
                    {
                        var name = String.Format("n{0}", i);
                        var vacant = ((i + 2) % 2) == 0;
                        var props = new Dictionary<string, object>()
                        {
                            {"name", name},
                            {"apt", i},
                            {"phone", 408100000 + i},
                            {"vacant", vacant}
                        };

                        var doc = database.CreateDocument();
                        doc.PutProperties(props);
                    }

                    return true;
                });

                var stopwatch = Stopwatch.StartNew();

                var query = database.GetView("vacant").CreateQuery();
                query.MapOnly = false;
                var rows = query.Run();

                var row = rows.GetRow(0);
                Assert.IsNotNull(row);

                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            });
        }
    }
}
