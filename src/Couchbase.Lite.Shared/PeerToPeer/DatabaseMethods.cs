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
using System.Linq;
using System.Net;
using Couchbase.Lite.Internal;
using System.Collections.Generic;

namespace Couchbase.Lite.PeerToPeer
{
    internal static class DatabaseMethods
    {
        public static CouchbaseLiteResponse GetConfiguration(HttpListenerContext context)
        {
            // http://wiki.apache.org/couchdb/HTTP_database_API#Database_Information
            return PerformLogicWithDatabase(context, true, db =>
            {
                int numDocs = db.DocumentCount;
                long updateSequence = db.LastSequenceNumber;
                if (numDocs < 0 || updateSequence < 0) {
                    return new CouchbaseLiteResponse() { InternalStatus = StatusCode.DbError };
                }

                var response = new CouchbaseLiteResponse();
                response.Body = new Body(new Dictionary<string, object> {
                    { "db_name", db.Name },
                    { "doc_count", numDocs },
                    { "update_seq", updateSequence },
                    { "committed_update_seq", updateSequence },
                    { "purge_seq", 0 }, //TODO: Implement
                    { "disk_size", db.TotalDataSize },
                    { "start_time", db.StartTime * 1000 }
                });

                return response;
            });
        }

        public static CouchbaseLiteResponse DeleteConfiguration(HttpListenerContext context) 
        {
            return PerformLogicWithDatabase(context, false, db =>
            {
                if(context.Request.QueryString["rev"] != null) {
                    // CouchDB checks for this; probably meant to be a document deletion
                    return new CouchbaseLiteResponse() { InternalStatus = StatusCode.BadId };
                }

                try {
                    db.Delete();
                } catch (CouchbaseLiteException) {
                    return new CouchbaseLiteResponse() { InternalStatus = StatusCode.InternalServerError };
                }

                return new CouchbaseLiteResponse();
            });
        }

        public static CouchbaseLiteResponse UpdateConfiguration(HttpListenerContext context)
        {
            string[] components = context.Request.Url.AbsolutePath.Split(new[]{ '/' }, StringSplitOptions.RemoveEmptyEntries);
            string dbName = components[0];
            Database db = Manager.SharedInstance.GetDatabaseWithoutOpening(dbName, false);
            if (db != null && db.Exists()) {
                return new CouchbaseLiteResponse() { InternalStatus = StatusCode.PreconditionFailed };
            }

            try {
                db.Open();
            } catch(CouchbaseLiteException) {
                return new CouchbaseLiteResponse() { InternalStatus = StatusCode.Exception };
            }

            return new CouchbaseLiteResponse() { InternalStatus = StatusCode.Created };
        }

        public static CouchbaseLiteResponse GetAllDocuments(HttpListenerContext context)
        {
            return PerformLogicWithDatabase(context, true, db =>
            {
                var options = GetQueryOptions(context);
                if(options == null) {
                    return new CouchbaseLiteResponse() { InternalStatus = StatusCode.BadParam };
                }

                return DoAllDocs(db, options);
            });
        }

        public static CouchbaseLiteResponse GetAllSpecifiedDocuments(HttpListenerContext context)
        {
            // http://wiki.apache.org/couchdb/HTTP_Bulk_Document_API
            return PerformLogicWithDatabase(context, true, db =>
            {
                var options = GetQueryOptions(context);
                if(options == null) {
                    return new CouchbaseLiteResponse() { InternalStatus = StatusCode.BadParam };
                }

                IList<object> keys = new List<object>();

                if(context.Request.HasEntityBody) {
                    try {
                        var body = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(context.Request.InputStream);
                        keys = body["keys"].AsList<object>();
                    } catch(CouchbaseLiteException) {
                        return new CouchbaseLiteResponse() { InternalStatus = StatusCode.BadJson };
                    } catch(KeyNotFoundException) {
                        return new CouchbaseLiteResponse() { InternalStatus = StatusCode.BadParam };
                    }
                }

                options.SetKeys(keys);
                return DoAllDocs(db, options);
            });
        }

        public static CouchbaseLiteResponse ProcessDocumentChangeOperations(HttpListenerContext context)
        {
            throw new NotImplementedException();
        }

        private static CouchbaseLiteResponse PerformLogicWithDatabase(HttpListenerContext context, bool open, 
            Func<Database, CouchbaseLiteResponse> action) 
        {
            string[] components = context.Request.Url.AbsolutePath.Split(new[]{ '/' }, StringSplitOptions.RemoveEmptyEntries);
            string dbName = components[0];
            Database db = Manager.SharedInstance.GetDatabaseWithoutOpening(dbName, false);
            if (db == null || !db.Exists()) {
                return new CouchbaseLiteResponse() { InternalStatus = StatusCode.NotFound };
            }

            if (open) {
                bool opened = false;
                try {
                    opened = db.Open();
                } catch (CouchbaseLiteException) {
                    return new CouchbaseLiteResponse() { InternalStatus = StatusCode.Exception };
                }

                if (!opened) {
                    return new CouchbaseLiteResponse() { InternalStatus = StatusCode.DbError };
                }
            }

            return action(db);
        }

        private static QueryOptions GetQueryOptions(HttpListenerContext context)
        {
            var options = new QueryOptions();
            var queryStr = context.Request.QueryString;
            options.SetSkip(queryStr.Get<int>("skip", int.TryParse, options.GetSkip()));
            options.SetLimit(queryStr.Get<int>("limit", int.TryParse, options.GetLimit()));
            options.SetGroupLevel(queryStr.Get<int>("group_level", int.TryParse, options.GetGroupLevel()));
            options.SetDescending(queryStr.Get<bool>("descending", bool.TryParse, options.IsDescending()));
            options.SetIncludeDocs(queryStr.Get<bool>("include_docs", bool.TryParse, options.IsIncludeDocs()));
            if (queryStr.Get<bool>("include_deleted", bool.TryParse, false)) {
                options.SetAllDocsMode(AllDocsMode.IncludeDeleted);
            } else if (queryStr.Get<bool>("include_conflicts", bool.TryParse, false)) { //non-standard
                options.SetAllDocsMode(AllDocsMode.ShowConflicts); 
            } else if(queryStr.Get<bool>("only_conflicts", bool.TryParse, false)) { //non-standard
                options.SetAllDocsMode(AllDocsMode.OnlyConflicts);
            }

            options.SetUpdateSeq(queryStr.Get<bool>("update_seq", bool.TryParse, false));
            options.SetInclusiveEnd(queryStr.Get<bool>("inclusive_end", bool.TryParse, false));
            //TODO: InclusiveStart
            //TODO: PrefixMatchLevel
            options.SetReduceSpecified(queryStr.Get("reduce") != null);
            options.SetReduce(queryStr.Get<bool>("reduce", bool.TryParse, false));
            options.SetGroup(queryStr.Get<bool>("group", bool.TryParse, false));
            options.SetContentOptions(GetContentOptions(context));

            // Stale options (ok or update_after):
            string stale = queryStr.Get("stale");
            if(stale != null) {
                if (stale.Equals("ok")) {
                    options.SetStale(IndexUpdateMode.Never);
                } else if (stale.Equals("update_after")) {
                    options.SetStale(IndexUpdateMode.After);
                }
            }

            IList<object> keys = null;
            try {
                keys = queryStr.JsonQuery("keys").AsList<object>();
                if (keys == null) {
                    var key = queryStr.JsonQuery("key");
                    if (key != null) {
                        keys = new List<object> { key };
                    }
                }
            } catch(CouchbaseLiteException) {
                return null;
            }

            if (keys != null) {
                options.SetKeys(keys);
            } else {
                try {
                    options.SetStartKey(queryStr.JsonQuery("start_key"));
                    options.SetEndKey(queryStr.JsonQuery("end_key"));
                    options.SetStartKeyDocId(queryStr.JsonQuery("startkey_docid") as string);
                    options.SetEndKeyDocId(queryStr.JsonQuery("endkey_docid") as string);
                } catch(CouchbaseLiteException) {
                    return null;
                }
            }

            //TODO:  Full text and bbox

            return options;
        }

        private static DocumentContentOptions GetContentOptions(HttpListenerContext context)
        {
            var queryStr = context.Request.QueryString;
            DocumentContentOptions options = DocumentContentOptions.None;
            if (queryStr.Get<bool>("attachments", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeAttachments;
            }

            if (queryStr.Get<bool>("local_seq", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeLocalSeq;
            }

            if (queryStr.Get<bool>("conflicts", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeConflicts;
            }

            if (queryStr.Get<bool>("revs", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeRevs;
            }

            if (queryStr.Get<bool>("revs_info", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeRevsInfo;
            }

            return options;
        }

        private static CouchbaseLiteResponse DoAllDocs(Database db, QueryOptions options)
        {
            var result = db.GetAllDocs(options);
            if (!result.ContainsKey("rows")) {
                return new CouchbaseLiteResponse() { InternalStatus = StatusCode.BadJson };
            }

            var documentProps = from row in (List<QueryRow>)result["rows"] select row.Document.Properties;
            result["rows"] = documentProps;
            var response = new CouchbaseLiteResponse();
            response.Body = new Body(result);
            return response;
        }
    }
}

