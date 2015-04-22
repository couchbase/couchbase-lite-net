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

using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
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

        #region Constants

        private const string TAG = "DatabaseMethods";
        private const int MIN_HEARTBEAT = 5000; //NOTE: iOS uses seconds but .NET uses milliseconds

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets information about the specified database.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/database/common.html#get--db
        /// <remarks>
        public static ICouchbaseResponseState GetConfiguration(ICouchbaseListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                int numDocs = db.DocumentCount;
                long updateSequence = db.LastSequenceNumber;
                if (numDocs < 0 || updateSequence < 0) {
                    return context.CreateResponse(StatusCode.DbError);
                }

                var response = context.CreateResponse();
                response.JsonBody = new Body(new Dictionary<string, object> {
                    { "db_name", db.Name },
                    { "doc_count", numDocs },
                    { "update_seq", updateSequence },
                    { "committed_update_seq", updateSequence },
                    { "purge_seq", 0 }, //TODO: Implement
                    { "disk_size", db.TotalDataSize },
                    { "start_time", db.StartTime * 1000 }
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
        /// <remarks>
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
        /// <remarks>
        public static ICouchbaseResponseState UpdateConfiguration(ICouchbaseListenerContext context)
        {
            string dbName = context.DatabaseName;
            Database db = context.DbManager.GetDatabaseWithoutOpening(dbName, false);
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
        /// <remarks>
        public static ICouchbaseResponseState GetAllDocuments(ICouchbaseListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                if(context.CacheWithEtag(db.LastSequenceNumber.ToString())) {
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
        /// <remarks>
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

                if(!body.ContainsKey("rows")) {
                    return context.CreateResponse(StatusCode.BadParam);
                }

                var keys = body["rows"].AsList<object>();
                options.SetKeys(keys);
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
        /// <remarks>
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

                bool newEdits;
                postBody.TryGetValue<bool>("new_edits", out newEdits);

                var response = context.CreateResponse();
                StatusCode status = StatusCode.Ok;
                bool success = db.RunInTransaction(() => {
                    List<IDictionary<string, object>> results = new List<IDictionary<string, object>>(docs.Count);
                    foreach(var doc in docs) {
                        string docId = doc.GetCast<string>("_id");
                        RevisionInternal rev = null;
                        Body body = new Body(doc);

                        if(!newEdits) {
                            if(!RevisionInternal.IsValid(body)) {
                                status = StatusCode.BadParam;
                            } else {
                                rev = new RevisionInternal(body);
                                var history = Database.ParseCouchDBRevisionHistory(doc);
                                try {
                                    db.ForceInsert(rev, history, null);
                                } catch(CouchbaseLiteException e) {
                                    status = e.Code;
                                }
                            } 
                        } else {
                            status = DocumentMethods.UpdateDocument(context, db, docId, body, false, allOrNothing, out rev);
                        }

                        IDictionary<string, object> result = null;
                        if((int)status < 300) {
                            Debug.Assert(rev != null && rev.GetRevId() != null);
                            if(newEdits) {
                                result = new Dictionary<string, object>
                                {
                                    { "id", rev.GetDocId() },
                                    { "rev", rev.GetRevId() },
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
        /// <remarks>
        public static ICouchbaseResponseState GetChanges(ICouchbaseListenerContext context)
        {
            DBMonitorCouchbaseResponseState responseState = new DBMonitorCouchbaseResponseState();

            var responseObject = PerformLogicWithDatabase(context, true, db =>
            {
                var response = context.CreateResponse();
                responseState.Response = response;
                if (context.ChangesFeedMode < ChangesFeedMode.Continuous) {
                    if(context.CacheWithEtag(db.LastSequenceNumber.ToString())) {
                        response.InternalStatus = StatusCode.NotModified;
                        return response;
                    }
                }
                    
                var options = new ChangesOptions();
                responseState.ChangesFeedMode = context.ChangesFeedMode;
                responseState.ChangesIncludeDocs = context.GetQueryParam<bool>("include_docs", bool.TryParse, false);
                options.SetIncludeDocs(responseState.ChangesIncludeDocs);
                responseState.ChangesIncludeConflicts = context.GetQueryParam("style") == "all_docs";
                options.SetIncludeConflicts(responseState.ChangesIncludeConflicts);
                options.SetContentOptions(context.ContentOptions);
                options.SetSortBySequence(!options.IsIncludeConflicts());
                options.SetLimit(context.GetQueryParam<int>("limit", int.TryParse, options.GetLimit()));
                int since = context.GetQueryParam<int>("since", int.TryParse, 0);

                string filterName = context.GetQueryParam("filter");
                if(filterName != null) {
                    Status status = new Status();
                    responseState.ChangesFilter = db.GetFilter(filterName, status);
                    if(responseState.ChangesFilter == null) {
                        return context.CreateResponse(status.GetCode());
                    }
                }


                RevisionList changes = db.ChangesSince(since, options, responseState.ChangesFilter);
                if((context.ChangesFeedMode >= ChangesFeedMode.Continuous) || 
                    (context.ChangesFeedMode == ChangesFeedMode.LongPoll && changes.Count == 0)) {
                    // Response is going to stay open (continuous, or hanging GET):
                    response.Chunked = true;
                    if(context.ChangesFeedMode == ChangesFeedMode.EventSource) {
                        response["Content-Type"] = "text/event-stream; charset=utf-8";
                    }

                    if(context.ChangesFeedMode >= ChangesFeedMode.Continuous) {
                        response.WriteHeaders();
                        foreach(var rev in changes) {
                            response.SendContinuousLine(ChangesDictForRev(rev, responseState), context.ChangesFeedMode);
                        }
                    }

                    responseState.SubscribeToDatabase(db);
                    string heartbeatParam = context.GetQueryParam("heartbeat");
                    if(heartbeatParam != null) {
                        int heartbeat;
                        if(!int.TryParse(heartbeatParam, out heartbeat) || heartbeat <= 0) {
                            responseState.IsAsync = false;
                            return context.CreateResponse(StatusCode.BadParam);
                        }

                        heartbeat = Math.Min(heartbeat, MIN_HEARTBEAT);
                        string heartbeatResponse = context.ChangesFeedMode == ChangesFeedMode.EventSource ? "\n\n" : "\r\n";
                        responseState.StartHeartbeat(heartbeatResponse, heartbeat);
                    }

                    return context.CreateResponse();
                } else {
                    if(responseState.ChangesIncludeConflicts) {
                        response.JsonBody = new Body(ResponseBodyForChanges(changes, since, options.GetLimit(), responseState));
                    } else {
                        response.JsonBody = new Body(ResponseBodyForChanges(changes, since, responseState));
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
        /// <remarks>
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
        /// <remarks>
        public static ICouchbaseResponseState Purge(ICouchbaseListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                var body = context.BodyAs<Dictionary<string, IList<string>>>();
                if(body == null) {
                    return context.CreateResponse(StatusCode.BadJson);
                }

                var purgedRevisions = db.PurgeRevisions(body);
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
        /// <remarks>
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
                if (context.CacheWithEtag(db.LastSequenceNumber.ToString())) {
                    response.InternalStatus = StatusCode.NotModified;
                    return response;
                }

                var view = db.GetView("@@TEMPVIEW@@");
                var status = view.Compile(props, "javascript");
                if(status.IsError) {
                    response.InternalStatus = status.GetCode();
                    return response;
                }

                try {
                    view.UpdateIndex();
                    return QueryView(context, view, options);
                } catch(CouchbaseLiteException e) {
                    response.InternalStatus = e.GetCBLStatus().GetCode();
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
            Database db = context.DbManager.GetDatabaseWithoutOpening(dbName, false);
            if (db == null || !db.Exists()) {
                return context.CreateResponse(StatusCode.NotFound);
            }

            if (open) {
                bool opened = db.Open();
                if (!opened) {
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
            foreach (var change in changes) {
                results.Add(DatabaseMethods.ChangesDictForRev(change, responseState));
            }

            if (changes.Count > 0) {
                since = changes.Last().GetSequence();
            }

            return new Dictionary<string, object> {
                { "results", results },
                { "last_seq", since }
            };
        }

        /// <summary>
        /// Creates a dictionary of metadata for one specific revision
        /// </summary>
        /// <returns>The metadata dictionary</returns>
        /// <param name="rev">The revision to examine</param>
        /// <param name="responseState">The current response state</param>
        public static IDictionary<string, object> ChangesDictForRev(RevisionInternal rev, DBMonitorCouchbaseResponseState responseState)
        {
            return new NonNullDictionary<string, object> {
                { "seq", rev.GetSequence() },
                { "id", rev.GetDocId() },
                { "changes", new List<object> { 
                        new Dictionary<string, object> { 
                            { "rev", rev.GetRevId() } 
                        } 
                    } 
                },
                { "deleted", rev.IsDeleted() ? (object)true : null },
                { "doc", responseState.ChangesIncludeDocs ? rev.GetProperties() : null }
            };
        }

        /// <summary>
        /// Queries the specified view using the specified options
        /// </summary>
        /// <returns>The HTTP response containing the results of the query</returns>
        /// <param name="context">The request context</param>
        /// <param name="view">The view to query</param>
        /// <param name="options">The options to apply to the query</param>
        public static CouchbaseLiteResponse QueryView(ICouchbaseListenerContext context, View view, QueryOptions options)
        {
            var result = view.QueryWithOptions(options);
            object updateSeq = options.IsUpdateSeq() ? (object)view.LastSequenceIndexed : null;
            var mappedDic = result.Select(x => new NonNullDictionary<string, object> {
                { "id", x.DocumentId },
                { "key", x.Key },
                { "value", x.Value },
                { "doc", x.DocumentProperties }
            });

            var body = new Body(new NonNullDictionary<string, object> {
                { "rows", mappedDic },
                { "total_rows", view.TotalRows },
                { "offset", options.GetSkip() },
                { "update_seq", updateSeq }
            });

            var retVal = context.CreateResponse();
            retVal.JsonBody = body;
            return retVal;
        }

        #endregion

        #region Private Methods
            
        //Do an all document request on the database (i.e. fetch all docs given some options)
        private static CouchbaseLiteResponse DoAllDocs(ICouchbaseListenerContext context, Database db, QueryOptions options)
        {
            var result = db.GetAllDocs(options);
            if (!result.ContainsKey("rows")) {
                return context.CreateResponse(StatusCode.BadJson);
            }

            var documentProps = from row in (List<QueryRow>)result["rows"] select row.AsJSONDictionary();
            result["rows"] = documentProps;
            var response = context.CreateResponse();
            response.JsonBody = new Body(result);
            return response;
        }

        //Create a response body for an HTTP response from a given list of DB changes, including all conflicts
        private static IDictionary<string, object> ResponseBodyForChanges(RevisionList changes, long since, int limit, DBMonitorCouchbaseResponseState state)
        {
            string lastDocId = null;
            IDictionary<string, object> lastEntry = null;
            var entries = new List<IDictionary<string, object>>();
            foreach (var rev in changes) {
                string docId = rev.GetDocId();
                if (docId.Equals(lastDocId)) {
                    ((IList)lastEntry["changes"]).Add(new Dictionary<string, object> { { "rev", rev.GetRevId() } });
                } else {
                    lastEntry = ChangesDictForRev(rev, state);
                    entries.Add(lastEntry);
                    lastDocId = docId;
                }
            }
                    
            entries.Sort((x, y) => (int)((long)x["seq"] - (long)y["seq"]));
            if (entries.Count > limit) {
                entries.RemoveRange(limit, entries.Count - limit);
            }

            long lastSequence = entries.Any() ? (long)entries.Last()["seq"] : since;
            return new Dictionary<string, object> {
                { "results", entries },
                { "last_seq", lastSequence }
            };
        }

        #endregion
    }
}

