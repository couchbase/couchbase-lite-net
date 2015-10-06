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

        public static readonly string[] ALL_KNOWN_PREFIXES = new string[] { "touchdb", "cblite" };

        #if !NOSQLITE

        private static readonly Dictionary<string, Func<Database, string, IDatabaseUpgrader>> UPGRADER_MAP = 
            new Dictionary<string, Func<Database, string, IDatabaseUpgrader>> {
            { "touchdb", (db, path) => new v1_upgrader(db, path) },     //Old naming
            { "cblite", (db, path) => new v1_upgrader(db, path) },      //possible v1.0 schema
        };

        public static IDatabaseUpgrader CreateUpgrader(Database db, string path)
        {
            var generator = UPGRADER_MAP.Get(path.Split('.').Last());
            if (generator != null) {
                return generator(db, path);
            }

            return null;
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

        #endif
    }
}
