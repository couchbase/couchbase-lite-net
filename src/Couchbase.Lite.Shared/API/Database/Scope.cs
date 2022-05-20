using Couchbase.Lite.Query;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed class Scope : IScope, IChangeObservable<DatabaseChangedEventArgs>
    {
        public string Name => throw new NotImplementedException();

        public IReadOnlyList<ICollection> Collections => throw new NotImplementedException();

        public ListenerToken AddChangeListener([CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken AddChangeListener([NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public void RemoveChangeListener(ListenerToken token)
        {
            throw new NotImplementedException();
        }

        public IQuery CreateQuery([NotNull] string queryExpression)
        {
            throw new NotImplementedException();
        }

        public ICollection GetCollection(string name)
        {
            throw new NotImplementedException();
        }
    }
}
