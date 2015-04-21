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

using Couchbase.Lite.Listener;
using Couchbase.Lite.Internal;
using System.Linq;
using Couchbase.Lite.Util;
using System.Collections;
using System.Text;
using System.Threading;
using Couchbase.Lite.Views;
using System.Threading.Tasks;
using System.Reflection;

#if NET_3_5
using WebRequest = System.Net.Couchbase.WebRequest;
using HttpWebRequest = System.Net.Couchbase.HttpWebRequest;
using HttpWebResponse = System.Net.Couchbase.HttpWebResponse;
using WebResponse = System.Net.Couchbase.WebResponse;
using WebException = System.Net.Couchbase.WebException;
using WebHeaderCollection = System.Net.Couchbase.WebHeaderCollection;
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

            string etag = null;
            SendRequest("GET", endpoint, null, null, false, r =>
            {
                etag = r.Headers["Etag"];
                Assert.AreEqual(String.Format("\"{0}\"", view.LastSequenceIndexed), etag);
            });
            CheckCacheable(endpoint);

            var nextEndpoint = String.Format("/{0}/doc4", database.Name);
            SendBody("PUT", nextEndpoint, new Body(new Dictionary<string, object> {
                { "message", "aloha" }
            }), HttpStatusCode.Created, null);

            SendRequest("GET", endpoint, new Dictionary<string, string> {
                { "If-None-Match", etag }
            }, null, false, r =>
            {
                Assert.AreEqual(HttpStatusCode.OK, r.StatusCode);
                var parsedResponse = ParseJsonResponse<IDictionary<string, object>>(r);
                Assert.AreEqual(parsedResponse.GetCast<long>("total_rows"), 4L);
            });

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

            //TODO.JHB: Should .NET also implement views so that groups are updated at once like iOS?
            //Assert.IsFalse(database.GetView("design/view").IsStale);
            Assert.IsFalse(database.GetView("design/view2").IsStale);

            //NOTE.JHB: The _rev property differs from iOS.  Should investigate later.
            // Try include_docs
            endpoint += "?include_docs=true&limit=1";
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L }, { "rows", new List<object> {
                        new Dictionary<string, object> {
                            { "id", "doc4" }, { "key", 2L }, { "value", "hi" }, 
                            { "doc", new Dictionary<string, object> {
                                    { "_id", "doc4" },
                                    { "_rev", "1-66d2a05927e5851025c6d204956afff4" },
                                    { "message", "hi" }
                                }
                            }
                        }
                    }
                },
                { "total_rows", 4L }
            });

            // Try include_docs with revs=true
            endpoint += "&revs=true";
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "offset", 0L }, { "rows", new List<object> {
                        new Dictionary<string, object> {
                            { "id", "doc4" }, { "key", 2L }, { "value", "hi" }, 
                            { "doc", new Dictionary<string, object> {
                                    { "_id", "doc4" },
                                    { "_rev", "1-66d2a05927e5851025c6d204956afff4" },
                                    { "_revisions", new Dictionary<string, object> {
                                            { "start", 1L },
                                            { "ids", new List<object> { "66d2a05927e5851025c6d204956afff4" } }
                                        }
                                    },
                                    { "message", "hi" }
                                }
                            }
                        }
                    }
                },
                { "total_rows", 4L }
            });

            View.Compiler = null;
        }

        [Test]
        public void TestNonMappedEndpoints()
        {
            Send("GET", "/", HttpStatusCode.OK, null);
            var response = Send<IDictionary<string, object>>("POST", "/", HttpStatusCode.MethodNotAllowed, null);
            Assert.AreEqual(405, response.GetCast<long>("status"));
            Assert.AreEqual("method_not_allowed", response.GetCast<string>("error"));

            response = Send<IDictionary<string, object>>("PUT", "/", HttpStatusCode.MethodNotAllowed, null);
            Assert.AreEqual(405, response.GetCast<long>("status"));
            Assert.AreEqual("method_not_allowed", response.GetCast<string>("error"));

            string endpoint = String.Format("/{0}/doc1", database.Name);
            response = Send<IDictionary<string, object>>("POST", endpoint, HttpStatusCode.MethodNotAllowed, null);
            Assert.AreEqual(405, response.GetCast<long>("status"));
            Assert.AreEqual("method_not_allowed", response.GetCast<string>("error"));

            endpoint = String.Format("/{0}/_session", database.Name);
            response = Send<IDictionary<string, object>>("GET", endpoint, HttpStatusCode.NotFound, null);
            Assert.AreEqual(404, response.GetCast<long>("status"));
            Assert.AreEqual("not_found", response.GetCast<string>("error"));
        }

        [Test]
        public void TestChanges()
        {
            var revIDs = PopulateDocs();

            // _changes:
            var endpoint = String.Format("/{0}/_changes", database.Name);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "last_seq", 5L },
                { "results", new List<object> {
                        new Dictionary<string, object> {
                            { "id", "doc3" },
                            { "changes", new List<object> {
                                    new Dictionary<string, object> {
                                        { "rev", revIDs[2] }
                                    }
                                }
                            },
                            { "seq", 3L }
                        },
                        new Dictionary<string, object> {
                            { "id", "doc2" },
                            { "changes", new List<object> {
                                    new Dictionary<string, object> {
                                        { "rev", revIDs[1] }
                                    }
                                }
                            },
                            { "seq", 4L }
                        },
                        new Dictionary<string, object> {
                            { "id", "doc1" },
                            { "changes", new List<object> {
                                    new Dictionary<string, object> {
                                        { "rev", revIDs[0] }
                                    }
                                }
                            },
                            { "seq", 5L },
                            { "deleted", true }
                        }
                    }
                }
            });

            CheckCacheable(endpoint);

            // _changes with ?since:
            endpoint += "?since=4";
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "last_seq", 5L }, 
                { "results", new List<object> {
                        new Dictionary<string, object> {
                            { "id", "doc1" }, 
                            { "changes", new List<object> {
                                    new Dictionary<string, object> {
                                        { "rev", revIDs[0] }
                                    }
                                }
                            },
                            { "seq", 5L },
                            { "deleted", true }
                        }
                    }
                }
            });

            endpoint = endpoint.Replace('4', '5');
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "last_seq", 5L }, 
                { "results", new List<object>() }
            });

            // _changes with include_docs:
            endpoint = endpoint.Replace('5', '4') + "&include_docs=true";
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "last_seq", 5L }, 
                { "results", new List<object> {
                        new Dictionary<string, object> {
                            { "id", "doc1" }, 
                            { "changes", new List<object> {
                                    new Dictionary<string, object> {
                                        { "rev", revIDs[0] }
                                    }
                                }
                            },
                            { "seq", 5L },
                            { "deleted", true },
                            { "doc", new Dictionary<string, object> {
                                    { "_id", "doc1" },
                                    { "_rev", revIDs[0] },
                                    { "_deleted", true }
                                }
                            }
                        }
                    }
                }
            });

            // _changes with include_docs and revs=true:
            endpoint += "&revs=true";
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "last_seq", 5L }, 
                { "results", new List<object> {
                        new Dictionary<string, object> {
                            { "id", "doc1" }, 
                            { "changes", new List<object> {
                                    new Dictionary<string, object> {
                                        { "rev", revIDs[0] }
                                    }
                                }
                            },
                            { "seq", 5L },
                            { "deleted", true },
                            { "doc", new Dictionary<string, object> {
                                    { "_id", "doc1" },
                                    { "_rev", revIDs[0] },
                                    { "_revisions", new Dictionary<string, object> {
                                            { "start", 3L },
                                            { "ids", new List<object> { 
                                                    "849ef0cb3227570b179053f30435c834",
                                                    "f4aa17f89f2d613c362f23148b74b794",
                                                    "73f49cb1381a29e8d6d50e840c51cfa5"
                                                } 
                                            }
                                        }
                                    },
                                    { "_deleted", true }
                                }
                            }
                        }
                    }
                }
            });
        }

        [Test]
        public void TestLongPollChanges()
        {
            PopulateDocs();
            var path = String.Format("/{0}/_changes?feed=longpoll&since=5", database.Name);
            var mre = new ManualResetEventSlim();
            byte[] bodyData = null;
            SendRequest("GET", path, null, null, true, r =>
            {
                bodyData = r.GetResponseStream().ReadAllBytes();
                mre.Set();
            });

            Assert.IsFalse(mre.IsSet);

            // Now make a change to the database:
            var endpoint = String.Format("/{0}/doc4", database.Name);
            var result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hej" } 
            }), HttpStatusCode.Created, null);
            var revID6 = result.GetCast<string>("rev");

            // Should now have received a response from the router with one revision:
            Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(5)), "Timed out waiting for response");
            mre.Dispose();
            var body = new Body(bodyData);
            Assert.IsNotNull(body.GetProperties(), "Couldn't parse response body:\n{0}", body.GetJSONString());
            Assert.AreEqual(new Dictionary<string, object> {
                { "last_seq", 6L },
                { "results", new List<object> {
                        new Dictionary<string, object> {
                            { "id", "doc4" },
                            { "changes", new List<object> { 
                                    new Dictionary<string, object> {
                                        { "rev", revID6 }
                                    }
                                } 
                            },
                            { "seq", 6L }
                        }
                    }
                }
            }, ConvertResponse(body.GetProperties()));

            mre.Dispose();
        }

        [Test]
        public void TestLongPollChanges_Heartbeat()
        {
            int heartbeat = 0;
            var mre = new ManualResetEventSlim();
            // Artificially short heartbeat (made possible by SetUp) to speed up the test
            SendRequest("GET", String.Format("/{0}/_changes?feed=longpoll&heartbeat=1000", database.Name), null, null, true,
                r =>
                {
                    var stream = r.GetResponseStream();
                    byte[] data = new byte[4096];
                    while (stream.Read(data, 0, data.Length) > 0)
                    {
                        if(data[0] == 13 && data[1] == 10) {
                            heartbeat += 1;
                        }
                    }

                    mre.Set();
                });

            Assert.IsFalse(mre.IsSet);
            Thread.Sleep(TimeSpan.FromSeconds(2.5));
            Assert.IsFalse(mre.IsSet);
            Assert.AreEqual(2, heartbeat);

            // Now make a change to the database:
            var endpoint = String.Format("/{0}/doc4", database.Name);
            SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hej" } 
            }), HttpStatusCode.Created, null);

            Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(5)), "Timeout waiting for response");
            mre.Dispose();
        }

        [Test]
        public void TestContinuousChanges()
        {
            HttpWebResponse response = null;
            try {
                string endpoint = String.Format("/{0}/doc1", database.Name);
                SendBody("PUT", endpoint, new Body(new Dictionary<string, object> {
                    { "message", "hello" } 
                }), HttpStatusCode.Created, null);


                List<byte> body = new List<byte>();
                var mre = new ManualResetEventSlim();
                SendRequest("GET", String.Format("/{0}/_changes?feed=continuous", database.Name), null, null, true, r =>
                {
                    response = r;
                    var stream = r.GetResponseStream();
                    byte[] data = new byte[4096];
                    while (stream.Read(data, 0, data.Length) > 0)
                    {
                        body.AddRange(data.TakeWhile(x => x != 0));
                    }

                    mre.Set();
                });

                // Should initially have a response and one line of output:
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(body.Count > 0);
                Assert.IsFalse(mre.IsSet);
                body.Clear();

                endpoint = String.Format("/{0}/doc2", database.Name);
                SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                    { "message", "hej" } 
                }), HttpStatusCode.Created, null);

                Thread.Sleep(TimeSpan.FromMilliseconds(500));

                Assert.IsTrue(body.Count > 0);
                Assert.IsFalse(mre.IsSet);

                mre.Dispose();
            } finally {
                if (response != null) {
                    response.Dispose();
                }
            }
        }

        [Test]
        public void TestContinuousChanges_Heartbeat()
        {
            HttpWebResponse response = null;
            try {
                int heartbeat = 0;
                List<byte> body = new List<byte>();
                var mre = new ManualResetEventSlim();

                SendRequest("GET", String.Format("/{0}/_changes?feed=continuous&heartbeat=1000", database.Name), null, null, true, r =>
                {
                    response = r;
                    var stream = response.GetResponseStream();
                    byte[] data = new byte[4096];
                    while (stream.Read(data, 0, data.Length) > 0)
                    {
                        if(data[0] == 13 && data[1] == 10) {
                            heartbeat += 1;
                        }

                        body.AddRange(data.TakeWhile(x => x != 0));
                    }

                    mre.Set();
                });

                Thread.Sleep(2500);
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(body.Count > 0);
                Assert.IsTrue(heartbeat == 2);
                Assert.IsFalse(mre.IsSet);

                mre.Dispose();
            } finally {
                if (response != null) {
                    response.Dispose();
                }
            }
        }

        [Test]
        public void TestChangesBadParam()
        {
            Send("GET", String.Format("/{0}/_changes?feed=continuous&heartbeat=foo", database.Name), HttpStatusCode.BadRequest, null);
            Send("GET", String.Format("/{0}/_changes?feed=continuous&heartbeat=-1", database.Name), HttpStatusCode.BadRequest, null);
            Send("GET", String.Format("/{0}/_changes?feed=continuous&heartbeat=-0", database.Name), HttpStatusCode.BadRequest, null);
            Send("GET", String.Format("/{0}/_changes?feed=longpoll&heartbeat=foo", database.Name), HttpStatusCode.BadRequest, null);
            Send("GET", String.Format("/{0}/_changes?feed=longpoll&heartbeat=-1", database.Name), HttpStatusCode.BadRequest, null);
            Send("GET", String.Format("/{0}/_changes?feed=longpoll&heartbeat=-0", database.Name), HttpStatusCode.BadRequest, null);
        }

        [Test]
        public void TestGetAttachment()
        {
            var attach1 = Encoding.UTF8.GetBytes("This is the body of attach1");
            var attach2 = Encoding.UTF8.GetBytes("This is the body of path/to/attachment");
            var result = CreateDocWithAttachments(attach1, attach2);
            var revID = result.GetCast<string>("rev");

            // Now get the attachment via its URL:
            SendRequest("GET", String.Format("/{0}/doc1/attach", database.Name), null, null, false, r =>
            {
                Assert.IsNotNull(r);
                Assert.AreEqual(HttpStatusCode.OK, r.StatusCode);
                var data = r.GetResponseStream().ReadAllBytes();
                Assert.AreEqual(data, attach1);
                Assert.AreEqual("text/plain", r.ContentType);
                var etag = r.GetResponseHeader("Etag");
                Assert.IsTrue(etag.Length > 0);
            });

            // Ditto the 2nd attachment, whose name contains "/"s:
            SendRequest("GET", String.Format("/{0}/doc1/path/to/attachment", database.Name), null, null, false, r =>
            {
                Assert.IsNotNull(r);
                Assert.AreEqual(HttpStatusCode.OK, r.StatusCode);
                var data = r.GetResponseStream().ReadAllBytes();
                Assert.AreEqual(data, attach2);
                Assert.AreEqual("text/plain", r.ContentType);
                var etag = r.GetResponseHeader("Etag");
                Assert.IsTrue(etag.Length > 0);
            });

            // A nonexistent attachment should result in a NotFound:
            SendRequest("GET", String.Format("/{0}/doc1/bogus", database.Name), null, null, false, r =>
            {
                Assert.IsNotNull(r);
                Assert.AreEqual(HttpStatusCode.NotFound, r.StatusCode);
            });

            SendRequest("GET", String.Format("/{0}/missingdoc/bogus", database.Name), null, null, false, r =>
            {
                Assert.IsNotNull(r);
                Assert.AreEqual(HttpStatusCode.NotFound, r.StatusCode);
            });

            // Get the document with attachment data:
            SendRequest("GET", String.Format("/{0}/doc1?attachments=true", database.Name), null, null, false, r =>
            {
                Assert.IsTrue(r.GetResponseHeader("Content-Type").StartsWith("multipart/related"));
            });

            SendRequest("GET", String.Format("/{0}/doc1?attachments=true", database.Name), new Dictionary<string, string> {
                { "Accept", "application/json" }
            }, null, false, r =>
            {
                Assert.AreEqual(HttpStatusCode.OK, r.StatusCode);
                var body = new Body(r.GetResponseStream().ReadAllBytes());
                var attachments = ConvertResponse(body.GetPropertyForKey("_attachments"));
                Assert.AreEqual(new Dictionary<string, object> { 
                    { "attach", new Dictionary<string, object> {
                            { "data", Convert.ToBase64String(attach1) },
                            { "content_type", "text/plain" },
                            { "length", attach1.Length },
                            { "digest", "sha1-gOHUOBmIMoDCrMuGyaLWzf1hQTE=" },
                            { "revpos", 1L }
                        }
                    }, 
                    { "path/to/attachment", new Dictionary<string, object> {
                            { "data", Convert.ToBase64String(attach2) },
                            { "content_type", "text/plain" },
                            { "length", attach2.Length },
                            { "digest", "sha1-IrXQo0jpePvuKPv5nswnenqsIMc=" },
                            { "revpos", 1L }
                        }
                    }
                }, attachments);
            });

            // Update the document but not the attachments:
            //FIXME.JHB: iOS doesn't need the revpos property present here
            var attachmentsDict = new Dictionary<string, object> {
                { "attach", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "stub", true },
                        { "revpos", 1L }
                    }
                },
                { "path/to/attachment", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "stub", true },
                        { "revpos", 1L }
                    }
                },
            };

            var props = new Dictionary<string, object> {
                { "_rev", revID },
                { "message", "aloha" },
                { "_attachments", attachmentsDict }
            };

            result = SendBody<IDictionary<string, object>>("PUT", String.Format("/{0}/doc1", database.Name), new Body(props),
                HttpStatusCode.Created, null);
            revID = result.GetCast<string>("rev");

            // Get the doc with attachments modified since rev #1:
            var endpoint = String.Format("/{0}/doc1?attachments=true&atts_since=[%22{1}%22]", database.Name, revID);
            Send("GET", endpoint, HttpStatusCode.OK, new Dictionary<string, object> {
                { "_id", "doc1" }, {@"_rev", revID}, {@"message", @"aloha"},
                { "_attachments", new Dictionary<string, object> {
                        { "attach", new Dictionary<string, object> {
                                { "stub", true },
                                { "revpos", 1L }
                            }
                        },
                        { "path/to/attachment", new Dictionary<string, object> {
                                { "stub", true },
                                { "revpos", 1L }
                            }
                        },
                    }
                }
            });
        }

        [Test]
        public void TestGetJSONAttachment()
        {
            // Create a document with two json-like attachments. One with be put as 'text/plain' and
            // the other one will be put as 'application/json'.
            var attach1 = Encoding.UTF8.GetBytes("{\"name\": \"foo\"}");
            var attach2 = Encoding.UTF8.GetBytes("{\"name\": \"bar\"}");

            var base641 = Convert.ToBase64String(attach1);
            var base642 = Convert.ToBase64String(attach2);

            var attachmentsDict = new Dictionary<string, object> {
                { "attach1", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "data", base641 }
                    }
                },
                { "attach2", new Dictionary<string, object> {
                        { "content_type", "application/json" },
                        { "data", base642 }
                    }
                }
            };

            var props = new Dictionary<string, object> {
                { "message", "hello" },
                { "_attachments", attachmentsDict }
            };

            SendBody("PUT", String.Format("/{0}/doc1", database.Name), new Body(props), HttpStatusCode.Created, null);

            // Get the first attachment
            SendRequest("GET", String.Format("/{0}/doc1/attach1", database.Name), null, null, false, r =>
            {
                Assert.AreEqual(HttpStatusCode.OK, r.StatusCode);
                var body = new Body(r.GetResponseStream().ReadAllBytes());
                Assert.AreEqual(attach1, body.GetJson());
                Assert.AreEqual("text/plain", r.GetResponseHeader("Content-Type"));
                var etag = r.GetResponseHeader("Etag");
                Assert.IsTrue(etag.Length > 0);
            });

            // Get the second attachment
            SendRequest("GET", String.Format("/{0}/doc1/attach2", database.Name), null, null, false, r =>
            {
                Assert.AreEqual(HttpStatusCode.OK, r.StatusCode);
                Assert.AreEqual(attach2, r.GetResponseStream().ReadAllBytes());
                Assert.AreEqual("application/json", r.GetResponseHeader("Content-Type"));
                var etag = r.GetResponseHeader("Etag");
                Assert.IsTrue(etag.Length > 0);
            });
        }

        [Test]
        public void TestGetRange()
        {
            var attach1 = Encoding.UTF8.GetBytes("This is the body of attach1");
            var attach2 = Encoding.UTF8.GetBytes("This is the body of path/to/attachment");
            CreateDocWithAttachments(attach1, attach2);

            SendRequest("GET", String.Format("/{0}/doc1/attach", database.Name), new Dictionary<string, string> {
                { "Range", "bytes=5-15" }
            }, null, false, r =>
            {
                Assert.AreEqual(HttpStatusCode.PartialContent, r.StatusCode);
                Assert.AreEqual("bytes 5-15/27", r.GetResponseHeader("Content-Range"));
                Assert.AreEqual(Encoding.UTF8.GetBytes("is the body"), r.GetResponseStream().ReadAllBytes());
            });

            SendRequest("GET", String.Format("/{0}/doc1/attach", database.Name), new Dictionary<string, string> {
                { "Range", "bytes=12-" }
            }, null, false, r =>
            {
                Assert.AreEqual(HttpStatusCode.PartialContent, r.StatusCode);
                Assert.AreEqual("bytes 12-26/27", r.GetResponseHeader("Content-Range"));
                Assert.AreEqual(Encoding.UTF8.GetBytes("body of attach1"), r.GetResponseStream().ReadAllBytes());
            });

            string etag = null;
            SendRequest("GET", String.Format("/{0}/doc1/attach", database.Name), new Dictionary<string, string> {
                { "Range", "bytes=-7" }
            }, null, false, r =>
            {
                Assert.AreEqual(HttpStatusCode.PartialContent, r.StatusCode);
                Assert.AreEqual("bytes 20-26/27", r.GetResponseHeader("Content-Range"));
                Assert.AreEqual(Encoding.UTF8.GetBytes("attach1"), r.GetResponseStream().ReadAllBytes());
                etag = r.GetResponseHeader("Etag");
                Assert.IsTrue(etag.Length > 0);
            });

            SendRequest("GET", String.Format("/{0}/doc1/attach", database.Name), new Dictionary<string, string> {
                { "Range", "bytes=-7" },
                { "If-None-Match", etag }
            }, null, false, r =>
            {
                Assert.AreEqual(HttpStatusCode.NotModified, r.StatusCode);
            });
        }

        [Test]
        public void TestPutMultipart()
        {
            var attachmentDict = new Dictionary<string, object> {
                { "attach", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "length", 36 },
                        { "follows", true }
                    }
                }
            };

            var props = new Dictionary<string, object> {
                { "message", "hello" },
                { "_attachments", attachmentDict }
            };
            const string attachmentstring = "This is the value of the attachment.";
            var body = String.Format("\r\n--BOUNDARY\r\n\r\n" +
                       "{0}" +
                       "\r\n--BOUNDARY\r\n" +
                       "Content-Disposition: attachment; filename=attach\r\n" +
                       "Content-Type: text/plain\r\n\r\n" +
                       "{1}" +
                       "\r\n--BOUNDARY--", Manager.GetObjectMapper().WriteValueAsString(props), attachmentstring);

            SendRequest("PUT", String.Format("/{0}/doc", database.Name), new Dictionary<string, string> {
                { "Content-Type", "multipart/related; boundary=\"BOUNDARY\"" }
            }, new Body(Encoding.UTF8.GetBytes(body)), false, r =>
            {
                Assert.AreEqual(HttpStatusCode.Created, r.StatusCode);
            });
        }

        [Test]
        public void TestOpenRevs()
        {
            // PUT:
            var result = SendBody<IDictionary<string, object>>("PUT", String.Format("/{0}/doc1", database.Name), new Body(new Dictionary<string, object> {
                { "message", "hello" }
            }), HttpStatusCode.Created, null);
            var revID1 = result.GetCast<string>("rev");

            // PUT to update:
            result = SendBody<IDictionary<string, object>>("PUT", String.Format("/{0}/doc1", database.Name), new Body(new Dictionary<string, object> {
                { "message", "goodbye" },
                { "_rev", revID1 }
            }), HttpStatusCode.Created, null);
            var revID2 = result.GetCast<string>("rev");

            Send("GET", String.Format("/{0}/doc1?open_revs=all", database.Name), HttpStatusCode.OK, new List<object> {
                new Dictionary<string, object> { 
                    { "ok", new Dictionary<string, object> {
                            { "_id", "doc1" },
                            { "_rev", revID2 },
                            { "message", "goodbye" }
                        }
                    }
                }
            });

            Send("GET", String.Format("/{0}/doc1?open_revs=[%22{1}%22,%22{2}%22]", database.Name, revID1, revID2), HttpStatusCode.OK, new List<object> {
                new Dictionary<string, object> {
                    { "ok", new Dictionary<string, object> {
                            { "_id", "doc1" },
                            { "_rev", revID1 },
                            { "message", "hello" }
                        }
                    }
                },
                new Dictionary<string, object> {
                    { "ok", new Dictionary<string, object> {
                            { "_id", "doc1" },
                            { "_rev", revID2 },
                            { "message", "goodbye" }
                        }
                    }
                }
            });

            var endpoint = String.Format("/{0}/doc1?open_revs=[%22{1}%22,%22{2}%22]", database.Name, revID1, "666-deadbeef");
            Send("GET", endpoint, HttpStatusCode.OK, new List<object> {
                new Dictionary<string, object> {
                    { "ok", new Dictionary<string, object> {
                            { "_id", "doc1" },
                            { "_rev", revID1 },
                            { "message", "hello" }
                        }
                    }
                },
                new Dictionary<string, object> {
                    { "missing", "666-deadbeef" }
                }
            });

            // We've been forcing JSON, but verify that open_revs defaults to multipart:
            SendRequest("GET", endpoint, null, null, false, r =>
            {
                Assert.IsTrue(r.GetResponseHeader("Content-Type").StartsWith("multipart/mixed"));
            });
        }

        [Test]
        public void TestAccessCheck()
        {
            var url = String.Format("/{0}", database.Name);
            bool calledOnAccessCheck = false;
            _listener._router.OnAccessCheck = (method, endpoint) =>
            {
                Assert.AreEqual(url, endpoint);
                calledOnAccessCheck = true;
                return new Status(StatusCode.Ok);
            };

            Send("GET", url, HttpStatusCode.OK, null);
            Assert.IsTrue(calledOnAccessCheck);

            calledOnAccessCheck = false;
            _listener._router.OnAccessCheck = (method, endpoint) =>
            {
                Assert.AreEqual(url, endpoint);
                calledOnAccessCheck = true;
                return new Status(StatusCode.Forbidden);
            };

            Send("GET", url, HttpStatusCode.Forbidden, null);
            Assert.IsTrue(calledOnAccessCheck);
        }

        [TestFixtureSetUp]
        protected void OneTimeSetUp()
        {
            ServicePointManager.DefaultConnectionLimit = 10;
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
            _listener._router.OnAccessCheck = null;
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
                case "range": SetByteRange(request, value); break;
                default: request.Headers.Set (key, value); break;
            }
        }

        private static void SetByteRange(HttpWebRequest request, string input)
        {
            //HACK Use private API because for some unknown reason, HttpWebRequest
            //does not support the bytes=-# form correctly as defined in W3 and throws
            //an exception when you try to set the header directly
            MethodInfo method = typeof(WebHeaderCollection).GetMethod
                ("AddWithoutValidate", BindingFlags.Instance | BindingFlags.NonPublic);

            method.Invoke(request.Headers, new object[] { "Range", input });
        }

        private void SendRequest(string method, string path, IDictionary<string, string> headers,
            Body bodyObj, bool isAsync, Action<HttpWebResponse> callback)
        {
            SendRequest(method, path, headers, bodyObj, isAsync, false, callback);
        }
            
        private void SendRequest(string method, string path, IDictionary<string, string> headers,
            Body bodyObj, bool isAsync, bool keepAlive, Action<HttpWebResponse> callback)
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
                
            if (isAsync) {
                request.GetResponseAsync().ContinueWith(t =>
                {
                    if(t.IsFaulted) {
                        throw t.Exception;
                    }

                    callback((HttpWebResponse)t.Result);
                    t.Result.Dispose(); //OK to be called multiple times
                });
            } else {
                HttpWebResponse response = null;
                try {
                    response = (HttpWebResponse)request.GetResponse();
                } catch (WebException e) {
                    if (e.Response == null) {
                        if (response != null) {
                            response.Dispose();
                        }
                        throw;
                    } else {
                        Log.D(TAG, "{0} {1} --> {2}", method, path, ((HttpWebResponse)e.Response).StatusCode);
                    }
                    response = (HttpWebResponse)e.Response;
                }

                Assert.IsNotNull(response);
                Log.D(TAG, "{0} {1} --> {2}", method, path, response.StatusCode);

                callback(response);
                if (!keepAlive) {
                    response.Dispose();
                }
            }
        }

        private T ParseJsonResponse<T>(HttpWebResponse response)
        {
            var foo = Encoding.UTF8.GetString(response.GetResponseStream().ReadAllBytes());
            var responseObj = Manager.GetObjectMapper().ReadValue<T>(foo);
            return (T)ConvertResponse(responseObj);
        }

        private object ConvertResponse(object obj) {
            var list = obj.AsList<object>();
            if (list != null) {
                for (int i = 0; i < list.Count; i++) {
                    list[i] = ConvertResponse(list[i]);
                }

                return list;
            } 

            var dictionary = obj.AsDictionary<string, object>();
            if (dictionary != null) {
                var retVal = new Dictionary<string, object>(dictionary.Count);
                foreach (var key in dictionary.Keys) {
                    retVal[(string)key] = ConvertResponse(dictionary[key]);
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
            object result = null;
            SendRequest(method, path, new Dictionary<string, string>{ { "Accept", "application/json" } }, bodyObj,
                false, true, r =>
            {
                _lastResponse = r;
                result = ParseJsonResponse<object>(_lastResponse);

                Assert.IsNotNull(result);
                Assert.AreEqual(expectedStatus, _lastResponse.StatusCode);

                if (expectedResult != null) {
                    Assert.AreEqual(expectedResult, result);
                }
            });

            return result;
        }

        private void CheckCacheable(string path)
        {
            if (_lastResponse == null) {
                return;
            }

            string etag = _lastResponse.Headers["Etag"];
            Assert.IsFalse(String.IsNullOrEmpty(etag), "Missing etag in response for {0}", path);
            SendRequest("GET", path, new Dictionary<string, string> { { "If-None-Match", etag } }, null, 
                false, true, r => 
            {
                _lastResponse = r;
                Assert.AreEqual(HttpStatusCode.NotModified, _lastResponse.StatusCode);
            });

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

            endpoint = endpoint.Replace("doc1", "doc3");
            result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" } 
            }), HttpStatusCode.Created, null);
            var revId2 = result.GetCast<string>("rev");

            endpoint = endpoint.Replace("doc3", "doc2");
            result = SendBody<IDictionary<string, object>>("PUT", endpoint, new Body(new Dictionary<string, object> {
                { "message", "hello" } 
            }), HttpStatusCode.Created, null);
            var revId3 = result.GetCast<string>("rev");

            endpoint = String.Format("/{0}/_all_docs", database.Name);
            result = Send<IDictionary<string, object>>("GET", endpoint, HttpStatusCode.OK, null);
            Assert.AreEqual(3, result.GetCast<long>("total_rows", 0));
            Assert.AreEqual(0, result.GetCast<long>("offset", -1));
            var rows = ConvertResponse(result["rows"]);
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

        private IDictionary<string, object> CreateDocWithAttachments(byte[] attach1, byte[] attach2)
        {
            var attachmentDict = new Dictionary<string, object>(2) {
                { "attach", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "data", Convert.ToBase64String(attach1) }
                    }
                },
                { "path/to/attachment", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "data", Convert.ToBase64String(attach2) }
                    }
                }
            };

            var props = new Dictionary<string, object>(1) {
                { "message", "hello" },
                { "_attachments", attachmentDict }
            };

            return SendBody<IDictionary<string, object>>("PUT", String.Format("/{0}/doc1", database.Name), new Body(props),
                HttpStatusCode.Created, null);
        }
    }
}

