//
//  RouterTests.cs
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
using System.Net;

using NUnit.Framework;

using Couchbase.Lite.PeerToPeer;
using Couchbase.Lite.Internal;
using System.Linq;
using Couchbase.Lite.Util;
using System.Collections;
using System.Text;
using System.Threading;
using Couchbase.Lite.Views;
using Couchbase.Lite.Databases;

#if NET_3_5
using WebRequest = System.Net.Couchbase.WebRequest;
using HttpWebRequest = System.Net.Couchbase.HttpWebRequest;
using HttpWebResponse = System.Net.Couchbase.HttpWebResponse;
using WebException = System.Net.Couchbase.WebException;
#endif

namespace Couchbase.Lite
{
    public class RouterTests : LiteTestCase
    {
        private const string TAG = "RouterTests";

        private int _savedMinHeartbeat;
        private int _minHeartbeat = 5000;
        private CouchbaseLiteServiceListener _listener;
        private HttpWebResponse _lastResponse;

        [Test]
        public void TestServer()
        {
            Send("GET", "/", HttpStatusCode.OK, new Dictionary<string, object> {
                { "CouchbaseLite", "Welcome" },
                { "couchdb", "Welcome" },
                { "version", Manager.VersionString }, 
                { "vendor", new Dictionary<string, object> {
                        { "name",  "Couchbase Lite (C#)" },
                        { "version", Manager.VersionString }
                    }
                }
            });

            Send("GET", "/_all_dbs", HttpStatusCode.OK, new List<object> { database.Name });
            Send("GET", "/non-existent", HttpStatusCode.NotFound, null);
            Send("GET", "/BadId", HttpStatusCode.BadRequest, null);
            var response = Send<IDictionary<string, object>>("PUT", "/", HttpStatusCode.MethodNotAllowed, null);

            Assert.AreEqual(405, response["status"]);
            Assert.AreEqual("method_not_allowed", response["error"]);

            var session = Send<IDictionary<string, object>>("GET", "/_session", HttpStatusCode.OK, null);
            Assert.IsTrue(session.GetCast<bool>("ok"));

            //FIXME:  I honestly have no idea how this is supposed to work
            /*const string sampleAssertion = "eyJhbGciOiJSUzI1NiJ9.eyJwdWJsaWMta2V5Ijp7ImFsZ29yaXRobSI6IkRTIiwieSI6ImNhNWJiYTY" +
                "zZmI4MDQ2OGE0MjFjZjgxYTIzN2VlMDcwYTJlOTM4NTY0ODhiYTYzNTM0ZTU4NzJjZjllMGUwMDk0ZWQ2NDBlOGNhYmEwMjNkYjc5ODU3Yjk" +
                "xMzBlZGNmZGZiNmJiNTUwMWNjNTk3MTI1Y2NiMWQ1ZWQzOTVjZTMyNThlYjEwN2FjZTM1ODRiOWIwN2I4MWU5MDQ4NzhhYzBhMjFlOWZkYmR" +
                "jYzNhNzNjOTg3MDAwYjk4YWUwMmZmMDQ4ODFiZDNiOTBmNzllYzVlNDU1YzliZjM3NzFkYjEzMTcxYjNkMTA2ZjM1ZDQyZmZmZjQ2ZWZiZDc" +
                "wNjgyNWQiLCJwIjoiZmY2MDA0ODNkYjZhYmZjNWI0NWVhYjc4NTk0YjM1MzNkNTUwZDlmMWJmMmE5OTJhN2E4ZGFhNmRjMzRmODA0NWFkNGU" +
                "2ZTBjNDI5ZDMzNGVlZWFhZWZkN2UyM2Q0ODEwYmUwMGU0Y2MxNDkyY2JhMzI1YmE4MWZmMmQ1YTViMzA1YThkMTdlYjNiZjRhMDZhMzQ5ZDM" +
                "5MmUwMGQzMjk3NDRhNTE3OTM4MDM0NGU4MmExOGM0NzkzMzQzOGY4OTFlMjJhZWVmODEyZDY5YzhmNzVlMzI2Y2I3MGVhMDAwYzNmNzc2ZGZ" +
                "kYmQ2MDQ2MzhjMmVmNzE3ZmMyNmQwMmUxNyIsInEiOiJlMjFlMDRmOTExZDFlZDc5OTEwMDhlY2FhYjNiZjc3NTk4NDMwOWMzIiwiZyI6ImM" +
                "1MmE0YTBmZjNiN2U2MWZkZjE4NjdjZTg0MTM4MzY5YTYxNTRmNGFmYTkyOTY2ZTNjODI3ZTI1Y2ZhNmNmNTA4YjkwZTVkZTQxOWUxMzM3ZTA" +
                "3YTJlOWUyYTNjZDVkZWE3MDRkMTc1ZjhlYmY2YWYzOTdkNjllMTEwYjk2YWZiMTdjN2EwMzI1OTMyOWU0ODI5YjBkMDNiYmM3ODk2YjE1YjR" +
                "hZGU1M2UxMzA4NThjYzM0ZDk2MjY5YWE4OTA0MWY0MDkxMzZjNzI0MmEzODg5NWM5ZDViY2NhZDRmMzg5YWYxZDdhNGJkMTM5OGJkMDcyZGZ" +
                "mYTg5NjIzMzM5N2EifSwicHJpbmNpcGFsIjp7ImVtYWlsIjoiamVuc0Btb29zZXlhcmQuY29tIn0sImlhdCI6MTM1ODI5NjIzNzU3NywiZXh" +
                "wIjoxMzU4MzgyNjM3NTc3LCJpc3MiOiJsb2dpbi5wZXJzb25hLm9yZyJ9.RnDK118nqL2wzpLCVRzw1MI4IThgeWpul9jPl6ypyyxRMMTurl" +
                "JbjFfs-BXoPaOem878G8-4D2eGWS6wd307k7xlPysevYPogfFWxK_eDHwkTq3Ts91qEDqrdV_JtgULC8c1LvX65E0TwW_GL_TM94g3CvqoQn" +
                "GVxxoaMVye4ggvR7eOZjimWMzUuu4Lo9Z-VBHBj7XM0UMBie57CpGwH4_Wkv0V_LHZRRHKdnl9ISp_aGwfBObTcHG9v0P3BW9vRrCjihIn0S" +
                "qOJQ9obl52rMf84GD4Lcy9NIktzfyka70xR9Sh7ALotW7rWywsTzMTu3t8AzMz2MJgGjvQmx49QA~eyJhbGciOiJEUzEyOCJ9.eyJleHAiOj" +
                "EzNTgyOTY0Mzg0OTUsImF1ZCI6Imh0dHA6Ly9sb2NhbGhvc3Q6NDk4NC8ifQ.4FV2TrUQffDya0MOxOQlzJQbDNvCPF2sfTIJN7KOLvvlSFP" +
                "knuIo5g";

            var asserted = SendBody<IDictionary<string, object>>("POST", "/_persona_assertion", new Body(new Dictionary<string, object> {
                { "assertion", sampleAssertion }
            }), HttpStatusCode.OK, null);
            Assert.IsTrue((asserted.GetCast<bool>("ok")));
            Assert.AreEqual("jens@mooseyard.com", asserted["email"]);*/
        }

        [Test]
        public void TestDatabases()
        {
            Send<IDictionary<string, object>>("PUT", "/database", HttpStatusCode.Created, null);

            var dbInfo = Send<IDictionary<string, object>>("GET", "/database", HttpStatusCode.OK, null);
            Assert.AreEqual(0, dbInfo.GetCast<long>("doc_count", -1));
            Assert.AreEqual(0, dbInfo.GetCast<long>("update_seq", -1));
            Assert.IsTrue(dbInfo.GetCast<long>("disk_size") > 8000);

            Send("PUT", "/database", HttpStatusCode.PreconditionFailed, null);
            Send("PUT", "/database2", HttpStatusCode.Created, null);
            Send("GET", "/_all_dbs", HttpStatusCode.OK, new List<object> { database.Name, "database", "database2" });
            dbInfo = Send<IDictionary<string, object>>("GET", "/database2", HttpStatusCode.OK, null);
            Assert.AreEqual("database2", dbInfo.GetCast<string>("db_name"));
            Send("DELETE", "/database2", HttpStatusCode.OK, null);
            Send("GET", "/_all_dbs", HttpStatusCode.OK, new List<object> { database.Name, "database" });

            Send("PUT", "/database%2Fwith%2Fslashes", HttpStatusCode.Created, null);
            dbInfo = Send<IDictionary<string, object>>("GET", "/database%2Fwith%2Fslashes", HttpStatusCode.OK, null);
            Assert.AreEqual("database/with/slashes", dbInfo.GetCast<string>("db_name"));
        }

        [Test]
        public void TestDocuments()
        {
            var revIDs = PopulateDocs();
            var endpoint = String.Format("/{0}/doc2", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "_id", "doc2" },
                { "_rev", revIDs[1] },
                { "message", "hello" }
            });
            CheckCacheable(endpoint);
            endpoint += "?revs=true";
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "_id", "doc2" },
                { "_rev", revIDs[1] },
                { "message", "hello" },
                { "_revisions", new Dictionary<string, object> {
                        { "ids", new List<object> { revIDs[1].Substring(2) } },
                        { "start", 1 }
                    }
                }
            });

            endpoint = String.Format("/{0}/doc2?revs_info=true", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "_id", "doc2" },
                { "_rev", revIDs[1] },
                { "message", "hello" },
                { "_revs_info", new List<object> {
                        new Dictionary<string, object> {
                            { "rev", revIDs[1] },
                            { "status", "available" }
                        }
                    }
                }
            });

            endpoint = String.Format("/{0}/doc2?conflicts=true", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "_id", "doc2" },
                { "_rev", revIDs[1] },
                { "message", "hello" }
            });
        }

        [Test]
        public void TestLocalDocs()
        {
            var endpoint = String.Format("/{0}/_local/doc1", database.Name);
            var result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" }
            }), HttpStatusCode.Created, null);

            var revID = result.GetCast<string>("rev");
            Assert.IsTrue(revID.StartsWith("1-"));

            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "_id", "_local/doc1" },
                { "_rev", revID },
                { "message", "hello" }
            });
            CheckCacheable(endpoint);

            Send("GET", String.Format("/{0}/_changes", database.Name), HttpStatusCode.OK, new Dictionary<string, object> {
                { "last_seq", 0 },
                { "results", new List<object>() }
            });
        }

        [Test]
        public void TestAllDocs()
        {
            string endpoint = String.Format("/{0}/doc1", database.Name);
            var result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" } 
            }), HttpStatusCode.Created, null);
            var revId = result.GetCast<string>("rev");

            endpoint = endpoint.Replace("doc1", "doc2");
            result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "bonjour" } 
            }), HttpStatusCode.Created, null);
            var revId2 = result.GetCast<string>("rev");

            endpoint = endpoint.Replace("doc2", "doc3");
            result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "guten tag" } 
            }), HttpStatusCode.Created, null);
            var revId3 = result.GetCast<string>("rev");

            endpoint = String.Format("/{0}/_all_docs?include_docs=true", database.Name);
            result = Send<IDictionary<string, object>>("GET", endpoint, HttpStatusCode.OK, null);
            Assert.AreEqual(3, result.GetCast<long>("total_rows", 0));
            Assert.AreEqual(0, result.GetCast<long>("offset", -1));
            var rows = Convert(result["rows"]);
            var expectedResult = new List<object>();
            expectedResult.Add(new Dictionary<string, object> {
                { "id", "doc1" },
                { "key", "doc1" }, 
                { "value", new Dictionary<string, object> {
                        { "rev", revId }
                    }
                },
                { "doc", new Dictionary<string, object> {
                        { "message", "hello" },
                        { "_id", "doc1" },
                        { "_rev", revId }
                    }
                }
            });

            expectedResult.Add(new Dictionary<string, object> {
                { "id", "doc2" },
                { "key", "doc2" }, 
                { "value", new Dictionary<string, object> {
                        { "rev", revId2 }
                    }
                },
                { "doc", new Dictionary<string, object> {
                        { "message", "bonjour" },
                        { "_id", "doc2" },
                        { "_rev", revId2 }
                    }
                }
            });

            expectedResult.Add(new Dictionary<string, object> {
                { "id", "doc3" },
                { "key", "doc3" }, 
                { "value", new Dictionary<string, object> {
                        { "rev", revId3 }
                    }
                },
                { "doc", new Dictionary<string, object> {
                        { "message", "guten tag" },
                        { "_id", "doc3" },
                        { "_rev", revId3 }
                    }
                }
            });
        }

        [Test]
        public void TestViews()
        {
            string endpoint = String.Format("/{0}/doc1", database.Name);
            SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" } 
            }), HttpStatusCode.Created, null);

            endpoint = endpoint.Replace("doc1", "doc3");
            SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "bonjour" } 
            }), HttpStatusCode.Created, null);

            endpoint = endpoint.Replace("doc3", "doc2");
            SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "guten tag" } 
            }), HttpStatusCode.Created, null);

            var view = database.GetView("design/view");
            view.SetMap((doc, emit) =>
            {
                var message = doc.GetCast<string>("message");
                if(message != null) {
                    emit(message, null);
                }
            }, "1");

            endpoint = String.Format("/{0}/_design/design/_view/view", database.Name);

            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> {
                        new Dictionary<string, object> { { "id", "doc3" }, { "key", "bonjour" } },
                        new Dictionary<string, object> { { "id", "doc2" }, { "key", "guten tag" } },
                        new Dictionary<string, object> { { "id", "doc1" }, { "key", "hello" } }
                    }
                },
                { "total_rows", 3L }
            });

            var response = SendRequest("GET", endpoint, null, null);
            var etag = response.Headers["Etag"];
            Assert.AreEqual(String.Format("\"{0}\"", view.LastSequenceIndexed), etag);
            CheckCacheable(endpoint);

            var nextEndpoint = String.Format("/{0}/doc4", database.Name);
            SendBody("PUT", nextEndpoint, new Body(new Dictionary<string, object> {
                { "message", "aloha" }
            }), HttpStatusCode.Created, null);

            response = SendRequest("GET", endpoint, new Dictionary<string, string> {
                { "If-None-Match", etag }
            }, null);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var parsedResponse = ParseJsonResponse<IDictionary<string, object>>(response);
            Assert.AreEqual(parsedResponse.GetCast<long>("total_rows"), 4L);

            endpoint += "?key=%22bonjour%22";
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> { new Dictionary<string, object> {
                            { "id", "doc3" },
                            { "key", "bonjour" }
                        }
                    }
                },
                { "total_rows", 4L }
            });

            endpoint = String.Format("/{0}/_design/design/_view/view?keys=%5B%22bonjour%22,%22hello%22%5D", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> { 
                        new Dictionary<string, object> {
                            { "id", "doc3" },
                            { "key", "bonjour" }
                        },
                        new Dictionary<string, object> {
                            { "id", "doc1" },
                            { "key", "hello" }
                        }
                    }
                },
                { "total_rows", 4L }
            });
        }

        [Test]
        public void TestViewsStale()
        {
            string endpoint = String.Format("/{0}/doc1", database.Name);
            SendBody("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" } 
            }), HttpStatusCode.Created, null);

            var view = database.GetView("design/view");
            view.SetMap((doc, emit) =>
            {
                var message = doc.GetCast<string>("message");
                if(message != null) {
                    emit(message, null);
                }
            }, "1");

            // No stale (upate_before):
            endpoint = String.Format("/{0}/_design/design/_view/view", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> { new Dictionary<string, object> {
                            { "id", "doc1" },
                            { "key", "hello" }
                        }
                    }
                },
                { "total_rows", 1L }
            });

            // Update database:
            endpoint = String.Format("/{0}/doc2", database.Name);
            SendBody("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "guten tag" } 
            }), HttpStatusCode.Created, null);

            // Stale = ok:
            endpoint = String.Format("/{0}/_design/design/_view/view?stale=ok", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> { new Dictionary<string, object> {
                            { "id", "doc1" },
                            { "key", "hello" }
                        }
                    }
                },
                { "total_rows", 1L }
            });

            // Stale = update_after:
            endpoint = String.Format("/{0}/_design/design/_view/view?stale=update_after", database.Name);
            long prevSequenceIndexed = view.LastSequenceIndexed;
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> { new Dictionary<string, object> {
                            { "id", "doc1" },
                            { "key", "hello" }
                        }
                    }
                },
                { "total_rows", 1L }
            });

            // Check if the current last sequence indexed has been changed:
            Thread.Sleep(5000);
            Assert.IsTrue(prevSequenceIndexed < view.LastSequenceIndexed);

            // Confirm the result with stale = ok:
            endpoint = String.Format("/{0}/_design/design/_view/view?stale=ok", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> { 
                        new Dictionary<string, object> {
                            { "id", "doc2" },
                            { "key", "guten tag" }
                        },
                        new Dictionary<string, object> {
                            { "id", "doc1" },
                            { "key", "hello" }
                        }
                    }
                },
                { "total_rows", 2L }
            });

            // Bad stale value:
            endpoint = String.Format("/{0}/_design/design/_view/view?stale=no", database.Name);
            Send("GET", endpoint, HttpStatusCode.BadRequest, null);
        }

        [Test]
        public void TestJSViews()
        {
            View.Compiler = new JSViewCompiler();
           // Database.FilterCompiler = new JSFilterCompiler();

            string endpoint = String.Format("/{0}/doc1", database.Name);
            SendBody("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" } 
            }), HttpStatusCode.Created, null);

            endpoint = endpoint.Replace("doc1", "doc3");
            SendBody("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "bonjour" } 
            }), HttpStatusCode.Created, null);

            endpoint = endpoint.Replace("doc3", "doc2");
            SendBody("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "guten tag" } 
            }), HttpStatusCode.Created, null);

            endpoint = String.Format("/{0}/_design/design", database.Name);
            SendBody("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "views", new Dictionary<string, object> {
                        { "view", new Dictionary<string, object> {
                                { "map", "function(doc){emit(doc.message, null);}" }
                            }
                        },
                        { "view2", new Dictionary<string, object> {
                                { "map", "function(doc){emit(doc.message.length, doc.message);}" }
                            }
                        }
                    }
                }
            }), HttpStatusCode.Created, null);

            // Query view and check the result:
            endpoint = String.Format("/{0}/_design/design/_view/view", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> {
                        new Dictionary<string, object> { { "id", "doc3" }, { "key", "bonjour" } },
                        new Dictionary<string, object> { { "id", "doc2" }, { "key", "guten tag" } },
                        new Dictionary<string, object> { { "id", "doc1" }, { "key", "hello" } }
                    }
                },
                { "total_rows", 3L }
            });

            // Query view2 and check the result:
            endpoint += "2";
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> {
                        new Dictionary<string, object> { { "id", "doc1" }, { "key", 5L }, { "value", "hello" } },
                        new Dictionary<string, object> { { "id", "doc3" }, { "key", 7L }, { "value", "bonjour" } },
                        new Dictionary<string, object> { { "id", "doc2" }, { "key", 9L }, { "value", "guten tag" } }
                    }
                },
                { "total_rows", 3L }
            });

            ReopenDatabase();

            endpoint = String.Format("/{0}/doc4", database.Name);
            SendBody("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hi" } 
            }), HttpStatusCode.Created, null);


            // Query view2 again
            endpoint = String.Format("/{0}/_design/design/_view/view2", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L },
                { "rows", new List<object> {
                        new Dictionary<string, object> { { "id", "doc4" }, { "key", 2L }, { "value", "hi" } },
                        new Dictionary<string, object> { { "id", "doc1" }, { "key", 5L }, { "value", "hello" } },
                        new Dictionary<string, object> { { "id", "doc3" }, { "key", 7L }, { "value", "bonjour" } },
                        new Dictionary<string, object> { { "id", "doc2" }, { "key", 9L }, { "value", "guten tag" } }
                    }
                },
                { "total_rows", 4L }
            });

            //TODO: Should .NET also implement views so that groups are updated at once like iOS?
            //Assert.IsFalse(database.GetView("design/view").IsStale);
            Assert.IsFalse(database.GetView("design/view2").IsStale);

            // Try include_docs
            endpoint += "?include_docs=true&limit=1";
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L }, { "rows", new List<object> {
                        new Dictionary<string, object> {
                            { "id", "doc4" }, { "key", 2L }, { "value", "hi" }, 
                            { "doc", new Dictionary<string, object> {
                                    { "_id", "doc4" },
                                    { "_rev", "1-cfdd78e822bbcbc25c91e9deb9537c4b" },
                                    { "message", "hi" }
                                }
                            }
                        }
                    }
                },
                { "total_rows", 4L }
            });
        }

        [TestFixtureSetUp]
        protected void OneTimeSetUp()
        {
            ManagerOptions.Default.CallbackScheduler = new SingleTaskThreadpoolScheduler();

            LoadCustomProperties();
            StartCBLite();

            _listener = new CouchbaseLiteServiceListener(manager, 59840);
            _listener.Start();
        }

        [TestFixtureTearDown]
        protected void OneTimeTearDown()
        {
            StopCBLite();
            _listener.Stop();
        }

        [SetUp]
        protected override void SetUp()
        {
            foreach (var name in manager.AllDatabaseNames) {
                var db = manager.GetDatabaseWithoutOpening(name, true);
                db.Delete();
            }

            StartDatabase();
            _savedMinHeartbeat = _minHeartbeat;
            _minHeartbeat = 0;
        }

        [TearDown]
        protected override void TearDown()
        {
            StopDatabase();
            _minHeartbeat = _savedMinHeartbeat;
        }

        private void ReopenDatabase()
        {
            Log.D(TAG, "----- CLOSING DB -----");
            Assert.IsNotNull(database);
            var dbName = database.Name;
            Assert.IsTrue(database.Close(), "Couldn't close DB");
            database = null;

            Log.D(TAG, "----- REOPENING DB -----");
            var db2 = manager.GetDatabase(dbName);
            Assert.IsNotNull(db2, "Couldn't make a new database instance");
            database = db2;
        }

        private static void SetHeader(HttpWebRequest request, string key, string value)
        {
            switch (key.ToLower ()) {
                case "user-agent": request.UserAgent = value; break;
                case "content-length": request.ContentLength = long.Parse (value); break;
                case "content-type": request.ContentType = value; break;
                case "expect": request.Expect = value; break;
                case "referer": request.Referer = value; break;
                case "transfer-encoding": request.TransferEncoding = value; break;
                case "accept": request.Accept = value; break;
                default: request.Headers.Set (key, value); break;
            }
        }

        private HttpWebResponse SendRequest(string method, string path, IDictionary<string, string> headers,
            Body bodyObj)
        {
            headers = headers ?? new Dictionary<string, string>();
            Uri url = null;
            bool validURL = Uri.TryCreate("http://localhost:59840" + path, UriKind.Absolute, out url);
            Assert.IsTrue(validURL, "Invalid URL {0}", path);
            HttpWebRequest request = WebRequest.CreateHttp(url);
            request.Method = method;
            foreach (var header in headers) {
                SetHeader(request, header.Key, header.Value);
            }

            if (bodyObj != null) {
                var array = bodyObj.GetJson().ToArray();
                request.ContentLength = array.Length;
                request.GetRequestStream().Write(array, 0, array.Length);
            } else {
                request.ContentLength = 0;
            }
                
            HttpWebResponse response = null;
            try {
                response = (HttpWebResponse)request.GetResponse();
            } catch(WebException e) {
                Log.D(TAG, "{0} {1} --> {2}", method, path, ((HttpWebResponse)e.Response).StatusCode);
                return (HttpWebResponse)e.Response;
            }

            Assert.IsNotNull(response);
            Log.D(TAG, "{0} {1} --> {2}", method, path, response.StatusCode);

            return response;
        }

        private T ParseJsonResponse<T>(HttpWebResponse response)
        {
            var foo = Encoding.UTF8.GetString(response.GetResponseStream().ReadAllBytes());
            var responseObj = Manager.GetObjectMapper().ReadValue<T>(foo);
            return (T)Convert(responseObj);
        }

        private object Convert(object obj) {
            var list = obj.AsList<object>();
            if (list != null) {
                for (int i = 0; i < list.Count; i++) {
                    list[i] = Convert(list[i]);
                }

                return list;
            } 

            var dictionary = obj.AsDictionary<string, object>();
            if (dictionary != null) {
                var retVal = new Dictionary<string, object>(dictionary.Count);
                foreach (var key in dictionary.Keys) {
                    retVal[(string)key] = Convert(dictionary[key]);
                }

                return retVal;
            }

            return obj;
        }

        private T SendBody<T>(string method, string path, Body bodyObj, HttpStatusCode expectedStatus, T expectedResult)
        {
            return (T)SendBody(method, path, bodyObj, expectedStatus, (object)expectedResult);
        }

        private T Send<T>(string method, string path, HttpStatusCode expectedStatus, T expectedResult)
        {
            return SendBody(method, path, null, expectedStatus, expectedResult);
        }

        private object Send(string method, string path, HttpStatusCode expectedStatus, object expectedResult)
        {
            return SendBody(method, path, null, expectedStatus, expectedResult);
        }

        private object SendBody(string method, string path, Body bodyObj, HttpStatusCode expectedStatus, object expectedResult)
        {
            _lastResponse = SendRequest(method, path, new Dictionary<string, string>{ { "Accept", "application/json" } }, bodyObj);
            var result = ParseJsonResponse<object>(_lastResponse);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedStatus, _lastResponse.StatusCode);

            if (expectedResult != null) {
                Assert.AreEqual(expectedResult, result);
            }

            return result;
        }

        private void CheckCacheable(string path)
        {
            if (_lastResponse == null) {
                return;
            }

            string etag = _lastResponse.Headers["Etag"];
            Assert.IsFalse(String.IsNullOrEmpty(etag), "Missing etag in response for {0}", path);
            _lastResponse = SendRequest("GET", path, new Dictionary<string, string> { { "If-None-Match", etag } }, null);
            Assert.AreEqual(HttpStatusCode.NotModified, _lastResponse.StatusCode);
        }

        private IList<string> PopulateDocs()
        {
            string endpoint = String.Format("/{0}/doc1", database.Name);
            var result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" } 
            }), HttpStatusCode.Created, null);
            var revId = result.GetCast<string>("rev");
            Assert.IsNotNull(revId);
            Assert.IsTrue(revId.StartsWith("1-"));

            result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "goodbye" },
                { "_rev", revId }
            }), HttpStatusCode.Created, null);
            Log.D(TAG, "PUT returned {0}", result);
            revId = result.GetCast<string>("rev");
            Assert.IsNotNull(revId);
            Assert.IsTrue(revId.StartsWith("2-"));

            endpoint = endpoint.Replace("doc1", "doc2");
            result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" } 
            }), HttpStatusCode.Created, null);
            var revId2 = result.GetCast<string>("rev");

            endpoint = endpoint.Replace("doc2", "doc3");
            result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" } 
            }), HttpStatusCode.Created, null);
            var revId3 = result.GetCast<string>("rev");

            endpoint = String.Format("/{0}/_all_docs", database.Name);
            result = Send<IDictionary<string, object>>("GET", endpoint, HttpStatusCode.OK, null);
            Assert.AreEqual(3, result.GetCast<long>("total_rows", 0));
            Assert.AreEqual(0, result.GetCast<long>("offset", -1));
            var rows = Convert(result["rows"]);
            var expectedResult = new List<object>();
            expectedResult.Add(new Dictionary<string, object> {
                { "id", "doc1" },
                { "key", "doc1" }, 
                { "value", new Dictionary<string, object> {
                        { "rev", revId }
                    }
                }
            });

            expectedResult.Add(new Dictionary<string, object> {
                { "id", "doc2" },
                { "key", "doc2" }, 
                { "value", new Dictionary<string, object> {
                        { "rev", revId2 }
                    }
                }
            });

            expectedResult.Add(new Dictionary<string, object> {
                { "id", "doc3" },
                { "key", "doc3" }, 
                { "value", new Dictionary<string, object> {
                        { "rev", revId3 }
                    }
                }
            });
            Assert.AreEqual(expectedResult, rows);
            CheckCacheable(endpoint);

            endpoint = string.Format("/{0}/doc1?rev={1}", database.Name, revId);
            result = Send<IDictionary<string, object>>("DELETE", endpoint, HttpStatusCode.OK, null);
            revId = result.GetCast<string>("rev");
            Assert.IsNotNull(revId);
            Assert.IsTrue(revId.StartsWith("3-"));

            result = Send<IDictionary<string, object>>("GET", String.Format("/{0}/doc1", database.Name), HttpStatusCode.NotFound, null);
            Assert.AreEqual("deleted", result.GetCast<string>("reason"));
            return new List<string> { revId, revId2, revId3 };
        }
    }
}

