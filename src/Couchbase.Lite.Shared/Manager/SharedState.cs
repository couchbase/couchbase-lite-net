//
//  SharedState.cs
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
using System.Collections.Concurrent;
using DbDict = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, object>>;

using Couchbase.Lite;

namespace Couchbase.Lite
{
    // Container for shared state between Database instances that represent the same database file. API is thread-safe.
    internal sealed class SharedState
    {

        #region Variables

        private ConcurrentDictionary<string, DbDict> _databases = new ConcurrentDictionary<string, DbDict>();
        private ConcurrentDictionary<string, int> _openDatabaseNames = new ConcurrentDictionary<string, int>();
        private object _locker = new object();

        #endregion

        #region Public Methods

        public void SetValue<T>(string type, string name, string dbName, T value)
        {
            DbDict dbDict = _databases.GetOrAdd(dbName, k => new DbDict());
            ConcurrentDictionary<string, object> typeDict = dbDict.GetOrAdd(type, k => new ConcurrentDictionary<string, object>());
            typeDict.AddOrUpdate(name, k => value, (k, v) => value);
        }

        public bool TryGetValue<T>(string type, string name, string dbName, out T result)
        {
            result = default(T);
            DbDict dbDict = _databases.GetOrAdd(dbName, k => new DbDict());
            ConcurrentDictionary<string, object> typeDict = dbDict.GetOrAdd(type, k => new ConcurrentDictionary<string, object>());
            object val;
            return typeDict.TryGetValue(name, out val) && ExtensionMethods.TryCast<T>(val, out result);
        }

        public bool HasValues(string type, string dbName)
        {
            DbDict dbDict = _databases.GetOrAdd(dbName, k => new DbDict());
            ConcurrentDictionary<string, object> typeDict = dbDict.GetOrAdd(type, k => new ConcurrentDictionary<string, object>());
            return typeDict.Count > 0;
        }

        public IDictionary<string, object> GetValues(string type, string dbName)
        {
            DbDict dbDict = _databases.GetOrAdd(dbName, k => new DbDict());
            ConcurrentDictionary<string, object> typeDict = dbDict.GetOrAdd(type, k => new ConcurrentDictionary<string, object>());
            Dictionary<string, object> retVal = new Dictionary<string, object>();
            foreach (var pair in typeDict) {
                retVal[pair.Key] = pair.Value;
            }

            return retVal;
        }

        public void OpenedDatabase(Database db)
        {
            _openDatabaseNames.AddOrUpdate(db.Name, k => 1, (k, v) => v + 1);
        }

        public void ClosedDatabase(Database db)
        {
            lock(_locker) {
                int val;
                if (_openDatabaseNames.TryGetValue(db.Name, out val) && val == 1) {
                    DbDict dummy;
                    _databases.TryRemove(db.Name, out dummy);
                }

                _openDatabaseNames.AddOrUpdate(db.Name, k => 1, (k, v) => v - 1);
            }
        }

        public bool IsDatabaseOpened(Database db)
        {
            return _openDatabaseNames.ContainsKey(db.Name);
        }

        #endregion
    }
}

