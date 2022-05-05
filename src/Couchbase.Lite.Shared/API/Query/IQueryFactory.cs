using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    public interface IQueryFactory
    {
        /// <summary>
        /// Creates a Query object from the given SQL string.
        /// </summary>
        /// <param name="queryExpression">SQL Expression</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="queryExpression"/>
        /// is <c>null</c></exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="CouchbaseLiteException">Throw if compiling <paramref name="queryExpression"/> returns an error</exception>
        IQuery CreateQuery([NotNull] string queryExpression);
    }
}
