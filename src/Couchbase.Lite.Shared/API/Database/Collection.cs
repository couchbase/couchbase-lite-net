using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Support;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed class Collection : ICollection, IDisposable, IChangeObservable<DatabaseChangedEventArgs>,
        IDocumentChangeObservable
    {
        [NotNull]
        internal ThreadSafety ThreadSafety { get; } = new ThreadSafety();

        public string Name => throw new NotImplementedException();

        public IScope Scope => throw new NotImplementedException();

        public ulong Count => throw new NotImplementedException();

        public ListenerToken AddChangeListener([CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken AddChangeListener([NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken AddDocumentChangeListener([NotNull] string id, [CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<DocumentChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken AddDocumentChangeListener([NotNull] string id, [NotNull] EventHandler<DocumentChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public void CreateIndex([NotNull] string name, [NotNull] IndexConfiguration indexConfig)
        {
            throw new NotImplementedException();
        }

        public void Delete([NotNull] Document document)
        {
            throw new NotImplementedException();
        }

        public bool Delete([NotNull] Document document, ConcurrencyControl concurrencyControl)
        {
            throw new NotImplementedException();
        }

        public void DeleteIndex([NotNull] string name)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Document GetDocument([NotNull] string id)
        {
            throw new NotImplementedException();
        }

        public DateTimeOffset? GetDocumentExpiration(string docId)
        {
            throw new NotImplementedException();
        }

        public IList<string> GetIndexes()
        {
            throw new NotImplementedException();
        }

        public void Purge([NotNull] Document document)
        {
            throw new NotImplementedException();
        }

        public void Purge([NotNull] string docId)
        {
            throw new NotImplementedException();
        }

        public void RemoveChangeListener(ListenerToken token)
        {
            throw new NotImplementedException();
        }

        public void Save([NotNull] MutableDocument document)
        {
            throw new NotImplementedException();
        }

        public bool Save([NotNull] MutableDocument document, ConcurrencyControl concurrencyControl)
        {
            throw new NotImplementedException();
        }

        public bool Save(MutableDocument document, Func<MutableDocument, Document, bool> conflictHandler)
        {
            throw new NotImplementedException();
        }

        public bool SetDocumentExpiration(string docId, DateTimeOffset? expiration)
        {
            throw new NotImplementedException();
        }
    }
}
