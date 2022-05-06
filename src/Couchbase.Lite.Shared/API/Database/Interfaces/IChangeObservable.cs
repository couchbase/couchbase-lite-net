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
        /// <summary>
        /// Adds a change listener for the changes that occur in this database.  Signatures
        /// are the same as += style event handlers, but the callbacks will be called using the
        /// specified <see cref="TaskScheduler"/>.  If the scheduler is null, the default task
        /// scheduler will be used (scheduled via thread pool).
        /// </summary>
        /// <param name="scheduler">The scheduler to use when firing the change handler</param>
        /// <param name="handler">The handler to invoke</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the handler later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        ListenerToken AddChangeListener([CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<TEventType> handler);

        /// <summary>
        /// Adds a change listener for the changes that occur in this database.  Signatures
        /// are the same as += style event handlers.  The callback will be invoked on a thread pool
        /// thread.
        /// </summary>
        /// <param name="handler">The handler to invoke</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the handler later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
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
