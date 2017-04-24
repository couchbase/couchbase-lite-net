// 
// LiveQuery.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;

using Couchbase.Lite.Query;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class LiveQuery : ILiveQuery
    {
        #region Variables

        private readonly IDatabase _database;
        private readonly IQuery _underlying;

        public event EventHandler<LiveQueryChangedEventArgs> Changed;
        private bool _started;

        #endregion

        #region Properties

        public IEnumerable<IQueryRow> Results { get; private set; }

        #endregion

        #region Constructors

        internal LiveQuery(IDatabase database, IQuery underlying)
        {
            _database = database;
            _underlying = underlying;
        }

        #endregion

        #region Public Methods

        public void Dispose()
        {
            if (!_started) {
                return;
            }

            _started = false;
            _underlying.Dispose();
            _database.Changed -= RerunQuery;
        }

        public void Start()
        {
            _database.Changed += RerunQuery;
            Results = _underlying.Run();
            _started = true;
        }

        #endregion

        #region Private Methods

        private void FireChangedAndUpdate(IEnumerable<IQueryRow> newResults)
        {
            Changed?.Invoke(this, new LiveQueryChangedEventArgs(newResults));
            Results = newResults;
        }

        private void RerunQuery(object sender, DatabaseChangedEventArgs e)
        {
            var newResults = _underlying.Run();
            using (var e1 = Results.GetEnumerator())
            using (var e2 = newResults.GetEnumerator()) {
                while (true) {
                    var moved1 = e1.MoveNext();
                    var moved2 = e2.MoveNext();
                    if (!moved1 && !moved2) {
                        // Both finished with the same count, and every result 
                        // was the same
                        return;
                    }

                    if (!moved1 || !moved2) {
                        // One of the results is shorter than the other, different
                        // count means different results
                        FireChangedAndUpdate(newResults);
                        return;
                    }

                    if (!e1.Current.Equals(e2.Current)) {
                        // Found a differing result!
                        FireChangedAndUpdate(newResults);
                        return;
                    }
                }
            }
        }

        #endregion
    }
}
