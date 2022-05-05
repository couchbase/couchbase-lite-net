using Couchbase.Lite.Query;
using JetBrains.Annotations;
using System;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public interface ICollection : IIndexable
    {
        /// <summary>
        /// Gets the Collection Name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the Scope of the Collection belongs to
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Gets the total documents in the Collection
        /// </summary>
        public ulong Count { get; }

        /// <summary>
        /// Gets the <see cref="Document"/> with the specified ID
        /// </summary>
        /// <param name="id">The ID to use when creating or getting the document</param>
        /// <returns>The instantiated document, or <c>null</c> if it does not exist</returns>
        [CanBeNull]
        public Document GetDocument([NotNull] string id);

        /// <summary>
        /// Saves the given <see cref="MutableDocument"/> into this database.  This call is equivalent to calling
        /// <see cref="Save(MutableDocument, ConcurrencyControl)" /> with a second argument of
        /// <see cref="ConcurrencyControl.LastWriteWins"/>
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a database
        /// other than the one it was previously added to</exception>
        public void Save([NotNull] MutableDocument document); // => Save(document, ConcurrencyControl.LastWriteWins);

        /// <summary>
        /// Saves the given <see cref="MutableDocument"/> into this database
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="concurrencyControl">The rule to use when encountering a conflict in the database</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a database
        /// other than the one it was previously added to</exception>
        /// <returns><c>true</c> if the save succeeded, <c>false</c> if there was a conflict</returns>
        public bool Save([NotNull] MutableDocument document, ConcurrencyControl concurrencyControl);

        /// <summary>
        /// Saves a document to the database. When write operations are executed concurrently, 
        /// and if conflicts occur, conflict handler will be called. Use the handler to directly
        /// edit the document.Returning true, will save the document. Returning false, will cancel
        /// the save operation.
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="conflictHandler">The conflict handler block which can be used to resolve it.</param> 
        /// <returns><c>true</c> if the save succeeded, <c>false</c> if there was a conflict</returns>
        public bool Save(MutableDocument document, Func<MutableDocument, Document, bool> conflictHandler);

        /// <summary>
        /// Deletes a document from the database.  When write operations are executed
        /// concurrently, the last writer will overwrite all other written values.
        /// Calling this method is the same as calling <see cref="Delete(Document, ConcurrencyControl)"/>
        /// with <see cref="ConcurrencyControl.LastWriteWins"/>
        /// </summary>
        /// <param name="document">The document</param>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.InvalidParameter"/>
        /// when trying to save a document into a database other than the one it was previously added to</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotFound"/>
        /// when trying to delete a document that hasn't been saved into a <see cref="Database"/> yet</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public void Delete([NotNull] Document document);// => Delete(document, ConcurrencyControl.LastWriteWins);

        /// <summary>
        /// Deletes the given <see cref="Document"/> from this database
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="concurrencyControl">The rule to use when encountering a conflict in the database</param>
        /// <returns><c>true</c> if the delete succeeded, <c>false</c> if there was a conflict</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.InvalidParameter"/>
        /// when trying to save a document into a database other than the one it was previously added to</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotFound"/>
        /// when trying to delete a document that hasn't been saved into a <see cref="Database"/> yet</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public bool Delete([NotNull] Document document, ConcurrencyControl concurrencyControl);

        /// <summary>
        /// Purges the given <see cref="Document"/> from the database.  This leaves
        /// no trace behind and will not be replicated
        /// </summary>
        /// <param name="document">The document to purge</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to purge a document from a database
        /// other than the one it was previously added to</exception>
        public void Purge([NotNull] Document document);

        /// <summary>
        /// Purges the given document id of the <see cref="Document"/> 
        /// from the database.  This leaves no trace behind and will 
        /// not be replicated
        /// </summary>
        /// <param name="docId">The id of the document to purge</param>
        /// <exception cref="C4ErrorCode.NotFound">Throws NOT FOUND error if the document 
        /// of the docId doesn't exist.</exception>
        public void Purge([NotNull] string docId);

        /// <summary>
        /// Sets an expiration date on a document. After this time, the document
        /// will be purged from the database.
        /// </summary>
        /// <param name="docId"> The ID of the <see cref="Document"/> </param> 
        /// <param name="expiration"> Nullable expiration timestamp as a 
        /// <see cref="DateTimeOffset"/>, set timestamp to <c>null</c> 
        /// to remove expiration date time from doc.</param>
        /// <returns>Whether successfully sets an expiration date on the document</returns>
        /// <exception cref="CouchbaseLiteException">Throws NOT FOUND error if the document 
        /// doesn't exist</exception>
        public bool SetDocumentExpiration(string docId, DateTimeOffset? expiration);

        /// <summary>
        /// Returns the expiration time of the document. <c>null</c> will be returned
        /// if there is no expiration time set
        /// </summary>
        /// <param name="docId"> The ID of the <see cref="Document"/> </param>
        /// <returns>Nullable expiration timestamp as a <see cref="DateTimeOffset"/> 
        /// of the document or <c>null</c> if time not set. </returns>
        /// <exception cref="CouchbaseLiteException">Throws NOT FOUND error if the document 
        /// doesn't exist</exception>
        public DateTimeOffset? GetDocumentExpiration(string docId);

        /// <summary>
        /// [Obsolete("AddDocumentChangeListener is deprecated, please use <see cref="GetDefaultCollection().AddDocumentChangeListener"/>.")]
        /// [DEPRECATED] Adds a document change listener for the document with the given ID and the <see cref="TaskScheduler"/>
        /// that will be used to invoke the callback.  If the scheduler is not specified, then the default scheduler
        /// will be used (scheduled via thread pool)
        /// </summary>
        /// <param name="id">The document ID</param>
        /// <param name="scheduler">The scheduler to use when firing the event handler</param>
        /// <param name="handler">The logic to handle the event</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the listener later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> or <paramref name="id"/>
        /// is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public ListenerToken<DocumentChangedEventArgs> AddDocumentChangeListener([NotNull] string id, [CanBeNull] TaskScheduler scheduler,
            [NotNull] EventHandler<DocumentChangedEventArgs> handler);

        /// <summary>
        /// [Obsolete("AddDocumentChangeListener is deprecated, please use <see cref="GetDefaultCollection().AddDocumentChangeListener"/>.")]
        /// [DEPRECATED] Adds a document change listener for the document with the given ID.  The callback will be
        /// invoked on a thread pool thread.
        /// </summary>
        /// <param name="id">The document ID</param>
        /// <param name="handler">The logic to handle the event</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the listener later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> or <paramref name="id"/>
        /// is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public ListenerToken<DocumentChangedEventArgs> AddDocumentChangeListener([NotNull] string id, [NotNull] EventHandler<DocumentChangedEventArgs> handler);// => AddDocumentChangeListener(id, null, handler);

    }
}
