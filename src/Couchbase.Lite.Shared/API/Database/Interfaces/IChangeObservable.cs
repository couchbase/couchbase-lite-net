using Couchbase.Lite.Sync;
using JetBrains.Annotations;
using System;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public interface IChangeObservableRemovable
    {
        void RemoveChangeListener(ListenerToken token);
    }

    public interface IChangeObservable<TEventType> : IChangeObservableRemovable where TEventType : EventArgs
    {
        ListenerToken AddChangeListener([CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<TEventType> handler);

        ListenerToken AddChangeListener([NotNull] EventHandler<TEventType> handler);
    }

    public interface IDocumentChangeObservable : IChangeObservableRemovable
    {
        ListenerToken AddDocumentChangeListener([NotNull] string id, [CanBeNull] TaskScheduler scheduler,
            [NotNull] EventHandler<DocumentChangedEventArgs> handler);

        ListenerToken AddDocumentChangeListener([NotNull] string id, [NotNull] EventHandler<DocumentChangedEventArgs> handler);
    }

    public interface IDocumentReplicatedObservable : IChangeObservableRemovable
    {
        ListenerToken AddDocumentReplicationListener([NotNull] EventHandler<DocumentReplicationEventArgs> handler);

        ListenerToken AddDocumentReplicationListener([CanBeNull] TaskScheduler scheduler,
            [NotNull] EventHandler<DocumentReplicationEventArgs> handler);
    }
}
