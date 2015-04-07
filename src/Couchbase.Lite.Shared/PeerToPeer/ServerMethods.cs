//
//  ServerMethods.cs
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
using System.Collections.Specialized;
using System.Linq;
using System.Net;

using Couchbase.Lite.Internal;
using Sharpen;
using Couchbase.Lite.Replicator;

namespace Couchbase.Lite.PeerToPeer
{
    internal static class ServerMethods
    {
        public static ICouchbaseResponseState Greeting(ICouchbaseListenerContext context)
        {
            var info = new Dictionary<string, object> {
                { "couchdb", "Welcome" }, //for compatibility
                { "CouchbaseLite", "Welcome" },
                { "version", Manager.VersionString },
                { "vendor", new Dictionary<string, object> {
                        { "name", "Couchbase Lite (C#)" },
                        { "version", Manager.VersionString }
                    }
                }
            };

            var body = new Body(info);
            var couchResponse = new CouchbaseLiteResponse(context);
            couchResponse.JsonBody = body;
            return couchResponse.AsDefaultState();
        }

        public static ICouchbaseResponseState GetActiveTasks(ICouchbaseListenerContext context)
        {
            // http://wiki.apache.org/couchdb/HttpGetActiveTasks

            // Get the current task info of all replicators:
            var activity = new List<object>();
            var replicators = new List<Replication>();
            foreach (var db in context.DbManager.AllOpenDatabases()) {
                foreach (var repl in db.ActiveReplicators) {
                    replicators.Add(repl);
                    activity.Add(repl.ActiveTaskInfo);
                }
            }

            if (context.ChangesFeedMode >= ChangesFeedMode.Continuous) {
                // Continuous activity feed (this is a CBL-specific API):
                var response = new CouchbaseLiteResponse(context);
                response.WriteHeaders();
                response.Chunked = true;

                foreach(var item in activity) {
                    response.SendContinuousLine((IDictionary<string, object>)item, context.ChangesFeedMode);
                }

                return new ReplicationCouchbaseResponseState(replicators) {
                    Response = response,
                    ChangesFeedMode = context.ChangesFeedMode
                };
            } else {
                var response = new CouchbaseLiteResponse(context);
                response.JsonBody = new Body(activity);
                return response.AsDefaultState();
            }
        }

        public static ICouchbaseResponseState GetAllDbs(ICouchbaseListenerContext context)
        {
            var names = context.DbManager.AllDatabaseNames.Cast<object>().ToList();
            var body = new Body(names);

            var couchResponse = new CouchbaseLiteResponse(context);
            couchResponse.JsonBody = body;
            return couchResponse.AsDefaultState();
        }

        public static ICouchbaseResponseState GetSession(ICouchbaseListenerContext context)
        {
            // Even though CouchbaseLite doesn't support user logins, it implements a generic response to the
            // CouchDB _session API, so that apps that call it (such as Futon!) won't barf.
            var couchResponse = new CouchbaseLiteResponse(context);
            couchResponse.JsonBody = new Body(new Dictionary<string, object> {
                { "ok", true },
                { "userCtx", new Dictionary<string, object> {
                        { "name", null },
                        { "roles", new[] { "_admin" } }
                    }
                }
            });

            return couchResponse.AsDefaultState();
        }

        public static ICouchbaseResponseState GetUUIDs(ICouchbaseListenerContext context)
        {
            int count = context.GetQueryParam<int>("count", int.TryParse, 1);
            if (count > 1000) {
                return new CouchbaseLiteResponse(context) { InternalStatus = StatusCode.Forbidden }.AsDefaultState();
            }

            var uuidList = new List<object>();
            for (int i = 0; i < count; i++) {
                uuidList.Add(Guid.NewGuid());
            }

            var couchResponse = new CouchbaseLiteResponse(context);
            couchResponse.JsonBody = new Body(uuidList);
            return couchResponse.AsDefaultState();
        }

        public static ICouchbaseResponseState ManageReplicationSession(ICouchbaseListenerContext context)
        {
            byte[] buffer = new byte[context.ContentLength];
            context.HttpBodyStream.Read(buffer, 0, buffer.Length);
            var body = new Body(buffer).GetProperties() ?? new Dictionary<string, object>();

            Replication rep = null;
            try {
                rep =  context.DbManager.ReplicationWithProperties(body);
            } catch(CouchbaseLiteException e) {
                CouchbaseLiteResponse failResponse = new CouchbaseLiteResponse(context);
                failResponse.InternalStatus = e.GetCBLStatus().GetCode();
                return failResponse.AsDefaultState();
            }

            var response = new CouchbaseLiteResponse(context);
            bool cancel = body.Get("cancel") is bool && (bool)body.Get("cancel");
            if (cancel) {
                if (!rep.IsRunning) {
                    response.InternalStatus = StatusCode.NotFound;
                } else {
                    rep.Stop();
                }
            } else {
                rep.Start();
                if (rep.Continuous || body.GetCast<bool>("async", false)) {
                    response.JsonBody = new Body(new Dictionary<string, object> {
                        { "session_id", rep.sessionID }
                    });
                } else {
                    return new OneShotCouchbaseResponseState(rep) { 
                        Response = response
                    };
                }
            }

            return response.AsDefaultState();
        }
    }
}

