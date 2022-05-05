using Couchbase.Lite.Query;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed class Scope : IScope, IDisposable, IChangeObservable<DatabaseChangedEventArgs>
    {
        public string Name => throw new NotImplementedException();

        public List<Collection> Collections => throw new NotImplementedException();

        public ListenerToken<DatabaseChangedEventArgs> AddChangeListener([CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken<DatabaseChangedEventArgs> AddChangeListener([NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public void RemoveChangeListener(ListenerToken<DatabaseChangedEventArgs> token)
        {
            throw new NotImplementedException();
        }

        public IQuery CreateQuery([NotNull] string queryExpression)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Collection GetCollection(string name)
        {
            throw new NotImplementedException();
        }
    }
}
