//
//  CouchbaseLiteHTTPHandler.cs
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
using System.Collections.Generic;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.PeerToPeer
{
    internal abstract class CouchbaseLiteHTTPHandler
    {
        protected HttpListenerContext _context;
        private static readonly Dictionary<string, Func<CouchbaseLiteHTTPHandler>> _RootFactoryMap = 
            new Dictionary<string, Func<CouchbaseLiteHTTPHandler>>() {

        };

        private static readonly Dictionary<string, Func<string, CouchbaseLiteHTTPHandler>> _DbNameFactoryMap = 
            new Dictionary<string, Func<string, CouchbaseLiteHTTPHandler>>() {
            { "_local", (x) => new CouchbaseLiteLocalHandler(x) }
        };

        public static CouchbaseLiteHTTPHandler HandlerForContext(HttpListenerContext context)
        {
            var request = context.Request;
            var url = request.RawUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            Database db;
            if (url.Length > 0) {
                var dbName = url[0];
                var validName = Manager.IsValidDatabaseName(dbName);
                if (dbName.StartsWith("_") && !validName) {

                } else if (!validName) {
                    return new CouchbaseLiteReturnStatus(StatusCode.BadId);
                } else {
                    db = Manager.SharedInstance.GetExistingDatabase(dbName);
                    if (db == null) {
                        return new CouchbaseLiteReturnStatus(StatusCode.NotFound);
                    }
                }
            } else {

            }

            string docId = null;
            if (db != null && url.Length > 1) {
                var status = OpenDB(db);
                if (status.IsError) {
                    return new CouchbaseLiteReturnStatus(status);
                }
            }

            return null;
        }

        public abstract void Process();

        private static Status OpenDB(Database db) 
        {
            if (!db.Exists()) {
                return new Status(StatusCode.NotFound);
            }

            if (!db.Open()) {
                return new Status(StatusCode.InternalServerError);
            }

            return new Status(StatusCode.Ok);
        }

        private class CouchbaseLiteReturnStatus : CouchbaseLiteHTTPHandler
        {
            private readonly int _status;

            public CouchbaseLiteReturnStatus(StatusCode statusCode) {
                _status = (int)statusCode;
            }

            public override void Process()
            {
                _context.Response.StatusCode = _status;
                _context.Response.Close();
            }
        }
    }


}

