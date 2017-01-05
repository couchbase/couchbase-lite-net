//
// ReaderConnectionPool.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

using SQLitePCL;

namespace Couchbase.Lite.Storage
{
    internal delegate void DisposeDelegate (ref sqlite3 connection);

    internal sealed class ConnectionPool : IDisposable
    {
        private readonly BlockingCollection<sqlite3> _pool = new BlockingCollection<sqlite3>(new ConcurrentBag<sqlite3>());
        private readonly ConcurrentDictionary<int, sqlite3> _inUseMap = new ConcurrentDictionary<int, sqlite3> ();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource ();
        private readonly DisposeDelegate _disposer;

        public ConnectionPool (int size, Func<sqlite3> generator, DisposeDelegate disposer)
        {
            for (int i = 0; i < size; i++) {
                _pool.Add (generator());
            }

            _disposer = disposer;
        }

        public Connection Acquire()
        {
            var gotConnection = _inUseMap.GetOrAdd (Thread.CurrentThread.ManagedThreadId, _ => {
                sqlite3 connection;
                if (_pool.TryTake (out connection, -1, _cts.Token)) {
                    return connection;
                }

                return null;
            });

            return gotConnection == null ? null : new Connection (gotConnection, this);
        }

        internal void Release()
        {
            sqlite3 connection;
            if(_inUseMap.TryRemove(Thread.CurrentThread.ManagedThreadId, out connection)) {
                _pool.Add (connection);
            }
        }

        public void Dispose()
        {
            _cts.Cancel ();
            foreach(var connection in _pool) {
                var c = connection;
                _disposer (ref c);
            }

            _pool.Dispose();
        }

    }

    internal sealed class Connection : IDisposable
    {
        public readonly sqlite3 Raw;

        private readonly ConnectionPool _parent;

        public Connection(sqlite3 connection, ConnectionPool parent)
        {
            Raw = connection;
            _parent = parent;
        }

        public void Dispose()
        {
            _parent?.Release ();
        }
    }
}
