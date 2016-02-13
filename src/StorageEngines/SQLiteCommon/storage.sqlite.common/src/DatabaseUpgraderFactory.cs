//
//  DatabaseUpgraderFactory.cs
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
using Sharpen;
using Couchbase.Lite.Util;

#if !NOSQLITE
using SQLitePCL;
#endif

namespace Couchbase.Lite.Db
{
    internal static partial class DatabaseUpgraderFactory
    {
        private const string TAG = "DatabaseUpgraderFactory";

        public static readonly string[] ALL_KNOWN_PREFIXES = new string[] { "touchdb", "cblite", "cblite2" };

        #if !NOSQLITE

        private static readonly Dictionary<string, int> UPGRADER_MAP =  new Dictionary<string, int> {
            { "touchdb", 0 },     //Old naming
            { "cblite", 0 },     // v1 schema
            { "cblite2", 1 }     // Newest version
        };

        private static readonly List<Func<Database, string, IDatabaseUpgrader>> UPGRADE_PATH = 
            new List<Func<Database, string, IDatabaseUpgrader>> {
            { (db, path) => new v1_upgrader(db, path) },
            { (db, path) => new v12_upgrader(db, path) }
        };


        public static IDatabaseUpgrader CreateUpgrader(Database db, string path)
        {
            var suffix = path.Split('.').Last();
            var index = 0;
            if (!UPGRADER_MAP.TryGetValue(suffix, out index)) {
                return null;
            }

            return UPGRADE_PATH[index](db, path);
        }

        private static int CollateRevIDs(object user_data, string s1, string s2)
        {
            throw new NotImplementedException();
        }

        internal static int SchemaVersion(string path) {
            int version = -1;
            sqlite3 sqlite;
            int err = raw.sqlite3_open_v2(path, out sqlite, raw.SQLITE_OPEN_READONLY, null);
            if (err != 0) {
                var errMsg = raw.sqlite3_errmsg(sqlite);
                Log.W(TAG, "Couldn't open SQLite {0} : {1}", path, errMsg);
                return version;
            }

            const string sql = "PRAGMA user_version";
            sqlite3_stmt versionQuery;
            err = raw.sqlite3_prepare_v2(sqlite, sql, out versionQuery);
            if (err == 0) {
                while (raw.SQLITE_ROW == raw.sqlite3_step(versionQuery)) {
                    version = raw.sqlite3_column_int(versionQuery, 0);
                }
            } else {
                var errMsg = raw.sqlite3_errmsg(sqlite);
                Log.W(TAG, "Couldn't compile SQL `{0}` : {1}", sql, errMsg);
            }

            raw.sqlite3_finalize(versionQuery);
            raw.sqlite3_close(sqlite);

            return version;
        }

        private static Status SqliteErrToStatus(int sqliteErr)
        {
            if (sqliteErr == raw.SQLITE_OK || sqliteErr == raw.SQLITE_DONE) {
                return new Status(StatusCode.Ok);
            }

            Log.W(TAG, "Upgrade failed: SQLite error {0}", sqliteErr);
            switch (sqliteErr) {
                case raw.SQLITE_NOTADB:
                    return new Status(StatusCode.BadRequest);
                case raw.SQLITE_PERM:
                    return new Status(StatusCode.Forbidden);
                case raw.SQLITE_CORRUPT:
                case raw.SQLITE_IOERR:
                    return new Status(StatusCode.CorruptError);
                case raw.SQLITE_CANTOPEN:
                    return new Status(StatusCode.NotFound);
                default:
                    return new Status(StatusCode.DbError);
            }
        }

        #endif
    }
}
