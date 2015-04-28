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

namespace Couchbase.Lite.Db
{
    internal static partial class DatabaseUpgraderFactory
    {
        public static readonly string[] ALL_KNOWN_PREFIXES = new string[] { "touchdb", "cblite", "cblite2" };

        private static readonly Dictionary<string, Func<Database, string, IDatabaseUpgrader>> UPGRADER_MAP = 
            new Dictionary<string, Func<Database, string, IDatabaseUpgrader>> {
            { "touchdb", (db, path) => new v1_upgrader(db, path) },     //Old naming
            { "cblite", (db, path) => new v1_upgrader(db, path) },      //v1 schema
            { "cblite2", (db, path) => new NoopUpgrader(db, path) }     //iOS v2 schema
        };

        public static IDatabaseUpgrader CreateUpgrader(Database db, string path)
        {
            var generator = UPGRADER_MAP.Get(path.Split('.').Last());
            if (generator != null) {
                return generator(db, path);
            }

            return null;
        }
    }
}

