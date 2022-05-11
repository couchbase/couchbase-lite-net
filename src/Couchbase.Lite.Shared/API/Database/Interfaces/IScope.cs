using Couchbase.Lite.Query;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    public interface IScope : IQueryFactory
    {
        /// <summary>
        /// Gets the Scope Name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets all collections in the Scope
        /// </summary>
        IReadOnlyList<ICollection> Collections { get; }

        /// <summary>
        /// Gets one collection of the given name
        /// </summary>
        /// <param name="name">The collection name</param>
        /// <returns>null will be returned if the collection doesn't exist in the Scope</returns>
        ICollection GetCollection(string name);
    }
}
