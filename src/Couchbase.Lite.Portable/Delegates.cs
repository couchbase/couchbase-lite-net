using System;
using System.Collections.Generic;
using Couchbase.Lite.Portable;

namespace Couchbase.Lite.Portable
{
    #region Global Delegates

    /// <summary>
    /// A delegate that can validate a key/value change.
    /// </summary>
    public delegate Boolean ValidateChangeDelegate(String key, Object oldValue, Object newValue);

    /// <summary>
    /// A delegate that can be run asynchronously on a <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public delegate void RunAsyncDelegate(IDatabase database);

    /// <summary>
    /// A delegate that can be used to accept/reject new <see cref="Couchbase.Lite.Revision"/>s being added to a <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public delegate Boolean ValidateDelegate(IRevision newRevision, IValidationContext context);

    /// <summary>
    /// A delegate that can be used to include/exclude <see cref="Couchbase.Lite.Revision"/>s during push <see cref="Couchbase.Lite.Replication"/>.
    /// </summary>
    public delegate Boolean FilterDelegate(ISavedRevision revision, Dictionary<String, Object> filterParams);

    /// <summary>
    /// A delegate that can be invoked to compile source code into a <see cref="FilterDelegate"/>.
    /// </summary>
    public delegate FilterDelegate CompileFilterDelegate(String source, String language);

    /// <summary>
    /// A delegate that can be run in a transaction on a <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public delegate Boolean RunInTransactionDelegate();

    ///
    /// <summary>The event raised when a <see cref="Couchbase.Lite.Database"/> changes</summary>
    ///
    public class DatabaseChangeEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> that raised the event.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Database"/> that raised the event.</value>
        public IDatabase Source { get; internal set; }

        /// <summary>
        /// Returns true if the change was not made by a Document belonging to this Database 
        /// (e.g. it came from another process or from a pull Replication), otherwise false.
        /// </summary>
        /// <value>true if the change was not made by a Document belonging to this Database 
        /// (e.g. it came from another process or from a pull Replication), otherwise false</value>
        public Boolean IsExternal { get; internal set; }

        /// <summary>
        /// Gets the DocumentChange details for the Documents that caused the Database change.
        /// </summary>
        /// <value>The DocumentChange details for the Documents that caused the Database change.</value>
        public IEnumerable<IDocumentChange> Changes { get; internal set; }

        /// <summary>
        /// empty ctor for use with initializer blocks
        /// </summary>
        internal DatabaseChangeEventArgs() { }

        /// <summary>
        /// public ctor to allow for portability
        /// </summary>
        /// <param name="Changes"></param>
        /// <param name="isExternal"></param>
        /// <param name="source"></param>
        public DatabaseChangeEventArgs(IEnumerable<IDocumentChange> Changes,
                                        bool isExternal, 
                                        IDatabase source)
        {
            Source = source;
            IsExternal = isExternal;
            this.Changes = Changes;
        }
    }

    #endregion

    #region Document Delegates

    /// <summary>
    /// A delegate that can be used to update a <see cref="Couchbase.Lite.Document"/>.
    /// </summary>
    /// <param name="revision">
    /// The <see cref="Couchbase.Lite.UnsavedRevision"/> to update.
    /// </param>
    /// <returns>
    /// True if the <see cref="Couchbase.Lite.UnsavedRevision"/> should be saved, otherwise false.
    /// </returns>
    public delegate Boolean DocUpdateDelegate(IUnsavedRevision revision);

    /// <summary>
    /// The type of event raised when a <see cref="Couchbase.Lite.Document"/> changes. 
    /// This event is not raised in response to local <see cref="Couchbase.Lite.Document"/> changes.
    ///</summary>
    public class DocumentChangeEventArgs : EventArgs
    {

        //Properties
        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Document"/> that raised the event.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Document"/> that raised the event</value>
        public IDocument Source { get; internal set; }

        /// <summary>
        /// Gets the details of the change.
        /// </summary>
        /// <value>The details of the change.</value>
        public IDocumentChange Change { get; internal set; }

        public DocumentChangeEventArgs(IDocumentChange c, IDocument s)
        {            Change = c; Source = s;        }

    }

    #endregion


    #region Replication EventArgs Subclasses

    ///
    /// <see cref="Couchbase.Lite.Replication"/> Change Event Arguments.
    ///
    public class ReplicationChangeEventArgs : EventArgs
    {
        //Properties
        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Replication"/> that raised the event.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Replication"/> that raised the event.</value>
        public IReplication Source { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Couchbase.Lite.ReplicationChangeEventArgs"/> class.
        /// </summary>
        /// <param name="sender">The <see cref="Couchbase.Lite.Replication"/> that raised the event.</param>
        public ReplicationChangeEventArgs(IReplication sender)
        {
            Source = sender;
        }
    }

    #endregion

    #region Replication Delegates

    public delegate IDictionary<string, object> PropertyTransformationDelegate(IDictionary<string, object> propertyBag);

    #endregion


    #region View Delegates

    /// <summary>
    /// A delegate that is invoked when a <see cref="Couchbase.Lite.Document"/> 
    /// is being added to a <see cref="Couchbase.Lite.View"/>.
    /// </summary>
    /// <param name="document">The <see cref="Couchbase.Lite.Document"/> being mapped.</param>
    /// <param name="emit">The delegate to use to add key/values to the <see cref="Couchbase.Lite.View"/>.</param>
    public delegate void MapDelegate(IDictionary<String, Object> document, EmitDelegate emit);

    /// <summary>
    /// A delegate that can be invoked to add key/values to a <see cref="Couchbase.Lite.View"/> 
    /// during a <see cref="Couchbase.Lite.MapDelegate"/> call.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public delegate void EmitDelegate(Object key, Object value);

    /// <summary>
    /// A delegate that can be invoked to summarize the results of a <see cref="Couchbase.Lite.View"/>.
    /// </summary>
    /// <param name="keys">A list of keys to be reduced, or null if this is a rereduce.</param>
    /// <param name="values">A parallel array of values to be reduced, corresponding 1-to-1 with the keys.</param>
    /// <param name="reduce"><c>true</c> if the input values are the results of previous reductions, otherwise <c>false</c>.</param>
    public delegate Object ReduceDelegate(IEnumerable<Object> keys, IEnumerable<Object> values, Boolean rereduce);

    #endregion

        /// <summary>
    /// An object that can be used to compile source code into map and reduce delegates.
    /// </summary>
    public partial interface IViewCompiler
    {

        #region Instance Members
        //Methods
        /// <summary>
        /// Compiles source code into a <see cref="Couchbase.Lite.MapDelegate"/>.
        /// </summary>
        /// <returns>A compiled <see cref="Couchbase.Lite.MapDelegate"/>.</returns>
        /// <param name="source">The source code to compile into a <see cref="Couchbase.Lite.MapDelegate"/>.</param>
        /// <param name="language">The language of the source.</param>
        MapDelegate CompileMap(String source, String language);

        /// <summary>
        /// Compiles source code into a <see cref="Couchbase.Lite.ReduceDelegate"/>.
        /// </summary>
        /// <returns>A compiled <see cref="Couchbase.Lite.ReduceDelegate"/>.</returns>
        /// <param name="source">The source code to compile into a <see cref="Couchbase.Lite.ReduceDelegate"/>.</param>
        /// <param name="language">The language of the source.</param>
        ReduceDelegate CompileReduce(String source, String language);

        #endregion
    }
}