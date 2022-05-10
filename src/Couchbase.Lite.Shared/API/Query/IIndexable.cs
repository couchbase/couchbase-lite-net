using Couchbase.Lite.Internal.Query;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    public interface IIndexable
    {
        /// <summary>
        /// Gets a list of index names that are present in the database
        /// </summary>
        /// <returns>The list of created index names</returns>
        [NotNull]
        [ItemNotNull]
        IReadOnlyList<string> GetIndexes();

        /// <summary>
        /// Creates a SQL index which could be a value index from <see cref="ValueIndexConfiguration"/> or a full-text search index
        /// from <see cref="FullTextIndexConfiguration"/> with the given name.
        /// The name can be used for deleting the index. Creating a new different index with an existing
        /// index name will replace the old index; creating the same index with the same name will be no-ops.
        /// </summary>
        /// <param name="name">The index name</param>
        /// <param name="indexConfig">The index</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> or <paramref name="indexConfig"/>
        /// is <c>null</c></exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        /// <exception cref="NotSupportedException">Thrown if an implementation of <see cref="IIndex"/> other than one of the library
        /// provided ones is used</exception>
        void CreateIndex([NotNull] string name, [NotNull] IndexConfiguration indexConfig);

        /// <summary>
        /// Deletes the index with the given name
        /// </summary>
        /// <param name="name">The name of the index to delete</param>
        void DeleteIndex([NotNull] string name);
    }
}
