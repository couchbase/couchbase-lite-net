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
using System.Net;
using Couchbase.Lite.Internal;
using System.Collections.Generic;

namespace Couchbase.Lite.PeerToPeer
{
    internal static class DatabaseMethods
    {
        public static CouchbaseLiteResponse GetDatabaseConfiguration(HttpListenerContext context)
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

        public static CouchbaseLiteResponse DeleteDatabaseConfiguration(HttpListenerContext context) 
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
    }
}

