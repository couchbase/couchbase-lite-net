using JetBrains.Annotations;
using System;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public interface IChangeObservable<TEventType> where TEventType : EventArgs
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
        ListenerToken<TEventType> AddChangeListener([CanBeNull] TaskScheduler scheduler,
            [NotNull] EventHandler<TEventType> handler);

        /// <summary>
        /// Adds a change listener for the changes that occur in this database.  Signatures
        /// are the same as += style event handlers.  The callback will be invoked on a thread pool
        /// thread.
        /// </summary>
        /// <param name="handler">The handler to invoke</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the handler later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        ListenerToken<TEventType> AddChangeListener([NotNull] EventHandler<TEventType> handler);

        /// <summary>
        /// Removes a previously added change listener via its <see cref="ListenerToken"/> and/or
        /// Removes a previously added documents ended listener via its <see cref="ListenerToken"/>
        /// </summary>
        /// <param name="token">The token received from <see cref="AddChangeListener(TaskScheduler, EventHandler{ReplicatorStatusChangedEventArgs})"/>
        /// and/or The token received from <see cref="AddDocumentReplicationListener(TaskScheduler, EventHandler{DocumentReplicationEventArgs})"/></param>
        void RemoveChangeListener(ListenerToken<TEventType> token);
    }
}
