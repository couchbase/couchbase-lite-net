//
//  DatabaseMethods.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Revisions;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Listener
{
    /// <summary>
    /// Methods that operate at the database level
    /// </summary>
    /// <remarks>
    /// http://docs.couchdb.org/en/latest/api/database/index.html
    /// </remarks>
    internal static class DatabaseMethods
    {
        private class WriteChangesContext
        {
            public IEnumerable<RevisionInternal> Changes;
            public long Since;
            public long Limit;
            public bool IncludeConflicts;
            public DBMonitorCouchbaseResponseState ResponseState;
        }

        #region Constants

        private const string TAG = "DatabaseMethods";
        internal static TimeSpan MinHeartbeat = TimeSpan.FromSeconds(5);

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets information about the specified database.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/common.html#get--db
        /// </remarks>
        public static ICouchbaseResponseState GetConfiguration(ICouchbaseListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                int numDocs = db.GetDocumentCount();
                long updateSequence = db.GetLastSequenceNumber();
                if (numDocs < 0 || updateSequence < 0) {
                    return context.CreateResponse(StatusCode.DbError);
                }

                var response = context.CreateResponse();
                response.JsonBody = new Body(new Dictionary<string, object> {
                    { "db_name", db.Name },
                    { "db_uuid", db.PublicUUID() },
                    { "doc_count", numDocs },
                    { "update_seq", updateSequence },
                    { "committed_update_seq", updateSequence },
                    { "purge_seq", 0 }, //TODO: Implement
                    { "disk_size", db.GetTotalDataSize() },
                    { "start_time", db.StartTime * 1000 },
                    { "revs_limit", db.GetMaxRevTreeDepth() }
                });

                return response;
            }).AsDefaultState();
        }

        /// <summary>
        /// Deletes the specified database, and all the documents and attachments contained within it.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/common.html#delete--db
        /// </remarks>
        public static ICouchbaseResponseState DeleteConfiguration(ICouchbaseListenerContext context) 
        {
            return PerformLogicWithDatabase(context, false, db =>
            {
                if(context.GetQueryParam("rev") != null) {
                    // CouchDB checks for this; probably meant to be a document deletion
                    return context.CreateResponse(StatusCode.BadId);
                }

                try {
                    db.Delete();
                } catch (CouchbaseLiteException) {
                    return context.CreateResponse(StatusCode.InternalServerError);
                }

                return context.CreateResponse();
            }).AsDefaultState();
        }

        /// <summary>
        /// Creates a new database.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/common.html#put--db
        /// </remarks>
        public static ICouchbaseResponseState UpdateConfiguration(ICouchbaseListenerContext context)
        {
            string dbName = context.DatabaseName;
            Database db = context.DbManager.GetDatabase(dbName, false);
            if (db != null && db.Exists()) {
                return context.CreateResponse(StatusCode.PreconditionFailed).AsDefaultState();
            }

            try {
                db.Open();
            } catch(CouchbaseLiteException) {
                return context.CreateResponse(StatusCode.Exception).AsDefaultState();
            }

            return context.CreateResponse(StatusCode.Created).AsDefaultState();
        }

        /// <summary>
        /// Returns a JSON structure of all of the documents in a given database.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/bulk-api.html#get--db-_all_docs
        /// </remarks>
        public static ICouchbaseResponseState GetAllDocuments(ICouchbaseListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                if(context.CacheWithEtag(db.GetLastSequenceNumber().ToString())) {
                    return context.CreateResponse(StatusCode.NotModified);
                }

                var options = context.QueryOptions;
                if(options == null) {
                    return context.CreateResponse(StatusCode.BadParam);
                }

                return DoAllDocs(context, db, options);
            }).AsDefaultState();
        }

        /// <summary>
        /// The POST to _all_docs allows to specify multiple keys to be selected from the database. 
        /// This enables you to request multiple documents in a single request, in place of multiple GET /{db}/{docid} requests.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/bulk-api.html#post--db-_all_docs
        /// </remarks>
        public static ICouchbaseResponseState GetAllSpecifiedDocuments(ICouchbaseListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                var options = context.QueryOptions;
                if(options == null) {
                    return context.CreateResponse(StatusCode.BadParam);
                }
                    
                var body = context.BodyAs<Dictionary<string, object>>();
                if(body == null) {
                    return context.CreateResponse(StatusCode.BadJson);
                }

                if(!body.ContainsKey("keys")) {
                    return context.CreateResponse(StatusCode.BadParam);
                }

                var keys = body["keys"].AsList<object>();
                options.Keys = keys;
                return DoAllDocs(context, db, options);
            }).AsDefaultState();
        }

        /// <summary>
        /// Create and update multiple documents at the same time within a single request.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/bulk-api.html#post--db-_bulk_docs
        /// </remarks>
        public static ICouchbaseResponseState ProcessDocumentChangeOperations(ICouchbaseListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                var postBody = context.BodyAs<Dictionary<string, object>>();
                if(postBody == null) {
                    return context.CreateResponse(StatusCode.BadJson);
                }
                    
                if(!postBody.ContainsKey("docs")) {
                    return context.CreateResponse(StatusCode.BadParam);
                }
                var docs = postBody["docs"].AsList<IDictionary<string, object>>();

                bool allOrNothing;
                postBody.TryGetValue<bool>("all_or_nothing", out allOrNothing);

                var newEdits = postBody.GetCast<bool>("new_edits", true);

                var response = context.CreateResponse();
                StatusCode status = StatusCode.Ok;
                bool success = db.RunInTransaction(() => {
                    List<IDictionary<string, object>> results = new List<IDictionary<string, object>>(docs.Count);
                    var castContext = context as ICouchbaseListenerContext2;
                    var source = castContext != null && !castContext.IsLoopbackRequest ? castContext.Sender : null;
                    foreach(var doc in docs) {
                        string docId = doc.CblID();
                        RevisionInternal rev = null;
                        Body body = new Body(doc);

                        if(!newEdits) {
                            if(!RevisionInternal.IsValid(body)) {
                                status = StatusCode.BadParam;
                            } else {
                                rev = new RevisionInternal(body);
                                var history = Database.ParseCouchDBRevisionHistory(doc);
                                try {
                                    Log.To.Router.I(TAG, "Inserting revision {0} from _bulk_docs", rev);
                                    Log.To.Router.V(TAG, "With history {0}", new LogJsonString(history));
                                    db.ForceInsert(rev, history, source);
                                } catch(CouchbaseLiteException e) {
                                    status = e.Code;
                                }
                            } 
                        } else {
                            status = DocumentMethods.UpdateDocument(context, db, docId, body, false, allOrNothing, out rev);
                        }

                        IDictionary<string, object> result = null;
                        if((int)status < 300) {
                            Debug.Assert(rev != null && rev.RevID != null);
                            if(newEdits) {
                                result = new Dictionary<string, object>
                                {
                                    { "id", rev.DocID },
                                    { "rev", rev.RevID },
                                    { "status", (int)status }
                                };
                            }
                        } else if((int)status >= 500) {
                            return false; // abort the whole thing if something goes badly wrong
                        } else if(allOrNothing) {
                            return false; // all_or_nothing backs out if there's any error
                        } else {
                            var info = Status.ToHttpStatus(status);
                            result = new Dictionary<string, object>
                            {
                                { "id", docId },
                                { "error", info.Item2 },
                                { "status", info.Item1 }
                            };
                        }

                        if(result != null) {
                            results.Add(result);
                        }
                    }

                    response.JsonBody = new Body(results.Cast<object>().ToList());
                    return true;
                });

                if(!success) {
                    response.InternalStatus = status;
                }

                return response;
            }).AsDefaultState();
        }

        /// <summary>
        /// Returns a sorted list of changes made to documents in the database, in time order of application.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/changes.html#get--db-_changes
        /// </remarks>
        public static ICouchbaseResponseState GetChanges(ICouchbaseListenerContext context)
        {
            DBMonitorCouchbaseResponseState responseState = new DBMonitorCouchbaseResponseState();

            var responseObject = PerformLogicWithDatabase(context, true, db =>
            {
                var response = context.CreateResponse();
                responseState.Response = response;
                if (context.ChangesFeedMode < ChangesFeedMode.Continuous) {
                    if(context.CacheWithEtag(db.GetLastSequenceNumber().ToString())) {
                        response.InternalStatus = StatusCode.NotModified;
                        return response;
                    }
                }
                    
                var options = ChangesOptions.Default;
                responseState.Db = db;
                responseState.ContentOptions = context.ContentOptions;
                responseState.ChangesFeedMode = context.ChangesFeedMode;
                responseState.ChangesIncludeDocs = context.GetQueryParam<bool>("include_docs", bool.TryParse, false);
                options.IncludeDocs = responseState.ChangesIncludeDocs;
                responseState.ChangesIncludeConflicts = context.GetQueryParam("style") == "all_docs";
                options.IncludeConflicts = responseState.ChangesIncludeConflicts;
                options.ContentOptions = context.ContentOptions;
                options.SortBySequence = !options.IncludeConflicts;
                options.Limit = context.GetQueryParam<int>("limit", int.TryParse, options.Limit);
                int since = context.GetQueryParam<int>("since", int.TryParse, 0);

                string filterName = context.GetQueryParam("filter");
                if(filterName != null) {
                    Status status = new Status();
                    responseState.ChangesFilter = db.GetFilter(filterName, status);
                    if(responseState.ChangesFilter == null) {
                        var code = Database.FilterCompiler == null ? StatusCode.NotImplemented : StatusCode.NotFound;
                        return context.CreateResponse(code);
                    }

                    responseState.FilterParams = context.GetQueryParams();
                }

                string heartbeatParam = context.GetQueryParam("heartbeat");
                int heartbeat = 0;
                if(heartbeatParam != null && (!Int32.TryParse(heartbeatParam, out heartbeat) || heartbeat <= 0)) {
                    return context.CreateResponse(StatusCode.BadParam);
                }

                var changes = db.ChangesSinceStreaming(since, options, responseState.ChangesFilter, responseState.FilterParams);
                if((context.ChangesFeedMode >= ChangesFeedMode.Continuous) ||
                    (context.ChangesFeedMode == ChangesFeedMode.LongPoll && !changes.Any())) {
                    // Response is going to stay open (continuous, or hanging GET):
                    response.Chunked = true;
                    if(context.ChangesFeedMode == ChangesFeedMode.EventSource) {
                        response["Content-Type"] = "text/event-stream; charset=utf-8";
                    }

                    if(context.ChangesFeedMode >= ChangesFeedMode.Continuous) {
                        response.WriteHeaders();
                        foreach(var rev in changes) {
                            var success = response.SendContinuousLine(ChangesDictForRev(rev, responseState), context.ChangesFeedMode);
                            if(!success) {
                                return context.CreateResponse(StatusCode.BadRequest);
                            }
                        }
                    }

                    var subscribed = responseState.SubscribeToDatabase(db, since, options);
                    if(!subscribed) {
                        return response;
                    }

                    if(heartbeat > 0) {
                        var heartbeatSpan = TimeSpan.FromMilliseconds(heartbeat);
                        if(heartbeatSpan < MinHeartbeat) {
                            heartbeatSpan = MinHeartbeat;
                        }

                        string heartbeatResponse = context.ChangesFeedMode == ChangesFeedMode.EventSource ? "\n\n" : "\r\n";
                        responseState.StartHeartbeat(heartbeatResponse, heartbeatSpan);
                    }

                    return response;
                } else {
                    response.Chunked = true;
                    response.Headers["Content-Type"] = "application/json";
                    response.WriteBodyCallback = WriteChangesBodyJson;
                    if(responseState.ChangesIncludeConflicts) {
                        response.WriteBodyContext = new WriteChangesContext {
                            IncludeConflicts = true,
                            Since = since,
                            Limit = options.Limit,
                            Changes = changes,
                            ResponseState = responseState
                        };
                    } else {
                        response.WriteBodyContext = new WriteChangesContext {
                            Since = since,
                            Changes = changes,
                            ResponseState = responseState
                        };
                    }
                }

                return response;
            });

            responseState.Response = responseObject;
            return responseState;
        }

        public static ICouchbaseResponseState GetChangesPost(ICouchbaseListenerContext context)
        {
            DBMonitorCouchbaseResponseState responseState = new DBMonitorCouchbaseResponseState();

            var responseObject = PerformLogicWithDatabase(context, true, db =>
            {
                var response = context.CreateResponse();
                responseState.Response = response;
                var body = context.BodyAs<Dictionary<string, object>>();
                ProcessBody(body);
                if (body.GetCast<ChangesFeedMode>("feed") < ChangesFeedMode.Continuous) {
                    if(context.CacheWithEtag(db.GetLastSequenceNumber().ToString())) {
                        response.InternalStatus = StatusCode.NotModified;
                        return response;
                    }
                }

                var options = ChangesOptions.Default;
                responseState.Db = db;
                responseState.ContentOptions = body.GetCast<DocumentContentOptions>("content_options");
                responseState.ChangesFeedMode = body.GetCast<ChangesFeedMode>("feed");
                responseState.ChangesIncludeDocs = body.GetCast<bool>("include_docs");
                options.IncludeDocs = responseState.ChangesIncludeDocs;
                responseState.ChangesIncludeConflicts = body.GetCast<string>("style") == "all_docs";
                options.IncludeConflicts = responseState.ChangesIncludeConflicts;
                options.ContentOptions = responseState.ContentOptions;
                options.SortBySequence = !options.IncludeConflicts;
                options.Limit = body.GetCast<int>("limit", options.Limit);
                int since = body.GetCast<int>("since");

                string filterName = body.GetCast<string>("filter");
                if(filterName != null) {
                    Status status = new Status();
                    responseState.ChangesFilter = db.GetFilter(filterName, status);
                    if(responseState.ChangesFilter == null) {
                        return context.CreateResponse(status.Code);
                    }

                    responseState.FilterParams = context.GetQueryParams();
                }


                if(responseState.ChangesFeedMode >= ChangesFeedMode.LongPoll) {
                    // Response is going to stay open (continuous, or hanging GET):
                    response.Chunked = true;
                    if(responseState.ChangesFeedMode == ChangesFeedMode.EventSource) {
                        response["Content-Type"] = "text/event-stream; charset=utf-8";
                    }

                    response.WriteHeaders();
                    if(responseState.SubscribeToDatabase(db, since, options)) {
                        int heartbeat = body.GetCast<int>("heartbeat", Int32.MinValue);
                        if(heartbeat != Int32.MinValue) {
                            if(heartbeat <= 0) {
                                responseState.IsAsync = false;
                                return context.CreateResponse(StatusCode.BadParam);
                            }

                            heartbeat = Math.Max(heartbeat, (int)MinHeartbeat.TotalMilliseconds);
                            string heartbeatResponse = context.ChangesFeedMode == ChangesFeedMode.EventSource ? "\n\n" : "\r\n";
                            responseState.StartHeartbeat(heartbeatResponse, TimeSpan.FromMilliseconds(heartbeat));
                        }
                    }

                    return responseState.Response;
                } else {
                    var changes = db.ChangesSinceStreaming(since, options, responseState.ChangesFilter, responseState.FilterParams);
                    response.Chunked = true;
                    response.Headers["Content-Type"] = "application/json";
                    response.WriteBodyCallback = WriteChangesBodyJson;
                    if(responseState.ChangesIncludeConflicts) {
                        response.WriteBodyContext = new WriteChangesContext {
                            IncludeConflicts = true,
                            Since = since,
                            Limit = options.Limit,
                            Changes = changes,
                            ResponseState = responseState
                        };
                    } else {
                        response.WriteBodyContext = new WriteChangesContext {
                            Since = since,
                            Changes = changes,
                            ResponseState = responseState
                        };
                    }

                    return response;
                }
            });

            responseState.Response = responseObject;
            return responseState;
        }
         
        /// <summary>
        /// Request compaction of the specified database. Compaction compresses the disk database file.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/compact.html#post--db-_compact
        /// </remarks>
        public static ICouchbaseResponseState Compact(ICouchbaseListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                try {
                    db.Compact();
                    return context.CreateResponse(StatusCode.Accepted);
                } catch (CouchbaseLiteException) {
                    return context.CreateResponse(StatusCode.DbError);
                }
            }).AsDefaultState();
        }

        /// <summary>
        /// A database purge permanently removes the references to deleted documents from the database.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/misc.html#post--db-_purge
        /// </remarks>
        public static ICouchbaseResponseState Purge(ICouchbaseListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                var body = context.BodyAs<Dictionary<string, IList<string>>>();
                if(body == null) {
                    return context.CreateResponse(StatusCode.BadJson);
                }

                var purgedRevisions = db.Storage.PurgeRevisions(body);
                if(purgedRevisions == null) {
                    return context.CreateResponse(StatusCode.DbError);
                }

                var responseBody = new Body(new Dictionary<string, object>
                {
                    { "purged", purgedRevisions }
                });

                var retVal = context.CreateResponse();
                retVal.JsonBody = responseBody;
                return retVal;
            }).AsDefaultState();
        }

        /// <summary>
        /// Creates (and executes) a temporary view based on the view function supplied in the JSON request.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/temp-views.html#post--db-_temp_view
        /// </remarks>
        public static ICouchbaseResponseState ExecuteTemporaryViewFunction(ICouchbaseListenerContext context)
        {
            var response = context.CreateResponse();
            if (context.RequestHeaders["Content-Type"] == null || 
                !context.RequestHeaders["Content-Type"].StartsWith("application/json")) {
                response.InternalStatus = StatusCode.UnsupportedType;
                return response.AsDefaultState();
            }

            IEnumerable<byte> json = context.BodyStream.ReadAllBytes();
            var requestBody = new Body(json);
            if (!requestBody.IsValidJSON()) {
                response.InternalStatus = StatusCode.BadJson;
                return response.AsDefaultState();
            }

            var props = requestBody.GetProperties();
            if (props == null) {
                response.InternalStatus = StatusCode.BadJson;
                return response.AsDefaultState();
            }

            var options = context.QueryOptions;
            if (options == null) {
                response.InternalStatus = StatusCode.BadRequest;
                return response.AsDefaultState();
            }

            return PerformLogicWithDatabase(context, true, db =>
            {
                if (context.CacheWithEtag(db.GetLastSequenceNumber().ToString())) {
                    response.InternalStatus = StatusCode.NotModified;
                    return response;
                }

                var view = db.GetView("@@TEMPVIEW@@");
                var status = view.Compile(props, "javascript");
                if(status.IsError) {
                    response.InternalStatus = status.Code;
                    return response;
                }

                try {
                    view.UpdateIndex_Internal();
                    return QueryView(context, null, view, options);
                } catch(CouchbaseLiteException e) {
                    response.InternalStatus = e.CBLStatus.Code;
                }

                return response;
            }).AsDefaultState();

        }

        /// <summary>
        /// Performs the given logic with the specified database
        /// </summary>
        /// <returns>The result (in terms of response to the client) of the database operation</returns>
        /// <param name="context">The Couchbase Lite HTTP context</param>
        /// <param name="open">Whether or not to open the database, or just find it</param>
        /// <param name="action">The logic to perform on the database</param>
        public static CouchbaseLiteResponse PerformLogicWithDatabase(ICouchbaseListenerContext context, bool open, 
            Func<Database, CouchbaseLiteResponse> action) 
        {
            string dbName = context.DatabaseName;
            Database db = context.DbManager.GetDatabase(dbName, true);
            if (db == null) {
                return context.CreateResponse(StatusCode.NotFound);
            }

            if (open) {
                try {
                    db.Open();
                } catch(CouchbaseLiteException e) {
                    Log.To.Listener.W(TAG, "Exception in PerformLogicWithDatabase, returning 500", e);
                    return context.CreateResponse(StatusCode.DbError);
                } catch(Exception e) {
                    Log.To.Listener.E(TAG, "Exception in PerformLogicWithDatabase, returning 500", e);
                    return context.CreateResponse(StatusCode.DbError);
                }
            }

            return action(db);
        }

        /// <summary>
        /// Create a response body for an HTTP response from a given list of DB changes (no conflicts)
        /// </summary>
        /// <returns>The response body</returns>
        /// <param name="changes">The list of changes to be processed</param>
        /// <param name="since">The first change ID to be processed</param>
        /// <param name="responseState">The current response state</param>
        public static IDictionary<string, object> ResponseBodyForChanges(RevisionList changes, long since, DBMonitorCouchbaseResponseState responseState)
        {
            List<IDictionary<string, object>> results = new List<IDictionary<string, object>>();
            foreach(var change in changes) {
                results.Add(DatabaseMethods.ChangesDictForRev(change, responseState));
            }

            if(changes.Count > 0) {
                since = changes.Last().Sequence;
            }

            return new Dictionary<string, object> {
                { "results", results },
                { "last_seq", since }
            };
        }

        //Create a response body for an HTTP response from a given list of DB changes, including all conflicts
        internal static IDictionary<string, object> ResponseBodyForChanges(RevisionList changes, long since, int limit, DBMonitorCouchbaseResponseState state)
        {
            string lastDocId = null;
            IDictionary<string, object> lastEntry = null;
            var entries = new List<IDictionary<string, object>>();
            foreach(var rev in changes) {
                string docId = rev.DocID;
                if(docId.Equals(lastDocId)) {
                    ((IList)lastEntry["changes"]).Add(new Dictionary<string, object> { { "rev", rev.RevID } });
                } else {
                    lastEntry = ChangesDictForRev(rev, state);
                    entries.Add(lastEntry);
                    lastDocId = docId;
                }
            }

            entries.Sort((x, y) => (int)((long)x["seq"] - (long)y["seq"]));
            if(entries.Count > limit) {
                entries.RemoveRange(limit, entries.Count - limit);
            }

            long lastSequence = entries.Any() ? (long)entries.Last()["seq"] : since;
            return new Dictionary<string, object> {
                { "results", entries },
                { "last_seq", lastSequence }
            };
        }

        public static void WriteChangesBodyJson(CouchbaseLiteResponse response, object context)
        {
            var c = (WriteChangesContext)context;
            response.WriteData(Encoding.UTF8.GetBytes("{\"results\":["), false);
            var since = c.Since;
            bool first = true;
            var comma = new[] { (byte)',' };
            if(!c.IncludeConflicts) {
                foreach(var change in c.Changes) {
                    if(!first) {
                        response.WriteData(comma, false);
                    }

                    first = false;
                    var dictToWrite = ChangesDictForRev(change, c.ResponseState);
                    response.WriteData(Manager.GetObjectMapper().WriteValueAsBytes(dictToWrite), false);
                    since = change.Sequence;
                }
            } else {
                string lastDocId = null;
                IDictionary<string, object> lastEntry = null;
                var entries = new List<IDictionary<string, object>>();
                foreach(var rev in c.Changes) {
                    string docId = rev.DocID;
                    if(docId.Equals(lastDocId)) {
                        ((IList)lastEntry["changes"]).Add(new Dictionary<string, object> { { "rev", rev.RevID } });
                    } else {
                        if(lastEntry != null) {
                            if(!first) {
                                response.WriteData(comma, false);
                            }

                            first = false;
                            response.WriteData(Manager.GetObjectMapper().WriteValueAsBytes(lastEntry), false);
                        }

                        lastEntry = ChangesDictForRev(rev, c.ResponseState);
                        since = rev.Sequence;
                        lastDocId = docId;
                    }
                }

                if(lastEntry != null) {
                    if(!first) {
                        response.WriteData(comma, false);
                    }

                    first = false;
                    response.WriteData(Manager.GetObjectMapper().WriteValueAsBytes(lastEntry), false);
                }
            }

            var finalBytes = Encoding.UTF8.GetBytes($"],\"last_seq\":{since}}}");
            response.WriteData(finalBytes, false);
        }

        /// <summary>
        /// Creates a dictionary of metadata for one specific revision
        /// </summary>
        /// <returns>The metadata dictionary</returns>
        /// <param name="rev">The revision to examine</param>
        /// <param name="responseState">The current response state</param>
        public static IDictionary<string, object> ChangesDictForRev(RevisionInternal rev, DBMonitorCouchbaseResponseState responseState)
        {
            if (responseState.ChangesIncludeDocs) {
                var status = new Status();
                var rev2 = DocumentMethods.ApplyOptions(responseState.ContentOptions, rev, responseState.Context, responseState.Db, status);
                if (rev2 != null) {
                    rev2.Sequence = rev.Sequence;
                    rev = rev2;
                }
            }
            return new NonNullDictionary<string, object> {
                { "seq", rev.Sequence },
                { "id", rev.DocID },
                { "changes", new List<object> { 
                        new Dictionary<string, object> { 
                            { "rev", rev.RevID } 
                        } 
                    } 
                },
                { "deleted", rev.Deleted ? (object)true : null },
                { "doc", responseState.ChangesIncludeDocs ? rev.GetProperties() : null }
            };
        }

        /// <summary>
        /// Queries the specified view using the specified options
        /// </summary>
        /// <returns>The HTTP response containing the results of the query</returns>
        /// <param name="context">The request context</param>
        /// <param name="db">The database to run the query in</param>
        /// <param name="view">The view to query</param>
        /// <param name="options">The options to apply to the query</param>
        public static CouchbaseLiteResponse QueryView(ICouchbaseListenerContext context, Database db, View view, QueryOptions options)
        {
            var result = view.QueryWithOptions(options);

            object updateSeq = options.UpdateSeq ? (object)view.LastSequenceIndexed : null;
            var mappedResult = new List<object>();
            foreach (var row in result) {
                row.Database = db;
                var dict = row.AsJSONDictionary();
                if (context.ContentOptions != DocumentContentOptions.None) {
                    var doc = dict.Get("doc").AsDictionary<string, object>();
                    if (doc != null) {
                        // Add content options:
                        RevisionInternal rev = new RevisionInternal(doc);
                        var status = new Status();
                        rev = DocumentMethods.ApplyOptions(context.ContentOptions, rev, context, db, status);
                        if (rev != null) {
                            dict["doc"] = rev.GetProperties();
                        }
                    }
                }

                mappedResult.Add(dict);
            }

            var body = new Body(new NonNullDictionary<string, object> {
                { "rows", mappedResult },
                { "total_rows", view.TotalRows },
                { "offset", options.Skip },
                { "update_seq", updateSeq }
            });

            var retVal = context.CreateResponse();
            retVal.JsonBody = body;
            return retVal;
        }

        public static ICouchbaseResponseState RevsDiff(ICouchbaseListenerContext context)
        {
            // Collect all of the input doc/revision IDs as CBL_Revisions:
            var revs = new RevisionList();
            var body = context.BodyAs<Dictionary<string, object>>();
            if (body == null) {
                return context.CreateResponse(StatusCode.BadJson).AsDefaultState();
            }

            foreach (var docPair in body) {
                var revIDs = docPair.Value.AsList<string>();
                if (revIDs == null) {
                    return context.CreateResponse(StatusCode.BadParam).AsDefaultState();
                }

                foreach (var revID in revIDs) {
                    var rev = new RevisionInternal(docPair.Key, revID.AsRevID(), false);
                    revs.Add(rev);
                }
            }

            return PerformLogicWithDatabase(context, true, db =>
            {
                var response = context.CreateResponse();
                // Look them up, removing the existing ones from revs:
                db.Storage.FindMissingRevisions(revs);

                // Return the missing revs in a somewhat different format:
                IDictionary<string, object> diffs = new Dictionary<string, object>();
                foreach(var rev in revs) {
                    var docId = rev.DocID;
                    IList<RevisionID> missingRevs = null;
                    if(!diffs.ContainsKey(docId)) {
                        missingRevs = new List<RevisionID>();
                        diffs[docId] = new Dictionary<string, IList<RevisionID>> { { "missing", missingRevs } };
                    } else {
                        missingRevs = ((Dictionary<string, IList<RevisionID>>)diffs[docId])["missing"];
                    }

                    missingRevs.Add(rev.RevID);
                }

                // Add the possible ancestors for each missing revision:
                foreach(var docPair in diffs) {
                    IDictionary<string, IList<RevisionID>> docInfo = (IDictionary<string, IList<RevisionID>>)docPair.Value;
                    int maxGen = 0;
                    RevisionID maxRevID = null;
                    foreach(var revId in docInfo["missing"]) {
                        if(revId.Generation > maxGen) {
                            maxGen = revId.Generation;
                            maxRevID = revId;
                        }
                    }

                    var rev = new RevisionInternal(docPair.Key, maxRevID, false);
                    var ancestors = db.Storage.GetPossibleAncestors(rev, 0, ValueTypePtr<bool>.NULL, false)?.ToList();
                    if(ancestors != null && ancestors.Count > 0) {
                        docInfo["possible_ancestors"] = ancestors;
                    }
                }

                response.JsonBody = new Body(diffs);
                return response;
            }).AsDefaultState();

        }

        #endregion

        #region Private Methods

        private static void ProcessBody(IDictionary<string, object> body)
        {
            var feed = body.GetCast<string>("feed");
            if (feed != null) {
                if (feed.Equals("longpoll")) {
                    body["feed"] = ChangesFeedMode.LongPoll;
                } else if (feed.Equals("continuous")) {
                    body["feed"] = ChangesFeedMode.Continuous;
                } else if (feed.Equals("eventsource")) {
                    body["feed"] = ChangesFeedMode.EventSource;
                }
            } else {
                body["feed"] = ChangesFeedMode.Normal;
            }

            var contentOptions = DocumentContentOptions.None;
            if (body.GetCast<bool>("attachments")) {
                contentOptions |= DocumentContentOptions.IncludeAttachments;
            }

            if (body.GetCast<bool>("local_seq")) {
                contentOptions |= DocumentContentOptions.IncludeLocalSeq;
            }

            if (body.GetCast<bool>("conflicts")) {
                contentOptions |= DocumentContentOptions.IncludeConflicts;
            }

            if (body.GetCast<bool>("revs")) {
                contentOptions |= DocumentContentOptions.IncludeRevs;
            }

            if (body.GetCast<bool>("revs_info")) {
                contentOptions |= DocumentContentOptions.IncludeRevsInfo;
            }

            body["content_options"] = contentOptions;
        }
            
        //Do an all document request on the database (i.e. fetch all docs given some options)
        private static CouchbaseLiteResponse DoAllDocs(ICouchbaseListenerContext context, Database db, QueryOptions options)
        {
            var iterator = db.GetAllDocs(options);
            if (iterator == null) {
                return context.CreateResponse(StatusCode.BadJson);
            }
                
            var response = context.CreateResponse();
            var result = (from row in iterator
                select row.AsJSONDictionary()).ToList();
            response.JsonBody = new Body(new NonNullDictionary<string, object> {
                { "rows", result },
                { "total_rows", result.Count },
                { "offset", options.Skip },
                { "update_seq", options.UpdateSeq ? (object)db.GetLastSequenceNumber() : null }
            });
            return response;
        }

        #endregion
    }
}

