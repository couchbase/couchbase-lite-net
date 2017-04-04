using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Lite.Query
{
    public sealed class LiveQueryChangedEventArgs : EventArgs
    {
        public IEnumerable<IQueryRow> Results { get; }

        internal LiveQueryChangedEventArgs(IEnumerable<IQueryRow> results)
        {
            Results = results;
        }
    }

    internal sealed class LiveQuery : ILiveQuery
    {
        private readonly IQuery _underlying;
        private readonly IDatabase _database;
        private bool _started;
        
        public event EventHandler<LiveQueryChangedEventArgs> Changed;

        public IEnumerable<IQueryRow> Results { get; private set; }

        internal LiveQuery(IDatabase database, IQuery underlying)
        {
            _database = database;
            _underlying = underlying;
        }

        public void Start()
        {
            _database.Changed += RerunQuery;
            Results = _underlying.Run();
            _started = true;
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

        private void FireChangedAndUpdate(IEnumerable<IQueryRow> newResults)
        {
            Changed?.Invoke(this, new LiveQueryChangedEventArgs(newResults));
            Results = newResults;
        }

        public void Dispose()
        {
            if (!_started) {
                return;
            }

            _started = false;
            _underlying.Dispose();
            _database.Changed -= RerunQuery;
        }
    }
}
