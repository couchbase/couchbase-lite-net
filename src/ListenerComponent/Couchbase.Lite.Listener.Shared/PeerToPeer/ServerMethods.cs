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
using System.Linq;

using Couchbase.Lite.Replicator;
using System.IO;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Listener
{
    /// <summary>
    /// The basic interface to a Couchbase Lite server for obtaining Couchbase Lite information and 
    /// getting and setting configuration information.
    /// </summary>
    internal static class ServerMethods
    {

        #region Public Methods

        /// <summary>
        /// Returns a JSON structure containing information about the server, including a welcome message and the version of the server.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/server/common.html#get--
        /// </remarks>
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
            var couchResponse = context.CreateResponse();
            couchResponse.JsonBody = body;
            return couchResponse.AsDefaultState();
        }

        /// <summary>
        /// List of running tasks, including the task type, name, status and process ID
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/server/common.html#get--_active_tasks
        /// </remarks>
        public static ICouchbaseResponseState GetActiveTasks(ICouchbaseListenerContext context)
        {
            // Get the current task info of all replicators:
            var activity = new List<object>();
            var replicators = new List<Replication>();
            foreach (var db in context.DbManager.AllOpenDatabases()) {
                var activeReplicators = default(IList<Replication>);
                if(db.ActiveReplicators.AcquireTemp(out activeReplicators)) {
                    foreach(var repl in activeReplicators) {
                        replicators.Add(repl);
                        activity.Add(repl.ActiveTaskInfo);
                    }
                }
            }

            if (context.ChangesFeedMode >= ChangesFeedMode.Continuous) {
                // Continuous activity feed (this is a CBL-specific API):
                var response = context.CreateResponse();
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
                var response = context.CreateResponse();
                response.JsonBody = new Body(activity);
                return response.AsDefaultState();
            }
        }

        /// <summary>
        /// Returns a list of all the databases in the Couchbase Lite instance.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/server/common.html#get--_all_dbs
        /// </remarks>
        public static ICouchbaseResponseState GetAllDbs(ICouchbaseListenerContext context)
        {
            var names = context.DbManager.AllDatabaseNames.Cast<object>().ToList();
            var body = new Body(names);

            var couchResponse = context.CreateResponse();
            couchResponse.JsonBody = body;
            return couchResponse.AsDefaultState();
        }

        /// <summary>
        /// Returns complete information about authenticated user (stubbed, not actual functionality)
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/server/authn.html#get--_session
        /// </remarks>
        public static ICouchbaseResponseState GetSession(ICouchbaseListenerContext context)
        {
            // Even though CouchbaseLite doesn't support user logins, it implements a generic response to the
            // CouchDB _session API, so that apps that call it (such as Futon!) won't barf.
            var couchResponse = context.CreateResponse();
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

        /// <summary>
        /// Requests one or more Universally Unique Identifiers (UUIDs) from the Couchbase Lite instance.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/server/common.html#get--_uuids
        /// </remarks>
        public static ICouchbaseResponseState GetUUIDs(ICouchbaseListenerContext context)
        {
            int count = context.GetQueryParam<int>("count", int.TryParse, 1);
            if (count > 1000) {
                return context.CreateResponse(StatusCode.Forbidden).AsDefaultState();
            }

            var uuidList = new List<object>();
            for (int i = 0; i < count; i++) {
                uuidList.Add(Guid.NewGuid());
            }

            var couchResponse = context.CreateResponse();
            couchResponse.JsonBody = new Body(uuidList);
            return couchResponse.AsDefaultState();
        }

        /// <summary>
        /// Request, configure, or stop, a replication operation.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/server/common.html#post--_replicate
        /// </remarks>
        public static ICouchbaseResponseState ManageReplicationSession(ICouchbaseListenerContext context)
        {
            var body = default(IDictionary<string, object>);
            try {
                byte[] buffer = new byte[context.ContentLength];
                context.BodyStream.Read(buffer, 0, buffer.Length);
                body = new Body(buffer).GetProperties() ?? new Dictionary<string, object>();
            } catch(IOException e) {
                Log.To.Router.E("_replicate", "IOException while reading POST body", e);
                return context.CreateResponse(StatusCode.RequestTimeout).AsDefaultState();
            }

            Replication rep =  context.DbManager.ReplicationWithProperties(body);
            var response = context.CreateResponse();
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

        #endregion
    }
}

